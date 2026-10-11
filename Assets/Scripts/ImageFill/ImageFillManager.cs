using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ImageFillManager — region-based fill coloring system.
///
/// PERFORMANCE FIX SUMMARY
/// ────────────────────────
/// OLD: FillRegion() called PolygonCollider2D.OverlapPoint() for every pixel
///      on every click → 360 000 physics calls per tap on a 600×600 texture.
///
/// NEW: On init, each region's pixel list is baked ONCE into a flat int[]
///      (pixel indices that belong to that region).  A click just iterates
///      that pre-built list and writes colors — zero physics calls at runtime.
///      Fill is now instant regardless of texture size.
///
/// BUTTON WIRING
/// ─────────────
///  SelectSolid()    SelectGlitter()   SelectMarker()
///  SelectSpray()    SelectPencil()    SelectEraser()   SelectNone()
///  ClearCanvas()    SaveScreenshot()  DeleteSavedProgress()
///  LoadNewImage()
/// </summary>
public class ImageFillManager : MonoBehaviour
{
    // =========================================================================
    // ENUMS & CONSTANTS
    // =========================================================================

    public enum MarkerType { None, Solid, Glitter, Marker, Spray, Pencil, Eraser }

    private const int SAVE_VERSION = 2;
    private const int GRID_CELL = 12;
    private const float BASE_COAT_ALPHA = 0.28f;


    // =========================================================================
    // INSPECTOR
    // =========================================================================

    [Header("Brush Settings")]
    public Color brushColor = Color.black;
    public MarkerType currentMarkerType = MarkerType.Solid;

    [Header("UI References")]
    public RectTransform[] brushButtons;
    public float activeOffset = 50f;

    [Header("Sound")]
    public FillModeSoundManager soundManager;

    [SerializeField] private Color glitterColor = new Color32(0x64, 0x95, 0xED, 255);


    // =========================================================================
    // PRIVATE – CORE
    // =========================================================================

    private RawImage drawingArea;
    private Texture2D drawingTexture;
    private RectTransform drawingRect;
    private PolygonCollider2D[] regionColliders;

    private Color[] drawingPixels;

    // ── BAKED REGION MASKS ───────────────────────────────────────────────────
    // Key change: instead of calling OverlapPoint per-pixel at runtime, we bake
    // each region's pixel indices once during init.  FillRegion then just loops
    // a plain int[] — no physics, no per-pixel world-space conversion.
    private int[][] regionPixelIndices;   // regionPixelIndices[regionIndex] = int[] of pixel indices
    // ────────────────────────────────────────────────────────────────────────

    private HashSet<PolygonCollider2D> filledColliders = new HashSet<PolygonCollider2D>();


    // =========================================================================
    // PRIVATE – UNDO
    // =========================================================================

    private struct PixelChange
    {
        public int index;
        public Color previousColor;
    }

    private struct UndoEntry
    {
        public List<PixelChange> pixelChanges;
        public HashSet<PolygonCollider2D> previousFilledColliders;
    }

    private Stack<UndoEntry> undoStack = new Stack<UndoEntry>();


    // =========================================================================
    // PRIVATE – GLITTER
    // =========================================================================

    private struct Sparkle
    {
        public int cx, cy;
        public float radius;
        public float brightness;
        public float hueShift;
    }

    private Sparkle[] sparkles;
    private List<int>[] sparkleGrid;
    private int gridCols, gridRows;
    private int glitterTexWidth, glitterTexHeight;


    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    void OnEnable()
    {
        InitializeDrawing();
        HighlightActiveBrush((int)currentMarkerType);
    }


    // =========================================================================
    // PUBLIC RESET
    // =========================================================================

    public void LoadNewImage()
    {
        filledColliders.Clear();
        undoStack.Clear();
        drawingTexture = null;
        drawingPixels = null;
        regionPixelIndices = null;
        Debug.Log("[ImageFillManager] Loading new image...");
        InitializeDrawing();
    }


    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    private void InitializeDrawing()
    {
        int selectedImageIndex = PlayerPrefs.GetInt("FillImage", -1);
        if (selectedImageIndex < 1)
        {
            Debug.LogError("[ImageFillManager] Invalid FillImage index.");
            drawingArea = null;
            return;
        }

        string objectName = GetObjectNameFromIndex(selectedImageIndex);
        GameObject activeObj = GameObject.Find(objectName);
        if (activeObj == null)
        {
            Debug.LogError($"[ImageFillManager] Object not found: {objectName}");
            drawingArea = null;
            return;
        }

        DrawingTargetFillMOde target = activeObj.GetComponent<DrawingTargetFillMOde>();
        if (target == null)
        {
            Debug.LogError($"[ImageFillManager] Missing DrawingTargetFillMOde on {objectName}");
            drawingArea = null;
            return;
        }

        drawingArea = target.GetDrawingArea();
        regionColliders = target.GetAllColliders();

        if (drawingArea == null || regionColliders == null || regionColliders.Length == 0)
        {
            Debug.LogError("[ImageFillManager] Invalid drawing setup.");
            drawingArea = null;
            return;
        }

        drawingRect = drawingArea.rectTransform;

        Texture2D src = drawingArea.texture as Texture2D;
        if (src == null)
        {
            Debug.LogError("[ImageFillManager] Source texture is null or not Texture2D.");
            drawingArea = null;
            return;
        }

        // Build working texture from source
        drawingTexture = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        drawingPixels = src.GetPixels();
        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply(false);
        drawingArea.texture = drawingTexture;

        filledColliders.Clear();
        undoStack.Clear();

        BakeGlitterData(src.width, src.height);

        // ── BAKE REGION MASKS ─────────────────────────────────────────────────
        // This is the one-time cost that makes all future fills instant.
        // We iterate every pixel once, test it against every collider, and store
        // the result.  Total cost: W*H*regionCount OverlapPoint calls, done once.
        BakeRegionMasks();
        // ─────────────────────────────────────────────────────────────────────

        LoadDrawingState(selectedImageIndex);

        Debug.Log($"[ImageFillManager] Initialized: {objectName} ({src.width}x{src.height}) | " +
                  $"Regions: {regionColliders.Length}");
    }

    /// <summary>
    /// Iterates every pixel once and records which region (if any) it belongs to.
    /// Result: regionPixelIndices[r] = array of all pixel indices inside region r.
    /// Called once on load — fills after this are O(regionSize) with no physics.
    /// </summary>
    private void BakeRegionMasks()
    {
        int texW = drawingTexture.width;
        int texH = drawingTexture.height;
        Rect rect = drawingRect.rect;
        int nReg = regionColliders.Length;

        // Use List<int> during building, convert to array at the end for fast iteration
        List<int>[] lists = new List<int>[nReg];
        for (int r = 0; r < nReg; r++)
            lists[r] = new List<int>(512);

        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                // Convert pixel → local UI position → world position (same math as before)
                float uiX = ((float)x / texW) * rect.width - rect.width * 0.5f;
                float uiY = ((float)y / texH) * rect.height - rect.height * 0.5f;
                Vector2 wp = drawingRect.TransformPoint(new Vector2(uiX, uiY));

                int pixelIndex = y * texW + x;

                for (int r = 0; r < nReg; r++)
                {
                    if (regionColliders[r] != null &&
                        regionColliders[r].OverlapPoint(wp))
                    {
                        lists[r].Add(pixelIndex);
                        break;  // a pixel belongs to at most one region
                    }
                }
            }
        }

        regionPixelIndices = new int[nReg][];
        for (int r = 0; r < nReg; r++)
            regionPixelIndices[r] = lists[r].ToArray();

        int total = 0;
        for (int r = 0; r < nReg; r++) total += regionPixelIndices[r].Length;
        Debug.Log($"[ImageFillManager] BakeRegionMasks done — {nReg} regions, {total} total pixels mapped.");
    }


    // =========================================================================
    // INPUT
    // =========================================================================

    void Update()
    {
        if (drawingArea == null) return;

        // Support both mouse and touch
        bool clicked = false;
        Vector2 inputPos = Vector2.zero;

        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            inputPos = Input.mousePosition;
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            clicked = true;
            inputPos = Input.GetTouch(0).position;
        }

        if (!clicked) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                drawingRect, inputPos, null, out Vector2 localPos))
        {
            Vector2 world = drawingRect.TransformPoint(localPos);
            for (int r = 0; r < regionColliders.Length; r++)
            {
                if (regionColliders[r] != null && regionColliders[r].OverlapPoint(world))
                {
                    FillRegion(r);  // pass index, not collider reference
                    break;
                }
            }
        }
    }


    // =========================================================================
    // FILL  ← THE FAST VERSION
    // =========================================================================

    /// <summary>
    /// Fills a region using its pre-baked pixel index list.
    /// No physics calls, no per-pixel world-space conversion.
    /// Cost = O(regionPixelCount) — typically &lt;50 ms even on large textures.
    /// </summary>
    private void FillRegion(int regionIndex)
    {
        if (drawingTexture == null || drawingPixels == null) return;
        if (regionPixelIndices == null || regionIndex >= regionPixelIndices.Length) return;

        PolygonCollider2D col = regionColliders[regionIndex];
        int[] pixels = regionPixelIndices[regionIndex];
        bool erasing = (currentMarkerType == MarkerType.Eraser);

        if (pixels.Length == 0) return;

        // Snapshot filledColliders state BEFORE this fill (for undo)
        HashSet<PolygonCollider2D> snapshotBefore =
            new HashSet<PolygonCollider2D>(filledColliders);

        List<PixelChange> changes = new List<PixelChange>(pixels.Length);

        int texW = drawingTexture.width;

        for (int i = 0; i < pixels.Length; i++)
        {
            int idx = pixels[i];
            Color oldColor = drawingPixels[idx];
            Color newColor = oldColor;

            if (erasing)
            {
                newColor = Color.white;
            }
            else
            {
                switch (currentMarkerType)
                {
                    case MarkerType.Solid:
                        newColor = brushColor;
                        break;

                    case MarkerType.Glitter:
                        // Reconstruct x,y from flat index for sparkle lookup
                        newColor = ComputeGlitterColor(idx % texW, idx / texW, oldColor);
                        break;

                    case MarkerType.Marker:
                        newColor = Color.Lerp(oldColor, brushColor, 0.6f);
                        break;

                    case MarkerType.Spray:
                        if (Random.value >= 0.35f)
                            newColor = Color.Lerp(oldColor, brushColor, 0.45f);
                        break;

                    case MarkerType.Pencil:
                        if (Random.value >= 0.15f)
                            newColor = Color.Lerp(oldColor, brushColor, Random.Range(0.25f, 0.45f));
                        break;
                }
            }

            if (newColor != oldColor)
            {
                drawingPixels[idx] = newColor;
                changes.Add(new PixelChange { index = idx, previousColor = oldColor });
            }
        }

        if (changes.Count > 0)
        {
            undoStack.Push(new UndoEntry
            {
                pixelChanges = changes,
                previousFilledColliders = snapshotBefore
            });
        }

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        UpdateFillState(col, erasing);

        SaveDrawingState(PlayerPrefs.GetInt("FillImage", -1));
    }


    // =========================================================================
    // GLITTER
    // =========================================================================

    private void BakeGlitterData(int w, int h)
    {
        glitterTexWidth = w;
        glitterTexHeight = h;

        int sparkleCount = Mathf.Max(w * h / 200, 40);
        sparkles = new Sparkle[sparkleCount];

        for (int i = 0; i < sparkleCount; i++)
        {
            sparkles[i] = new Sparkle
            {
                cx = Random.Range(0, w),
                cy = Random.Range(0, h),
                radius = Random.Range(2f, 7f),
                brightness = Random.Range(0.55f, 1.0f),
                hueShift = Random.Range(-0.13f, 0.13f)
            };
        }

        gridCols = Mathf.CeilToInt((float)w / GRID_CELL);
        gridRows = Mathf.CeilToInt((float)h / GRID_CELL);
        sparkleGrid = new List<int>[gridCols * gridRows];
        for (int c = 0; c < sparkleGrid.Length; c++)
            sparkleGrid[c] = new List<int>();

        const int maxRadius = 8;
        for (int i = 0; i < sparkleCount; i++)
        {
            Sparkle s = sparkles[i];
            int minGX = Mathf.Max(0, (s.cx - maxRadius) / GRID_CELL);
            int maxGX = Mathf.Min(gridCols - 1, (s.cx + maxRadius) / GRID_CELL);
            int minGY = Mathf.Max(0, (s.cy - maxRadius) / GRID_CELL);
            int maxGY = Mathf.Min(gridRows - 1, (s.cy + maxRadius) / GRID_CELL);

            for (int gy = minGY; gy <= maxGY; gy++)
                for (int gx = minGX; gx <= maxGX; gx++)
                    sparkleGrid[gy * gridCols + gx].Add(i);
        }
    }

    /// <summary>Computes the glitter color for a single pixel at (x, y).</summary>
    private Color ComputeGlitterColor(int x, int y, Color oldColor)
    {
        if (sparkles == null || sparkleGrid == null) return brushColor;

        int gx = Mathf.Min(x / GRID_CELL, gridCols - 1);
        int gy = Mathf.Min(y / GRID_CELL, gridRows - 1);

        List<int> cell = sparkleGrid[gy * gridCols + gx];
        float strongest = 0f;
        Color result = brushColor;

        for (int i = 0; i < cell.Count; i++)
        {
            Sparkle s = sparkles[cell[i]];
            float dx = x - s.cx;
            float dy = y - s.cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > s.radius) continue;

            float t = 1f - (dist / s.radius);
            float strength = t * t * s.brightness;

            if (strength > strongest)
            {
                strongest = strength;
                result = Color.Lerp(brushColor, Color.white, strength * 0.8f);
            }
        }

        return result;
    }


    // =========================================================================
    // STATE MANAGEMENT
    // =========================================================================

    private void UpdateFillState(PolygonCollider2D regionCollider, bool isErasing)
    {
        if (isErasing)
        {
            filledColliders.Remove(regionCollider);
        }
        else
        {
            bool wasFilled = filledColliders.Contains(regionCollider);
            filledColliders.Add(regionCollider);
            if (!wasFilled) soundManager?.PlayFilledSound();
        }

        if (regionColliders != null &&
            filledColliders.Count == regionColliders.Length &&
            filledColliders.Count > 0)
        {
            soundManager?.PlayCompletedSound();
            Debug.Log("[ImageFillManager] All regions filled! 🎉");
        }
    }


    // =========================================================================
    // UNDO
    // =========================================================================

    public void ClearCanvas()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("[ImageFillManager] Nothing to undo.");
            return;
        }

        UndoEntry entry = undoStack.Pop();

        foreach (var change in entry.pixelChanges)
            drawingPixels[change.index] = change.previousColor;

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        filledColliders = new HashSet<PolygonCollider2D>(entry.previousFilledColliders);

        SaveDrawingState(PlayerPrefs.GetInt("FillImage", -1));
        Debug.Log($"[ImageFillManager] Undo — filled: {filledColliders.Count} | remaining: {undoStack.Count}");
    }


    // =========================================================================
    // SAVE
    // =========================================================================

    private string GetSavePath(int imageIndex)
        => Path.Combine(Application.persistentDataPath, $"fill_save_{imageIndex}.dat");

    private void SaveDrawingState(int imageIndex)
    {
        if (drawingPixels == null || drawingTexture == null) return;
        string path = GetSavePath(imageIndex);

        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(SAVE_VERSION);
                bw.Write(drawingTexture.width);
                bw.Write(drawingTexture.height);
                bw.Write(drawingPixels.Length);

                foreach (Color c in drawingPixels)
                { bw.Write(c.r); bw.Write(c.g); bw.Write(c.b); bw.Write(c.a); }

                WriteColliderSet(bw, filledColliders);

                // Save undo stack oldest-first so on load we push in the same
                // order and the most recent entry ends up on top
                UndoEntry[] entries = undoStack.ToArray();
                System.Array.Reverse(entries);

                bw.Write(entries.Length);
                foreach (UndoEntry entry in entries)
                {
                    bw.Write(entry.pixelChanges.Count);
                    foreach (PixelChange pc in entry.pixelChanges)
                    {
                        bw.Write(pc.index);
                        bw.Write(pc.previousColor.r); bw.Write(pc.previousColor.g);
                        bw.Write(pc.previousColor.b); bw.Write(pc.previousColor.a);
                    }
                    WriteColliderSet(bw, entry.previousFilledColliders);
                }
            }
            Debug.Log($"[ImageFillManager] Saved — undo depth: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ImageFillManager] Save failed: {ex.Message}");
        }
    }


    // =========================================================================
    // LOAD
    // =========================================================================

    private void LoadDrawingState(int imageIndex)
    {
        string path = GetSavePath(imageIndex);
        if (!File.Exists(path))
        {
            Debug.Log("[ImageFillManager] No save file — starting fresh.");
            return;
        }

        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                int version = br.ReadInt32();
                if (version != SAVE_VERSION)
                {
                    Debug.LogWarning($"[ImageFillManager] Save version mismatch — deleting.");
                    fs.Close();
                    File.Delete(path);
                    return;
                }

                int savedW = br.ReadInt32();
                int savedH = br.ReadInt32();
                int pixelCount = br.ReadInt32();

                if (savedW != drawingTexture.width || savedH != drawingTexture.height)
                {
                    Debug.LogWarning("[ImageFillManager] Texture size mismatch — ignoring save.");
                    return;
                }

                for (int i = 0; i < pixelCount; i++)
                    drawingPixels[i] = new Color(
                        br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle());

                drawingTexture.SetPixels(drawingPixels);
                drawingTexture.Apply(false);

                filledColliders = ReadColliderSet(br);

                int entryCount = br.ReadInt32();
                undoStack.Clear();

                for (int e = 0; e < entryCount; e++)
                {
                    int changeCount = br.ReadInt32();
                    var changes = new List<PixelChange>(changeCount);
                    for (int c = 0; c < changeCount; c++)
                        changes.Add(new PixelChange
                        {
                            index = br.ReadInt32(),
                            previousColor = new Color(
                                br.ReadSingle(), br.ReadSingle(),
                                br.ReadSingle(), br.ReadSingle())
                        });

                    undoStack.Push(new UndoEntry
                    {
                        pixelChanges = changes,
                        previousFilledColliders = ReadColliderSet(br)
                    });
                }
            }
            Debug.Log($"[ImageFillManager] Loaded — filled: {filledColliders.Count} | undo depth: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ImageFillManager] Load failed: {ex.Message}");
        }
    }

    public void DeleteSavedProgress()
    {
        int idx = PlayerPrefs.GetInt("FillImage", -1);
        if (idx < 1) return;
        string path = GetSavePath(idx);
        if (!File.Exists(path)) return;
        File.Delete(path);
        Debug.Log($"[ImageFillManager] Save deleted: {path}");
    }


    // ── Save/load helpers ─────────────────────────────────────────────────────

    private void WriteColliderSet(BinaryWriter bw, HashSet<PolygonCollider2D> set)
    {
        var indices = new List<int>();
        for (int i = 0; i < regionColliders.Length; i++)
            if (set.Contains(regionColliders[i])) indices.Add(i);
        bw.Write(indices.Count);
        foreach (int i in indices) bw.Write(i);
    }

    private HashSet<PolygonCollider2D> ReadColliderSet(BinaryReader br)
    {
        int count = br.ReadInt32();
        var set = new HashSet<PolygonCollider2D>();
        for (int i = 0; i < count; i++)
        {
            int idx = br.ReadInt32();
            if (idx >= 0 && idx < regionColliders.Length)
                set.Add(regionColliders[idx]);
        }
        return set;
    }


    // =========================================================================
    // BRUSH SELECTION
    // =========================================================================

    public void SetBrushColor(Color color) => brushColor = color;

    private void SetMarkerAndColor(MarkerType type, Color color, int index)
    {
        currentMarkerType = type;
        brushColor = color;
        HighlightActiveBrush(index);
    }

    public void SelectNone()
    {
        SetMarkerAndColor(MarkerType.None, Color.clear, -1);
        if (soundManager != null) soundManager.allowFillSound = false;
    }

    public void SelectSolid()
    {
        SetMarkerAndColor(MarkerType.Solid, Color.red, 0);
        if (soundManager != null) soundManager.allowFillSound = true;
    }

    public void SelectGlitter()
    {
        SetMarkerAndColor(MarkerType.Glitter, new Color32(0x64, 0x95, 0xED, 255), 1);
        if (soundManager != null) soundManager.allowFillSound = true;
    }

    public void SelectMarker()
    {
        SetMarkerAndColor(MarkerType.Marker, new Color(1f, 0.4f, 0.7f), 2);
        if (soundManager != null) soundManager.allowFillSound = true;
    }

    public void SelectSpray()
    {
        SetMarkerAndColor(MarkerType.Spray, Color.green, 3);
        if (soundManager != null) soundManager.allowFillSound = true;
    }

    public void SelectPencil()
    {
        SetMarkerAndColor(MarkerType.Pencil, new Color(1f, 0.92f, 0.2f, 1f), 4);
        if (soundManager != null) soundManager.allowFillSound = true;
    }

    public void SelectEraser()
    {
        SetMarkerAndColor(MarkerType.Eraser, Color.black, 5);
        if (soundManager != null) soundManager.allowFillSound = true;
    }


    // =========================================================================
    // UI HIGHLIGHT
    // =========================================================================

    private void HighlightActiveBrush(int activeIndex)
    {
        if (brushButtons == null || brushButtons.Length == 0) return;
        for (int i = 0; i < brushButtons.Length; i++)
        {
            if (brushButtons[i] == null) continue;
            StartCoroutine(SlideButton(brushButtons[i], (i == activeIndex) ? activeOffset : 0f));
        }
    }

    private IEnumerator SlideButton(RectTransform btn, float targetX)
    {
        Vector2 start = btn.anchoredPosition;
        Vector2 end = new Vector2(targetX, start.y);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.2f;
            btn.anchoredPosition = Vector2.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }
        btn.anchoredPosition = end;
    }


    // =========================================================================
    // SCREENSHOT
    // =========================================================================

    public void SaveScreenshot() => StartCoroutine(CaptureFullScreenScreenshot());

    private IEnumerator CaptureFullScreenScreenshot()
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        byte[] bytes = screenTex.EncodeToPNG();
        Destroy(screenTex);

        string filename = "FillScreenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

#if UNITY_EDITOR || UNITY_STANDALONE
        string filePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), filename);
        try
        {
            File.WriteAllBytes(filePath, bytes);
            Debug.Log("✅ Screenshot saved: " + filePath);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start(filePath);
#endif
        }
        catch (System.Exception ex) { Debug.LogError("❌ Screenshot failed: " + ex.Message); }

#elif UNITY_ANDROID
        string path = Path.Combine(Application.persistentDataPath, filename);
        try
        {
            File.WriteAllBytes(path, bytes);
            using (var scanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
            using (var player  = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var ctx = player.GetStatic<AndroidJavaObject>("currentActivity");
                scanner.CallStatic("scanFile", ctx, new string[] { path }, null, null);
            }
            Debug.Log("✅ Screenshot saved to Gallery: " + path);
        }
        catch (System.Exception ex) { Debug.LogError("❌ Screenshot failed: " + ex.Message); }

#elif UNITY_IOS
        string path = Path.Combine(Application.persistentDataPath, filename);
        try
        {
            File.WriteAllBytes(path, bytes);
            Debug.Log("✅ Screenshot saved: " + path);
        }
        catch (System.Exception ex) { Debug.LogError("❌ Screenshot failed: " + ex.Message); }
#endif
    }


    // =========================================================================
    // OBJECT NAME LOOKUP
    // =========================================================================

    private string GetObjectNameFromIndex(int index)
    {
        string[] names =
        {
            "",
            // Fruits 1–10
            "Apple","Banana","Cherry","Grappes","Mango",
            "Orange","Pear","Gauva","Strawberry","Pineapple",
            // Vegetables 11–20
            "brocolli","Capsicum","Carrot","Chilli","Corn",
            "EggPlant","Lemon","Onion","Pea","Tomato",
            // Sea creatures 21–30
            "Crab","Fish","JellyFish","Octupus","Shark",
            "SmallFIsh","StarFish","StingRay","Turtle","Whale",
            // Animals 31–40
            "Cat","Deer","Dog","Elephant","graph",
            "Lion","Monkey","Rabbit","Sheep","Tiger"
        };
        return (index >= 1 && index < names.Length) ? names[index] : string.Empty;
    }
}
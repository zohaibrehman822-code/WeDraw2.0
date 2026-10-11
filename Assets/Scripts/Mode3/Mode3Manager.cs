using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Random = UnityEngine.Random;

public class Mode3Manager : MonoBehaviour
{
    // =========================================================================
    // ENUMS
    // =========================================================================

    public enum BrushType
    {
        None,
        Solid,
        Crayon,
        Marker,
        Spray,
        Pencil
    }


    // =========================================================================
    // INSPECTOR FIELDS
    // =========================================================================

    [Header("Sound")]
    public Mode3SoundManager soundManager;

    [Header("UI Elements")]
    public Slider brushSizeSlider;

    [Header("Brush Settings")]
    public Color brushColor = Color.black;
    public BrushType currentBrush = BrushType.Solid;

    [Header("Brush Tuning")]
    [SerializeField] private float sprayDensityMultiplier = 1f;
    [SerializeField] private float sprayOpacityMultiplier = 1f;
    [SerializeField] private float pencilScratchinessMultiplier = 1f;
    [SerializeField] private float pencilOpacityMultiplier = 1f;

    [Header("UI – Brush Buttons")]
    public RectTransform[] brushButtons;
    [SerializeField] private float activeOffset = -20f;


    // =========================================================================
    // PRIVATE STATE
    // =========================================================================

    // --- Drawing ---
    private RawImage drawingArea;
    private Texture2D drawingTexture;
    private RectTransform drawingRect;
    private Color[] drawingPixels;

    // --- Stroke tracking ---
    private bool isBrushing = false;
    private bool isDrawingStroke = false;

    /// <summary>
    /// Set to true once the FIRST valid mouse position of a stroke has been
    /// recorded.  Drawing is deferred until we have a second point so no
    /// stray dot is stamped at (0,0) on the first frame.
    /// </summary>
    private bool hasFirstPoint = false;
    private Vector2 firstPoint;          // stored first point of each stroke
    private Vector2? lastMousePos = null;

    private bool hasDrawnThisFrame = false;

    // --- Brush size preview ---
    private Color savedColor;
    private BrushType savedBrushType;
    private bool isAdjustingBrush = false;

    private readonly int textureScale = 1;

    // --- Undo system ---
    private struct PixelChange
    {
        public int index;
        public Color previousColor;
    }

    private Stack<List<PixelChange>> undoStack = new Stack<List<PixelChange>>();
    private Dictionary<int, Color> currentStrokeSnapshot = new Dictionary<int, Color>();

    // Save format version – bump when binary layout changes
    private const int SAVE_VERSION = 1;


    // =========================================================================
    // HELPERS
    // =========================================================================

    private int GetIndex(int x, int y)
    {
        if (drawingTexture == null) return -1;
        if (x < 0 || x >= drawingTexture.width || y < 0 || y >= drawingTexture.height) return -1;
        return y * drawingTexture.width + x;
    }

    /// <summary>
    /// Records the pixel's colour BEFORE this stroke touched it (once only).
    /// </summary>
    private void SnapshotPixel(int index)
    {
        if (index == -1) return;
        if (!currentStrokeSnapshot.ContainsKey(index))
            currentStrokeSnapshot[index] = drawingPixels[index];
    }


    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    void OnEnable()
    {
        InitializeDrawing();
        HighlightActiveBrush((int)currentBrush);
    }

    void InitializeDrawing()
    {
        int selectedImageIndex = PlayerPrefs.GetInt("Mode3Image", 0);
        if (selectedImageIndex <= 0)
        {
            Debug.LogError("Mode3Manager: Invalid or unset Mode3Image PlayerPref.");
            return;
        }

        string objectName = GetObjectNameFromIndex(selectedImageIndex);
        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject == null)
        {
            Debug.LogError("Mode3Manager: No GameObject found with name: " + objectName);
            return;
        }

        drawingArea = activeObject.GetComponentInChildren<RawImage>();
        drawingRect = drawingArea?.rectTransform;

        Texture2D sourceTexture = drawingArea.texture as Texture2D;
        if (sourceTexture == null)
        {
            Debug.LogError("Mode3Manager: RawImage texture is not a readable Texture2D – using blank fallback.");
            sourceTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            Color[] white = new Color[512 * 512];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            sourceTexture.SetPixels(white);
            sourceTexture.Apply();
        }

        drawingTexture = new Texture2D(
            sourceTexture.width / textureScale,
            sourceTexture.height / textureScale,
            TextureFormat.RGBA32, false);

        drawingPixels = sourceTexture.GetPixels();
        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        drawingArea.texture = drawingTexture;

        // Reset stroke state
        undoStack.Clear();
        currentStrokeSnapshot.Clear();
        isDrawingStroke = false;
        hasFirstPoint = false;
        lastMousePos = null;

        // Restore previously saved canvas + full undo history
        LoadDrawingState(selectedImageIndex);
    }


    // =========================================================================
    // MAIN DRAWING LOOP
    // =========================================================================

    void Update()
    {
        bool mouseDown = Input.GetMouseButton(0);

        if (mouseDown && drawingRect != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    drawingRect, Input.mousePosition, null, out Vector2 localMousePos))
            {
                if (drawingRect.rect.Contains(localMousePos))
                {
                    // ── Start brushing sound ──────────────────────────────
                    if (!isBrushing)
                    {
                        isBrushing = true;
                        soundManager?.PlayBrushingSound();
                    }

                    // ── Begin a new stroke ────────────────────────────────
                    if (!isDrawingStroke)
                    {
                        currentStrokeSnapshot.Clear();
                        isDrawingStroke = true;
                    }

                    // ── FIX: defer drawing until we have TWO valid points ─
                    //
                    // On the very first frame of a click, `localMousePos` is
                    // valid but `lastMousePos` is null.  Instead of drawing
                    // a single dot right away (which previously appeared at
                    // the texture's (0,0) corner when the rect-transform
                    // coordinate happened to be near its origin), we store
                    // the point and wait for the next frame.  On the second
                    // frame we draw from firstPoint → current, giving us a
                    // smooth stroke start with no phantom dot.
                    if (!hasFirstPoint)
                    {
                        firstPoint = localMousePos;
                        lastMousePos = localMousePos;
                        hasFirstPoint = true;
                        return; // wait for next frame
                    }

                    // ── Interpolated draw ─────────────────────────────────
                    Vector2 prev = lastMousePos.Value;
                    Vector2 curr = localMousePos;
                    float distance = Vector2.Distance(prev, curr);
                    int steps = Mathf.CeilToInt(distance * 2f);
                    // Guarantee at least the start point is drawn
                    steps = Mathf.Max(steps, 1);

                    for (int i = 0; i <= steps; i++)
                    {
                        Vector2 lerped = Vector2.Lerp(prev, curr, i / (float)steps);
                        DrawStrokeAt(lerped);
                    }

                    hasDrawnThisFrame = true;
                    lastMousePos = curr;
                    return;
                }
            }
        }

        // ── Mouse released or left the rect ──────────────────────────────────
        if (isBrushing)
        {
            isBrushing = false;
            soundManager?.StopBrushingSound();
        }

        if (isDrawingStroke)
            CommitStroke();

        // Reset stroke-start guard every time the button is released
        hasFirstPoint = false;
        lastMousePos = null;
    }

    void OnDisable()
    {
        isBrushing = false;
        soundManager?.StopBrushingSound();

        if (isDrawingStroke)
            CommitStroke();

        hasFirstPoint = false;
        lastMousePos = null;
    }

    void LateUpdate()
    {
        if (hasDrawnThisFrame)
        {
            drawingTexture.SetPixels(drawingPixels);
            drawingTexture.Apply();
            hasDrawnThisFrame = false;
        }
    }


    // ── Commit current stroke to the undo stack ───────────────────────────────
    private void CommitStroke()
    {
        if (!isDrawingStroke || currentStrokeSnapshot.Count == 0)
        {
            isDrawingStroke = false;
            return;
        }

        List<PixelChange> changes = new List<PixelChange>(currentStrokeSnapshot.Count);
        foreach (var kv in currentStrokeSnapshot)
            changes.Add(new PixelChange { index = kv.Key, previousColor = kv.Value });

        undoStack.Push(changes);
        currentStrokeSnapshot.Clear();
        isDrawingStroke = false;

        // Auto-save after every completed stroke
        SaveDrawingState(PlayerPrefs.GetInt("Mode3Image", 0));

        Debug.Log($"Stroke committed ✅ | {changes.Count} pixels | Undo steps: {undoStack.Count}");
    }


    // =========================================================================
    // BRUSH IMPLEMENTATIONS
    // =========================================================================

    void DrawStrokeAt(Vector2 localPos)
    {
        float normalizedX = (localPos.x + drawingRect.rect.width / 2f) / drawingRect.rect.width;
        float normalizedY = (localPos.y + drawingRect.rect.height / 2f) / drawingRect.rect.height;

        int centerX = Mathf.RoundToInt(normalizedX * drawingTexture.width);
        int centerY = Mathf.RoundToInt(normalizedY * drawingTexture.height);
        int radius = Mathf.RoundToInt(brushSizeSlider.value);

        switch (currentBrush)
        {
            case BrushType.Solid: DrawSolidBrush(centerX, centerY, radius); break;
            case BrushType.Crayon: DrawCrayonBrush(centerX, centerY, radius); break;
            case BrushType.Marker: DrawMarkerBrush(centerX, centerY, radius); break;
            case BrushType.Pencil: DrawPencilBrush(centerX, centerY, radius); break;
            case BrushType.Spray: DrawSprayBrush(centerX, centerY, radius); break;
            case BrushType.None:
            default: break;
        }
    }

    // ── Solid ─────────────────────────────────────────────────────────────────
    void DrawSolidBrush(int centerX, int centerY, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius) continue;

                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                int index = GetIndex(px, py);
                if (index == -1) continue;

                SnapshotPixel(index);
                drawingPixels[index] = brushColor;
            }
        }
    }

    // ── Crayon ────────────────────────────────────────────────────────────────
    void DrawCrayonBrush(int centerX, int centerY, int radius)
    {
        const float noiseFactor = 0.5f;
        const float opacity = 0.35f;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius) continue;
                if (Random.value > noiseFactor) continue;

                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                int index = GetIndex(px, py);
                if (index == -1) continue;

                SnapshotPixel(index);
                Color existing = drawingPixels[index];
                float blendAlpha = opacity + Random.Range(-0.1f, 0.1f);
                Color finalColor = Color.Lerp(existing, brushColor, blendAlpha);

                Color.RGBToHSV(finalColor, out float h, out float s, out float v);
                v = Mathf.Clamp01(v + Random.Range(-0.05f, 0.05f));
                finalColor = Color.HSVToRGB(h, s, v);

                drawingPixels[index] = finalColor;
            }
        }
    }

    // ── Marker ────────────────────────────────────────────────────────────────
    void DrawMarkerBrush(int centerX, int centerY, int radius)
    {
        const float markerOpacity = 0.35f;
        const float softEdgeAmount = 0.4f;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                float distSq = x * x + y * y;
                if (distSq > radius * radius) continue;

                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                int index = GetIndex(px, py);
                if (index == -1) continue;

                SnapshotPixel(index);
                Color existing = drawingPixels[index];
                float blendFactor = markerOpacity;
                float distance = Mathf.Sqrt(distSq);
                float softStart = radius * (1f - softEdgeAmount);

                if (distance > softStart)
                    blendFactor *= 1f - (distance - softStart) / (radius * softEdgeAmount);

                blendFactor = Mathf.Clamp01(blendFactor);
                drawingPixels[index] = Color.Lerp(existing, brushColor, blendFactor);
            }
        }
    }

    // ── Pencil ────────────────────────────────────────────────────────────────
    void DrawPencilBrush(int centerX, int centerY, int radius)
    {
        float pencilOpacity = 0.1f * pencilOpacityMultiplier;
        float scratchiness = 0.45f * pencilScratchinessMultiplier;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius) continue;
                if (Random.value < scratchiness) continue;

                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                int index = GetIndex(px, py);
                if (index == -1) continue;

                SnapshotPixel(index);
                drawingPixels[index] = Color.Lerp(drawingPixels[index], brushColor, pencilOpacity);
            }
        }
    }

    // ── Spray ─────────────────────────────────────────────────────────────────
    void DrawSprayBrush(int centerX, int centerY, int radius)
    {
        int sprayCount = Mathf.RoundToInt(radius * 4f * sprayDensityMultiplier);
        float sprayOpacity = 0.18f * sprayOpacityMultiplier;

        for (int i = 0; i < sprayCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius * 0.9f;
            int px = Mathf.Clamp(centerX + Mathf.RoundToInt(offset.x), 0, drawingTexture.width - 1);
            int py = Mathf.Clamp(centerY + Mathf.RoundToInt(offset.y), 0, drawingTexture.height - 1);
            int index = GetIndex(px, py);
            if (index == -1) continue;

            SnapshotPixel(index);
            drawingPixels[index] = Color.Lerp(drawingPixels[index], brushColor, sprayOpacity);
        }
    }


    // =========================================================================
    // UNDO
    // =========================================================================

    /// <summary>
    /// Undoes the last committed stroke, restoring every changed pixel to its
    /// colour before that stroke began.  Wire your Undo button to this method.
    /// </summary>
    public void UndoLastStroke()
    {
        // Safety: commit any in-progress stroke first
        if (isDrawingStroke)
            CommitStroke();

        if (undoStack.Count == 0)
        {
            Debug.Log("Nothing to undo ❌");
            return;
        }

        List<PixelChange> changes = undoStack.Pop();
        foreach (var change in changes)
            drawingPixels[change.index] = change.previousColor;

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        // Persist reverted state
        SaveDrawingState(PlayerPrefs.GetInt("Mode3Image", 0));

        Debug.Log($"Undo ✅ | Restored {changes.Count} pixels | Remaining steps: {undoStack.Count}");
    }


    // =========================================================================
    // SAVE / LOAD
    // =========================================================================

    private string GetSavePath(int imageIndex)
        => Path.Combine(Application.persistentDataPath, $"mode3_save_{imageIndex}.dat");

    /// <summary>
    /// Saves current pixels AND the full undo stack so every individual stroke
    /// survives a game restart.
    ///
    /// Binary layout (SAVE_VERSION = 1):
    ///   int32       SAVE_VERSION
    ///   int32       texWidth
    ///   int32       texHeight
    ///   int32       pixelCount
    ///   float*4     per pixel          ← current canvas
    ///   int32       undoEntryCount
    ///   per entry (written bottom→top):
    ///     int32     changeCount
    ///     per change: int32 pixelIndex, float*4 previousColor
    /// </summary>
    private void SaveDrawingState(int imageIndex)
    {
        if (drawingPixels == null || drawingTexture == null) return;

        string path = GetSavePath(imageIndex);

        try
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // Header
                bw.Write(SAVE_VERSION);
                bw.Write(drawingTexture.width);
                bw.Write(drawingTexture.height);
                bw.Write(drawingPixels.Length);

                // Current canvas pixels
                foreach (Color c in drawingPixels)
                { bw.Write(c.r); bw.Write(c.g); bw.Write(c.b); bw.Write(c.a); }

                // Undo stack – saved bottom→top so on load we push in that
                // order and the most-recent stroke ends up on top (correct).
                var entries = new List<PixelChange>[undoStack.Count];
                undoStack.CopyTo(entries, 0);   // top→bottom
                System.Array.Reverse(entries);  // now bottom→top

                bw.Write(entries.Length);
                foreach (var entry in entries)
                {
                    bw.Write(entry.Count);
                    foreach (var pc in entry)
                    {
                        bw.Write(pc.index);
                        bw.Write(pc.previousColor.r); bw.Write(pc.previousColor.g);
                        bw.Write(pc.previousColor.b); bw.Write(pc.previousColor.a);
                    }
                }
            }

            Debug.Log($"[Mode3Manager] Saved ✅  undo steps: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mode3Manager] Save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores canvas pixels and the complete undo stack exactly as they
    /// were when the game was closed.
    /// </summary>
    private void LoadDrawingState(int imageIndex)
    {
        string path = GetSavePath(imageIndex);

        if (!File.Exists(path))
        {
            Debug.Log("[Mode3Manager] No save file — starting fresh.");
            return;
        }

        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Header
                int version = br.ReadInt32();
                if (version != SAVE_VERSION)
                {
                    Debug.LogWarning($"[Mode3Manager] Save version mismatch ({version} vs {SAVE_VERSION}) — deleting old save.");
                    fs.Close();
                    File.Delete(path);
                    return;
                }

                int savedW = br.ReadInt32();
                int savedH = br.ReadInt32();
                int pixelCount = br.ReadInt32();

                if (savedW != drawingTexture.width || savedH != drawingTexture.height)
                {
                    Debug.LogWarning("[Mode3Manager] Texture size mismatch — ignoring save.");
                    return;
                }

                // Canvas pixels
                for (int i = 0; i < pixelCount; i++)
                    drawingPixels[i] = new Color(
                        br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle());

                drawingTexture.SetPixels(drawingPixels);
                drawingTexture.Apply(false);

                // Undo stack (entries were saved bottom→top; push in that order
                // so the most-recent stroke ends up on top)
                int entryCount = br.ReadInt32();
                undoStack.Clear();

                for (int e = 0; e < entryCount; e++)
                {
                    int changeCount = br.ReadInt32();
                    var changes = new List<PixelChange>(changeCount);

                    for (int c = 0; c < changeCount; c++)
                    {
                        changes.Add(new PixelChange
                        {
                            index = br.ReadInt32(),
                            previousColor = new Color(
                                br.ReadSingle(), br.ReadSingle(),
                                br.ReadSingle(), br.ReadSingle())
                        });
                    }
                    undoStack.Push(changes);
                }
            }

            Debug.Log($"[Mode3Manager] Loaded ✅  undo steps: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Mode3Manager] Load failed: {ex.Message}");
        }
    }

    /// <summary>Deletes the save file for the current image (wire to a Reset button if needed).</summary>
    public void DeleteSavedProgress()
    {
        int idx = PlayerPrefs.GetInt("Mode3Image", 0);
        if (idx <= 0) return;

        string path = GetSavePath(idx);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[Mode3Manager] Save deleted: {path}");
        }
    }


    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void SetBrushColor(Color color) => brushColor = color;

    public void ClearCanvas()
    {
        if (drawingTexture == null || drawingArea == null) return;

        // Snapshot entire canvas as one undoable step
        var changes = new List<PixelChange>(drawingPixels.Length);
        for (int i = 0; i < drawingPixels.Length; i++)
        {
            if (drawingPixels[i] == Color.white) continue;
            changes.Add(new PixelChange { index = i, previousColor = drawingPixels[i] });
            drawingPixels[i] = Color.white;
        }

        if (changes.Count > 0)
            undoStack.Push(changes);

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        SaveDrawingState(PlayerPrefs.GetInt("Mode3Image", 0));
        Debug.Log("✅ Canvas cleared");
    }

    public void UseEraser()
    {
        currentBrush = BrushType.Solid;
        brushColor = Color.white;
        HighlightActiveBrush(0);
    }

    public void SaveFullScreenScreenshot() => StartCoroutine(CaptureFullScreenScreenshot());


    // ── Brush switch buttons ──────────────────────────────────────────────────

    public void SetBrushToNone()
    {
        currentBrush = BrushType.None;
        if (soundManager != null) { soundManager.allowBrushingSound = false; soundManager.StopBrushingSound(); }
        HighlightActiveBrush(-1);
    }

    public void SetBrushToSolid()
    {
        brushColor = Color.red;
        currentBrush = BrushType.Solid;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush(0);
    }

    public void SetBrushToCrayon()
    {
        brushColor = Color.cyan;
        currentBrush = BrushType.Crayon;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush(1);
    }

    public void SetBrushToMarker()
    {
        brushColor = new Color(1f, 0.4f, 0.7f);
        currentBrush = BrushType.Marker;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush(2);
    }

    public void SetBrushToSpray()
    {
        brushColor = Color.green;
        currentBrush = BrushType.Spray;
        sprayDensityMultiplier = Application.isMobilePlatform ? 0.75f : 1.1f;
        sprayOpacityMultiplier = 1.15f;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush(3);
    }

    public void SetBrushToPencil()
    {
        brushColor = Color.yellow;
        currentBrush = BrushType.Pencil;
        pencilScratchinessMultiplier = Application.isMobilePlatform ? 0.8f : 0.6f;
        pencilOpacityMultiplier = 1.2f;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush(4);
    }


    // ── Brush size preview ────────────────────────────────────────────────────

    public void OnBrushSizeDragStart()
    {
        if (!isAdjustingBrush)
        {
            savedColor = brushColor;
            savedBrushType = currentBrush;
            brushColor = Color.white;
            currentBrush = BrushType.Solid;
            isAdjustingBrush = true;
        }
    }

    public void OnBrushSizeDragEnd()
    {
        if (isAdjustingBrush)
            StartCoroutine(RestoreBrushAfterDelay(0.1f));
    }

    private IEnumerator RestoreBrushAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        brushColor = savedColor;
        currentBrush = savedBrushType;
        isAdjustingBrush = false;
        HighlightActiveBrush((int)currentBrush);
    }


    // =========================================================================
    // PRIVATE UTILITIES
    // =========================================================================

    string GetObjectNameFromIndex(int index)
    {
        switch (index)
        {
            case 1: return "Apple";
            case 2: return "Banana";
            case 3: return "Cherry";
            case 4: return "Grappes";
            case 5: return "Mango";
            case 6: return "Orange";
            case 7: return "Pear";
            case 8: return "Gauva";
            case 9: return "Strawberry";
            case 10: return "Pineapple";
            case 11: return "brocolli";
            case 12: return "Capsicum";
            case 13: return "Carrot";
            case 14: return "Chilli";
            case 15: return "Corn";
            case 16: return "EggPlant";
            case 17: return "Lemon";
            case 18: return "Onion";
            case 19: return "Pea";
            case 20: return "Tomato";
            case 21: return "Crab";
            case 22: return "Fish";
            case 23: return "JellyFish";
            case 24: return "Octupus";
            case 25: return "Shark";
            case 26: return "SmallFIsh";
            case 27: return "StarFish";
            case 28: return "StingRay";
            case 29: return "Turtle";
            case 30: return "Whale";
            case 31: return "Cat";
            case 32: return "Deer";
            case 33: return "Dog";
            case 34: return "Elephant";
            case 35: return "graph";
            case 36: return "Lion";
            case 37: return "Monkey";
            case 38: return "Rabbit";
            case 39: return "Sheep";
            case 40: return "Tiger";
            default: return "";
        }
    }

    private void HighlightActiveBrush(int activeIndex)
    {
        for (int i = 0; i < brushButtons.Length; i++)
        {
            if (brushButtons[i] == null) continue;
            float targetX = (i == activeIndex) ? activeOffset : 0f;
            StopCoroutine("SlideButton");
            StartCoroutine(SlideButton(brushButtons[i], targetX));
        }
    }

    private IEnumerator CaptureFullScreenScreenshot()
    {
        yield return new WaitForEndOfFrame();

        var screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        byte[] bytes = screenTex.EncodeToPNG();
        Destroy(screenTex);

        string filename = "Mode3_Screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

#if UNITY_EDITOR || UNITY_STANDALONE
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string filePath = Path.Combine(desktopPath, filename);
        File.WriteAllBytes(filePath, bytes);
        Debug.Log("✅ Screenshot saved: " + filePath);

#elif UNITY_ANDROID
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, bytes);

        using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
        using (AndroidJavaClass unityPlayer  = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            mediaScanner.CallStatic("scanFile", context, new string[] { path }, null, null);
        }
        Debug.Log("✅ Screenshot saved to Gallery: " + path);

#elif UNITY_IOS
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, bytes);
        Debug.Log("✅ Screenshot saved: " + path);
#endif
    }

    private IEnumerator SlideButton(RectTransform btn, float targetX)
    {
        Vector2 startPos = btn.anchoredPosition;
        Vector2 endPos = new Vector2(targetX, startPos.y);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.2f;
            btn.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        btn.anchoredPosition = endPos;
    }
}
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class DrawingManager : MonoBehaviour
{
    public enum BrushType
    {
        None,
        Solid,
        Crayon,
        Marker,
        Spray,
        Pencil
    }

    [Header("UI Elements")]
    public Slider brushSizeSlider;

    [Header("Brush Settings")]
    public Color brushColor = Color.black;
    public BrushType currentBrush = BrushType.Solid;

    // --- OPTIMIZATION FIELDS ---
    private Color[] drawingPixels;
    private bool hasDrawnThisFrame = false;
    // ---------------------------

    private RawImage drawingArea;
    private PolygonCollider2D drawingBoundaryCollider;
    private Texture2D drawingTexture;
    private RectTransform drawingRect;

    // FIX PIXELATION: Store the ORIGINAL source texture per image index so we
    // never downscale an already-downscaled texture on re-entry.
    private static Dictionary<int, Texture2D> originalSourceTextures = new Dictionary<int, Texture2D>();

    // FIX #1: Cache boundary mask as a flat bool array instead of calling
    // GetPixel() (GPU readback) inside every brush loop — massive perf gain.
    private bool[] boundaryMaskCache;
    private int boundaryMaskWidth;
    private int boundaryMaskHeight;

    private Vector2? lastDrawPos = null;
    private Color savedColor;
    private bool isAdjustingBrush = false;
    private int textureScale = 1;

    private float lastDrawTime = 0f;
    private float drawInterval = 0.016f;

    // FIX #8: Track which input source is currently active so mouse and touch
    // cannot both fire drawing in the same frame.
    private bool touchActiveThisFrame = false;

    public DrawingPanelSoundManager soundManager;

    [Header("UI – Brush Buttons")]
    public RectTransform[] brushButtons;
    [SerializeField] private float activeOffset = -20f;

    // ── UNDO SYSTEM ──────────────────────────────────────────────────────
    private struct PixelChange
    {
        public int index;
        public Color previousColor;
    }

    private Stack<List<PixelChange>> undoStack = new Stack<List<PixelChange>>();
    private Dictionary<int, Color> currentStrokeSnapshot = new Dictionary<int, Color>();
    private bool isDrawingStroke = false;
    // ─────────────────────────────────────────────────────────────────────

    // Bump when binary save format changes
    private const int SAVE_VERSION = 1;

    // FIX #5: Track whether the boundary mask is ready before allowing draws.
    private bool boundaryMaskReady = false;


    void OnEnable()
    {
        StartCoroutine(InitializeDrawingDelayed());
    }

    private IEnumerator InitializeDrawingDelayed()
    {
        yield return null;

        if (drawingRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(drawingRect);

        InitializeDrawing();
    }

    // =========================================================================
    // PUBLIC RESET — called by DrawingButtonManager when switching images
    // =========================================================================
    public void ResetDrawingProperly()
    {
        // Commit any in-progress stroke before tearing down
        if (isDrawingStroke) CommitStroke();

        // Clean up the previous drawing texture to avoid memory leak
        if (drawingTexture != null)
        {
            Destroy(drawingTexture);
            drawingTexture = null;
        }

        boundaryMaskReady = false;
        undoStack.Clear();
        currentStrokeSnapshot.Clear();
        isDrawingStroke = false;
        lastDrawPos = null;

        Debug.Log("[DrawingManager] ResetDrawingProperly called — re-initialising.");
        StartCoroutine(InitializeDrawingDelayed());
    }


    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    void InitializeDrawing()
    {
        if (Application.isMobilePlatform)
        {
            textureScale = 2;
            brushSizeSlider.maxValue = 10f;
        }
        else
        {
            brushSizeSlider.maxValue = 20f;
        }

        int selectedImageIndex = PlayerPrefs.GetInt("DrawingImage", 0);
        if (selectedImageIndex <= 0)
        {
            Debug.LogError("[DrawingManager] Invalid or unset DrawingImage PlayerPref.");
            return;
        }

        string objectName = GetObjectNameFromIndex(selectedImageIndex);
        GameObject activeFruit = GameObject.Find(objectName);
        if (activeFruit == null)
        {
            Debug.LogError("[DrawingManager] No GameObject found with name: " + objectName);
            return;
        }

        drawingArea = activeFruit.GetComponentInChildren<RawImage>();
        drawingBoundaryCollider = activeFruit.GetComponentInChildren<PolygonCollider2D>();
        drawingRect = drawingArea?.rectTransform;

        if (drawingArea == null || drawingBoundaryCollider == null)
        {
            Debug.LogError("[DrawingManager] RawImage or PolygonCollider2D missing on " + objectName);
            return;
        }

        // ── FIX PIXELATION: Cache the ORIGINAL source texture the first time ──
        // On every subsequent visit, use the cached original — NEVER the already-
        // modified drawingArea.texture (which would be our own drawingTexture
        // from the last session, causing progressive quality loss).
        Texture2D sourceTexture;
        if (!originalSourceTextures.ContainsKey(selectedImageIndex))
        {
            sourceTexture = drawingArea.texture as Texture2D;
            if (sourceTexture == null)
            {
                Debug.LogError("[DrawingManager] Source texture is missing or not a Texture2D!");
                return;
            }
            // Deep-copy the original pixels so the asset is never modified
            Texture2D pristineCopy = new Texture2D(sourceTexture.width, sourceTexture.height,
                                                    TextureFormat.RGBA32, false);
            pristineCopy.SetPixels(sourceTexture.GetPixels());
            pristineCopy.Apply();
            originalSourceTextures[selectedImageIndex] = pristineCopy;
            Debug.Log($"[DrawingManager] PIXELATION FIX — Cached original source texture for index {selectedImageIndex} " +
                      $"({sourceTexture.width}x{sourceTexture.height})");
        }
        else
        {
            Debug.Log($"[DrawingManager] PIXELATION FIX — Using cached original source texture for index {selectedImageIndex}, " +
                      $"avoiding re-downscale on re-entry.");
        }

        sourceTexture = originalSourceTextures[selectedImageIndex];
        // ─────────────────────────────────────────────────────────────────────

        // FIX #6: drawingTexture dimensions divided by textureScale from the
        // ORIGINAL resolution — never from a previously scaled texture.
        int texW = sourceTexture.width / textureScale;
        int texH = sourceTexture.height / textureScale;

        Debug.Log($"[DrawingManager] Creating drawingTexture at {texW}x{texH} (scale={textureScale}) " +
                  $"from original {sourceTexture.width}x{sourceTexture.height}");

        drawingTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false);

        // Scale source pixels down to match drawingTexture resolution.
        if (textureScale == 1)
        {
            drawingPixels = sourceTexture.GetPixels();
        }
        else
        {
            // Use a temporary RenderTexture to blit-scale the source.
            RenderTexture rt = RenderTexture.GetTemporary(texW, texH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sourceTexture, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            drawingTexture.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            drawingPixels = drawingTexture.GetPixels();
            Debug.Log($"[DrawingManager] PIXELATION FIX — Blit-scaled from original to {texW}x{texH} successfully.");
        }

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();
        drawingArea.texture = drawingTexture;

        undoStack.Clear();
        currentStrokeSnapshot.Clear();
        isDrawingStroke = false;
        boundaryMaskReady = false;

        // FIX #5: Load saved state AFTER the boundary mask is built so that
        // restored pixels are not drawn before the mask is ready.
        StartCoroutine(RebuildBoundaryMaskThenLoad(selectedImageIndex));

        HighlightActiveBrush((int)currentBrush - 1);
    }

    // FIX #5: Combined coroutine — build mask first, then load saved state.
    private IEnumerator RebuildBoundaryMaskThenLoad(int imageIndex)
    {
        yield return null;
        drawingBoundaryCollider.enabled = false;
        drawingBoundaryCollider.enabled = true;
        CreateBoundaryMask();
        boundaryMaskReady = true;
        Debug.Log("[DrawingManager] Boundary mask ready — loading saved state.");

        LoadDrawingState(imageIndex);
    }

    void CreateBoundaryMask()
    {
        int w = drawingTexture.width;
        int h = drawingTexture.height;
        boundaryMaskWidth = w;
        boundaryMaskHeight = h;

        // FIX #1: Store mask as a flat bool[] so brushes can do a simple array
        // lookup instead of an expensive GetPixel GPU readback each pixel.
        boundaryMaskCache = new bool[w * h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector2 worldPoint = PixelToWorld(x, y);
                boundaryMaskCache[y * w + x] = drawingBoundaryCollider.OverlapPoint(worldPoint);
            }
        }
    }

    // FIX #1: Helper — replaces every boundaryMask.GetPixel() call.
    private bool IsInsideBoundary(int px, int py)
    {
        if (boundaryMaskCache == null) return false;
        px = Mathf.Clamp(px, 0, boundaryMaskWidth - 1);
        py = Mathf.Clamp(py, 0, boundaryMaskHeight - 1);
        return boundaryMaskCache[py * boundaryMaskWidth + px];
    }

    string GetObjectNameFromIndex(int index)
    {
        string[] names = {
            "", "Apple","Banana","Cherry","Grappes","Mango","Orange","Pear","Gauva","Strawberry",
            "Pineapple","Brocolli","Capsicum","Carrot","Chilli","Corn","EggPlant","Lemon","Onion","Pea","Tomato",
            "Crab","Fish","JellyFish","Octupus","Shark","SmallFIsh","StarFish","StingRay","Turtle","Whale",
            "Cat","Deer","Dog","Elephant","graph","Lion","Monkey","Rabbit","Sheep","Tiger"
        };
        return (index >= 1 && index < names.Length) ? names[index] : "";
    }


    // =========================================================================
    // DRAWING LOOP
    // =========================================================================

    void Update()
    {
        // FIX #5: Don't process drawing until the boundary mask is ready.
        if (!boundaryMaskReady) return;

        if (Time.time - lastDrawTime < drawInterval) return;
        lastDrawTime = Time.time;

        // FIX #8: Reset touch-active flag each frame so mouse can fire only
        // when no touch input is being processed.
        touchActiveThisFrame = false;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved ||
                touch.phase == TouchPhase.Stationary ||
                touch.phase == TouchPhase.Began)
            {
                touchActiveThisFrame = true;
                HandleDrawing(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended ||
                     touch.phase == TouchPhase.Canceled)
            {
                touchActiveThisFrame = true;
                CommitStroke();
                lastDrawPos = null;
                if (soundManager != null) soundManager.StopBrushingSound();
            }
        }

        // FIX #8: Only handle mouse if no touch was processed this frame.
        if (!touchActiveThisFrame && Input.GetMouseButton(0))
            HandleDrawing(Input.mousePosition);
    }

    void LateUpdate()
    {
        if (hasDrawnThisFrame)
        {
            drawingTexture.SetPixels(drawingPixels);
            drawingTexture.Apply();
            hasDrawnThisFrame = false;
        }

        // FIX #8: Only commit on mouse-up when no touch is active.
        if (!touchActiveThisFrame && Input.GetMouseButtonUp(0) && lastDrawPos != null)
        {
            CommitStroke();
            lastDrawPos = null;
            if (soundManager != null) soundManager.StopBrushingSound();
        }
    }

    void HandleDrawing(Vector2 screenPos)
    {
        // FIX #4: Block all drawing while the brush-size slider is being dragged
        // so the preview strokes don't land on the canvas or pollute the undo stack.
        if (isAdjustingBrush) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                drawingRect, screenPos, null, out Vector2 localPos))
        {
            if (drawingRect.rect.Contains(localPos))
            {
                hasDrawnThisFrame = true;

                if (!isDrawingStroke)
                {
                    currentStrokeSnapshot.Clear();
                    isDrawingStroke = true;
                }

                if (soundManager != null) soundManager.PlayBrushingSound();

                Vector2 curr = localPos;

                if (lastDrawPos == null)
                {
                    DrawCircleAt(curr);
                }
                else
                {
                    Vector2 prev = lastDrawPos.Value;
                    float distance = Vector2.Distance(prev, curr);
                    int steps = Mathf.Min(Mathf.CeilToInt(distance * 2), 10);

                    for (int i = 0; i <= steps; i++)
                    {
                        Vector2 lerped = Vector2.Lerp(prev, curr, i / (float)steps);
                        DrawCircleAt(lerped);
                    }
                }

                lastDrawPos = curr;
            }
            else
            {
                if (lastDrawPos != null)
                {
                    if (soundManager != null) soundManager.StopBrushingSound();
                    CommitStroke();
                    lastDrawPos = null;
                }
            }
        }
    }

    void OnDisable()
    {
        if (isDrawingStroke) CommitStroke();
        if (soundManager != null) soundManager.StopBrushingSound();
    }

    // ── Commit stroke → push to undo stack → auto-save ───────────────────
    private void CommitStroke()
    {
        if (!isDrawingStroke || currentStrokeSnapshot.Count == 0)
        {
            isDrawingStroke = false;
            return;
        }

        // FIX #7: Skip saving strokes that touched fewer than 5 pixels so that
        // accidental taps don't bloat the undo stack and save file.
        if (currentStrokeSnapshot.Count < 5)
        {
            currentStrokeSnapshot.Clear();
            isDrawingStroke = false;
            return;
        }

        List<PixelChange> changes = new List<PixelChange>(currentStrokeSnapshot.Count);
        foreach (var kv in currentStrokeSnapshot)
            changes.Add(new PixelChange { index = kv.Key, previousColor = kv.Value });

        undoStack.Push(changes);
        currentStrokeSnapshot.Clear();
        isDrawingStroke = false;

        SaveDrawingState(PlayerPrefs.GetInt("DrawingImage", 0));

        Debug.Log($"Stroke committed ✅ | {changes.Count} pixels | Undo steps: {undoStack.Count}");
    }


    // =========================================================================
    // BRUSH IMPLEMENTATIONS
    // =========================================================================

    void DrawCircleAt(Vector2 localPos)
    {
        float normalizedX = (localPos.x + drawingRect.rect.width / 2f) / drawingRect.rect.width;
        float normalizedY = (localPos.y + drawingRect.rect.height / 2f) / drawingRect.rect.height;

        int centerX = Mathf.RoundToInt(normalizedX * drawingTexture.width);
        int centerY = Mathf.RoundToInt(normalizedY * drawingTexture.height);
        int radius = Mathf.RoundToInt(
            brushSizeSlider.value * (Application.isMobilePlatform ? 0.5f : 1f));

        switch (currentBrush)
        {
            case BrushType.None: return;
            case BrushType.Solid: DrawSolidBrush(centerX, centerY, radius); return;
            case BrushType.Crayon: DrawCrayonBrush(centerX, centerY, radius); return;
            case BrushType.Marker: DrawMarkerBrush(centerX, centerY, radius); return;
            case BrushType.Pencil: DrawPencilBrush(centerX, centerY, radius); return;
            case BrushType.Spray: DrawSprayBrush(centerX, centerY, radius); return;
        }
    }

    int GetIndex(int x, int y) => y * drawingTexture.width + x;

    private void SnapshotPixel(int index)
    {
        if (!currentStrokeSnapshot.ContainsKey(index))
            currentStrokeSnapshot[index] = drawingPixels[index];
    }

    void DrawSolidBrush(int centerX, int centerY, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radius * radius) continue;
                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                if (!IsInsideBoundary(px, py)) continue;
                int index = GetIndex(px, py);
                SnapshotPixel(index);
                drawingPixels[index] = brushColor;
            }
    }

    void DrawCrayonBrush(int centerX, int centerY, int radius)
    {
        float baseOpacity = 0.40f;
        float noiseChance = 0.45f;
        float sparkleChance = 0.08f;

        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                float distSq = x * x + y * y;
                if (distSq > radius * radius) continue;
                if (Random.value > noiseChance) continue;
                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                if (!IsInsideBoundary(px, py)) continue;
                int index = GetIndex(px, py);
                SnapshotPixel(index);
                Color existing = drawingPixels[index];
                float opacity = baseOpacity + Random.Range(-0.1f, 0.1f);
                Color finalColor = Color.Lerp(existing, brushColor, opacity);
                Color.RGBToHSV(finalColor, out float h, out float s, out float v);
                v = Mathf.Clamp01(v + Random.Range(-0.04f, 0.04f));
                finalColor = Color.HSVToRGB(h, s, v);
                if (Random.value < sparkleChance)
                    finalColor = Color.Lerp(finalColor, Color.white, Random.Range(0.5f, 0.9f));
                drawingPixels[index] = finalColor;
            }
    }

    void DrawMarkerBrush(int centerX, int centerY, int radius)
    {
        float markerOpacity = 0.35f;
        float softEdgeAmount = 0.4f;

        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                float distSq = x * x + y * y;
                if (distSq > radius * radius) continue;
                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                if (!IsInsideBoundary(px, py)) continue;
                int index = GetIndex(px, py);
                SnapshotPixel(index);
                Color existing = drawingPixels[index];
                float blendFactor = markerOpacity;
                float distance = Mathf.Sqrt(distSq);
                if (distance > radius * (1f - softEdgeAmount))
                    blendFactor *= 1f - ((distance - radius * (1f - softEdgeAmount)) / (radius * softEdgeAmount));
                drawingPixels[index] = Color.Lerp(existing, brushColor, Mathf.Clamp01(blendFactor));
            }
    }

    void DrawPencilBrush(int centerX, int centerY, int radius)
    {
        float pencilOpacity = 0.18f;
        float skipChance = 0.25f;

        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                float distSq = x * x + y * y;
                if (distSq > radius * radius) continue;
                if (Random.value < skipChance) continue;
                int px = Mathf.Clamp(centerX + x, 0, drawingTexture.width - 1);
                int py = Mathf.Clamp(centerY + y, 0, drawingTexture.height - 1);
                if (!IsInsideBoundary(px, py)) continue;
                int index = GetIndex(px, py);
                SnapshotPixel(index);
                Color existing = drawingPixels[index];
                float falloff = Mathf.SmoothStep(1f, 0.3f, Mathf.Sqrt(distSq) / radius);
                drawingPixels[index] = Color.Lerp(existing, brushColor, pencilOpacity * falloff);
            }
    }

    void DrawSprayBrush(int centerX, int centerY, int radius)
    {
        int sprayCount = radius * (Application.isMobilePlatform ? 6 : 10);
        float baseOpacity = 0.2f;

        for (int i = 0; i < sprayCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            float dist = offset.magnitude / radius;
            float falloff = Mathf.SmoothStep(1f, 0f, dist);
            int px = Mathf.Clamp(centerX + Mathf.RoundToInt(offset.x), 0, drawingTexture.width - 1);
            int py = Mathf.Clamp(centerY + Mathf.RoundToInt(offset.y), 0, drawingTexture.height - 1);
            if (!IsInsideBoundary(px, py)) continue;
            int index = GetIndex(px, py);
            SnapshotPixel(index);
            drawingPixels[index] = Color.Lerp(drawingPixels[index], brushColor, baseOpacity * falloff);
        }
    }


    // =========================================================================
    // UNDO
    // =========================================================================

    public void UndoLastStroke()
    {
        if (isDrawingStroke) CommitStroke();

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

        SaveDrawingState(PlayerPrefs.GetInt("DrawingImage", 0));

        Debug.Log($"Undo ✅ | Restored {changes.Count} pixels | Remaining steps: {undoStack.Count}");
    }


    // =========================================================================
    // SAVE / LOAD
    // =========================================================================

    private string GetSavePath(int imageIndex)
        => Path.Combine(Application.persistentDataPath, $"drawing_save_{imageIndex}.dat");

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

                List<PixelChange>[] entries = new List<PixelChange>[undoStack.Count];
                undoStack.CopyTo(entries, 0);
                System.Array.Reverse(entries);

                bw.Write(entries.Length);
                foreach (List<PixelChange> entry in entries)
                {
                    bw.Write(entry.Count);
                    foreach (PixelChange pc in entry)
                    {
                        bw.Write(pc.index);
                        bw.Write(pc.previousColor.r); bw.Write(pc.previousColor.g);
                        bw.Write(pc.previousColor.b); bw.Write(pc.previousColor.a);
                    }
                }
            }

            Debug.Log($"[DrawingManager] Saved ✅  undo steps: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DrawingManager] Save failed: {ex.Message}");
        }
    }

    private void LoadDrawingState(int imageIndex)
    {
        string path = GetSavePath(imageIndex);

        if (!File.Exists(path))
        {
            Debug.Log("[DrawingManager] No save file — starting fresh.");
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
                    Debug.LogWarning($"[DrawingManager] Save version mismatch ({version} vs {SAVE_VERSION}) — deleting old save.");
                    fs.Close();
                    File.Delete(path);
                    return;
                }

                int savedW = br.ReadInt32();
                int savedH = br.ReadInt32();
                int pixelCount = br.ReadInt32();

                if (savedW != drawingTexture.width || savedH != drawingTexture.height)
                {
                    Debug.LogWarning($"[DrawingManager] PIXELATION FIX — Texture size mismatch " +
                                     $"(saved {savedW}x{savedH} vs current {drawingTexture.width}x{drawingTexture.height}) " +
                                     $"— ignoring old save to prevent blur.");
                    return;
                }

                for (int i = 0; i < pixelCount; i++)
                    drawingPixels[i] = new Color(
                        br.ReadSingle(), br.ReadSingle(),
                        br.ReadSingle(), br.ReadSingle());

                drawingTexture.SetPixels(drawingPixels);
                drawingTexture.Apply(false);

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

            Debug.Log($"[DrawingManager] Loaded ✅  undo steps: {undoStack.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DrawingManager] Load failed: {ex.Message}");
        }
    }

    public void DeleteSavedProgress()
    {
        int idx = PlayerPrefs.GetInt("DrawingImage", 0);
        if (idx <= 0) return;
        string path = GetSavePath(idx);
        if (File.Exists(path)) { File.Delete(path); Debug.Log($"[DrawingManager] Save deleted: {path}"); }
    }


    // =========================================================================
    // UTILITY METHODS
    // =========================================================================

    Vector2 PixelToWorld(int px, int py)
    {
        float uiX = ((float)px / drawingTexture.width) * drawingRect.rect.width - drawingRect.rect.width / 2f;
        float uiY = ((float)py / drawingTexture.height) * drawingRect.rect.height - drawingRect.rect.height / 2f;
        return drawingRect.TransformPoint(new Vector2(uiX, uiY));
    }

    public void SetBrushColor(Color color) => brushColor = color;

    public void SetBrushToNone()
    {
        currentBrush = BrushType.None;
        if (soundManager != null)
        {
            soundManager.allowBrushingSound = false;
            soundManager.StopBrushingSound();
        }
        HighlightActiveBrush(-1);
    }

    // FIX #3: UseEraser now ensures a valid brush type is active and sound is
    // re-enabled so the eraser always works regardless of prior state.
    public void UseEraser()
    {
        brushColor = Color.white;
        if (currentBrush == BrushType.None)
            currentBrush = BrushType.Solid;
        if (soundManager != null)
            soundManager.allowBrushingSound = true;
    }

    public void ClearCanvas()
    {
        if (drawingArea == null) return;

        // FIX #9: Commit any in-progress stroke first so it doesn't corrupt
        // the clear's undo entry.
        if (isDrawingStroke) CommitStroke();

        List<PixelChange> changes = new List<PixelChange>(drawingPixels.Length);
        for (int i = 0; i < drawingPixels.Length; i++)
        {
            if (drawingPixels[i] != Color.white)
            {
                changes.Add(new PixelChange { index = i, previousColor = drawingPixels[i] });
                drawingPixels[i] = Color.white;
            }
        }

        if (changes.Count > 0)
            undoStack.Push(changes);

        drawingTexture.SetPixels(drawingPixels);
        drawingTexture.Apply();

        SaveDrawingState(PlayerPrefs.GetInt("DrawingImage", 0));

        Debug.Log("✅ Canvas cleared");
    }


    // =========================================================================
    // BRUSH SIZE SLIDER
    // =========================================================================

    public void OnBrushSizeDragStart()
    {
        if (!isAdjustingBrush)
        {
            // FIX #4: Don't change brushColor — just set the flag. Drawing is
            // blocked in HandleDrawing() while isAdjustingBrush is true.
            isAdjustingBrush = true;
        }
    }

    public void OnBrushSizeDragEnd()
    {
        if (isAdjustingBrush)
            StartCoroutine(RestoreBrushStateAfterDelay(0.05f));
    }

    // FIX #4: Renamed and simplified — no color swap needed any more.
    private IEnumerator RestoreBrushStateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAdjustingBrush = false;
    }


    // =========================================================================
    // SCREENSHOT
    // =========================================================================

    public void SaveFullScreenScreenshot()
    {
        StartCoroutine(CaptureFullScreenScreenshot());
    }

    private IEnumerator CaptureFullScreenScreenshot()
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        byte[] bytes = screenTex.EncodeToPNG();
        Destroy(screenTex);

        string filename = "FullScreenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

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


    // =========================================================================
    // BRUSH SELECTORS
    // FIX #2: HighlightActiveBrush receives (brushIndex - 1) so that the
    // button array (0-based) aligns with BrushType (1-based).
    // =========================================================================

    public void SetBrushToSolid()
    {
        brushColor = Color.red;
        currentBrush = BrushType.Solid;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush((int)BrushType.Solid - 1);
    }

    public void SetBrushToCrayon()
    {
        brushColor = Color.cyan;
        currentBrush = BrushType.Crayon;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush((int)BrushType.Crayon - 1);
    }

    public void SetBrushToMarker()
    {
        brushColor = new Color(1f, 0.4f, 0.7f);
        currentBrush = BrushType.Marker;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush((int)BrushType.Marker - 1);
    }

    public void SetBrushToSpray()
    {
        brushColor = Color.green;
        currentBrush = BrushType.Spray;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush((int)BrushType.Spray - 1);
    }

    public void SetBrushToPencil()
    {
        brushColor = Color.yellow;
        currentBrush = BrushType.Pencil;
        if (soundManager != null) soundManager.allowBrushingSound = true;
        HighlightActiveBrush((int)BrushType.Pencil - 1);
    }


    // =========================================================================
    // UI HIGHLIGHT
    // =========================================================================

    private void HighlightActiveBrush(int activeIndex)
    {
        for (int i = 0; i < brushButtons.Length; i++)
        {
            if (brushButtons[i] == null) continue;
            float targetX = (i == activeIndex) ? activeOffset : 0f;
            StartCoroutine(SlideButton(brushButtons[i], targetX));
        }
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


    // =========================================================================
    // APP LIFECYCLE
    // =========================================================================

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (isDrawingStroke) CommitStroke();
            SaveDrawingState(PlayerPrefs.GetInt("DrawingImage", 0));
        }
    }

    void OnApplicationQuit()
    {
        if (isDrawingStroke) CommitStroke();
        SaveDrawingState(PlayerPrefs.GetInt("DrawingImage", 0));
    }
}
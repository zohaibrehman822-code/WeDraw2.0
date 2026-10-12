using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Guided drawing test. Attach to an "Ink" UI object (stretched over the BG).
/// The child can only draw along the path made by the children of "Points Parent".
/// Ink is drawn as a red line from the first point up to the current thumb position.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class GuidedDrawing : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Path")]
    [Tooltip("Drag your point objects here in order: P1 is the start. Takes priority over Points Parent.")]
    [SerializeField] Transform[] points;
    [Tooltip("Optional: if the array above is empty, the children of this object are used in order.")]
    [SerializeField] Transform pointsParent;

    [Header("Line")]
    [SerializeField] Color lineColor = Color.red;
    [SerializeField, Min(1f)] float thickness = 24f;

    [Header("Kids Guide Style")]
    [Tooltip("Shows a friendly dotted path, start/finish targets and touch feedback.")]
    [SerializeField] bool kidsGuideStyle;
    [SerializeField] Color guideColor = new Color(0.28f, 0.72f, 1f, 0.55f);
    [SerializeField] Color wrongTouchColor = new Color(1f, 0.3f, 0.28f, 0.8f);
    [SerializeField] Color startDotColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] Color finishDotColor = new Color(1f, 0.78f, 0.12f, 1f);
    [SerializeField, Min(4f)] float guideDotRadius = 7f;
    [SerializeField, Min(8f)] float guideDotSpacing = 26f;
    [SerializeField, Range(0.85f, 1f)] float completionThreshold = 0.97f;
    [SerializeField, Min(0f)] float wrongTouchFlashDuration = 0.3f;
    [SerializeField, Min(0f)] float thumbPulseAmount = 0.1f;
    [SerializeField, Min(0f)] float thumbPulseSpeed = 4f;

    [Header("Rules (canvas units)")]
    [Tooltip("How far the finger may stray from the path before ink stops.")]
    [SerializeField, Min(1f)] float tolerance = 50f;
    [Tooltip("How close the finger must be to the thumb to start / resume.")]
    [SerializeField, Min(1f)] float startRadius = 70f;
    [Tooltip("Max distance the thumb can jump ahead in one drag event.")]
    [SerializeField, Min(1f)] float lookAhead = 120f;

    [Header("Thumb")]
    [SerializeField] RectTransform thumb;
    [SerializeField] Vector2 thumbOffset;
    [Tooltip("How fast the thumb moves along the path (canvas units per second).")]
    [SerializeField, Min(1f)] float thumbSpeed = 250f;
    [Tooltip("How far ahead of the child's ink the thumb may go.")]
    [SerializeField, Min(0f)] float leadDistance = 120f;

    [Header("Events")]
    public UnityEvent onCompleted;

    readonly List<Vector2> path = new List<Vector2>();
    readonly List<float> cum = new List<float>();
    float total;
    float progress;      // how far the child has drawn
    float thumbArc;      // where the thumb is (ahead of progress while drawing)
    bool dragging;
    bool completed;
    float wrongTouchUntil;
    Vector3 thumbBaseScale = Vector3.one;

    // reused buffers and caches, so nothing is allocated while the child draws
    float placedArc = -1f;
    readonly List<Vector2> meshPts = new List<Vector2>();
    readonly List<Transform> pointBuffer = new List<Transform>();

    // ---------- setup ----------

    protected override void OnEnable()
    {
        base.OnEnable();
        if (thumb != null) thumbBaseScale = thumb.localScale;
        ResetDrawing();
    }

    protected override void OnDisable()
    {
        CancelInvoke(nameof(ClearWrongTouchFeedback));
        if (thumb != null) thumb.localScale = thumbBaseScale;
        base.OnDisable();
    }

    /// <summary>Clears the ink and puts the thumb back on the first point.</summary>
    public void ResetDrawing()
    {
        Canvas.ForceUpdateCanvases();
        BuildPath();
        progress = 0f;
        thumbArc = 0f;
        placedArc = -1f;
        dragging = false;
        completed = false;
        wrongTouchUntil = 0f;
        PlaceThumb();
        SetVerticesDirty();
    }

    void BuildPath()
    {
        path.Clear();
        cum.Clear();
        total = 0f;
        foreach (Transform t in GetPointTransforms())
        {
            if (t == null) continue;
            Vector3 l = rectTransform.InverseTransformPoint(t.position);
            path.Add(new Vector2(l.x, l.y));
        }

        cum.Add(0f);
        for (int i = 1; i < path.Count; i++)
        {
            total += Vector2.Distance(path[i - 1], path[i]);
            cum.Add(total);
        }
    }

    Vector2 PointAt(float s)
    {
        if (path.Count == 0) return Vector2.zero;
        if (s <= 0f || path.Count == 1) return path[0];
        for (int i = 1; i < path.Count; i++)
        {
            if (s <= cum[i])
            {
                float len = cum[i] - cum[i - 1];
                float t = len > 0f ? (s - cum[i - 1]) / len : 0f;
                return Vector2.Lerp(path[i - 1], path[i], t);
            }
        }
        return path[path.Count - 1];
    }

    void PlaceThumb()
    {
        if (thumb == null || path.Count == 0) return;
        thumb.position = rectTransform.TransformPoint(PointAt(thumbArc) + thumbOffset);
        placedArc = thumbArc;
    }

    void Update()
    {
        if (thumb == null || path.Count < 2) return;

        if (kidsGuideStyle && !completed)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * thumbPulseSpeed) * thumbPulseAmount;
            thumb.localScale = thumbBaseScale * pulse;
        }
        else
        {
            thumb.localScale = thumbBaseScale;
        }

        // While the child is drawing, the thumb leads ahead along the path.
        // When the child lifts the finger or leaves the path, it comes back
        // to the last drawn point to show where to touch again.
        float target = (dragging && !completed)
            ? Mathf.Min(total, progress + leadDistance)
            : progress;

        if (thumbArc < progress) thumbArc = progress;
        if (thumbArc != target)
            thumbArc = Mathf.MoveTowards(thumbArc, target, thumbSpeed * Time.deltaTime);

        // Only touch the thumb's transform when it really moved (keeps the canvas calm).
        if (thumbArc != placedArc) PlaceThumb();
    }

    bool ToLocal(PointerEventData e, out Vector2 p)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, e.position, e.pressEventCamera, out p);
    }

    // ---------- input ----------

    public void OnPointerDown(PointerEventData e)
    {
        if (completed) return;
        BuildPath();
        if (path.Count < 2) return;

        if (ToLocal(e, out Vector2 p) && Vector2.Distance(p, PointAt(progress)) <= startRadius)
            dragging = true;
        else
            ShowWrongTouchFeedback();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging || completed) return;
        if (!ToLocal(e, out Vector2 p)) return;

        float lo = progress;
        float hi = Mathf.Min(total, progress + lookAhead);
        float bestDist = float.MaxValue;
        float bestArc = progress;

        for (int i = 1; i < path.Count; i++)
        {
            float s0 = cum[i - 1], s1 = cum[i], len = s1 - s0;
            if (len < 0.0001f || s1 < lo || s0 > hi) continue;

            Vector2 a = path[i - 1], b = path[i];
            float tMin = Mathf.Max(0f, (lo - s0) / len);
            float tMax = Mathf.Min(1f, (hi - s0) / len);
            float t = Mathf.Clamp(Vector2.Dot(p - a, b - a) / (len * len), tMin, tMax);

            float d = Vector2.Distance(p, a + (b - a) * t);
            if (d < bestDist)
            {
                bestDist = d;
                bestArc = s0 + t * len;
            }
        }

        // Finger left the path: ink stops. Touch the thumb again to continue.
        if (bestDist > tolerance)
        {
            dragging = false;
            ShowWrongTouchFeedback();
            return;
        }

        if (bestArc > progress)
        {
            progress = bestArc;
            PlaceThumb();
            SetVerticesDirty();
        }

        if (total > 0f && progress >= total * completionThreshold)
        {
            progress = total;
            completed = true;
            dragging = false;
            if (thumb != null) thumb.localScale = thumbBaseScale;
            PlaceThumb();
            SetVerticesDirty();
            Debug.Log("Complete");
            onCompleted?.Invoke();
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        dragging = false;
    }

    // ---------- drawing the red line ----------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (path.Count < 2) return;

        if (kidsGuideStyle)
            DrawKidsGuide(vh);

        if (progress <= 0f) return;

        // path from first point up to current progress
        List<Vector2> pts = meshPts;
        pts.Clear();
        pts.Add(path[0]);
        for (int i = 1; i < path.Count; i++)
        {
            if (cum[i] <= progress) pts.Add(path[i]);
            else { pts.Add(PointAt(progress)); break; }
        }

        Color32 shadow = new Color(0f, 0f, 0f, 0.16f);
        Color32 c = lineColor;

        // A soft outline keeps the child's ink readable over light artwork.
        for (int i = 1; i < pts.Count; i++)
        {
            AddSegment(vh, pts[i - 1], pts[i], thickness + 7f, shadow);
            AddSegment(vh, pts[i - 1], pts[i], thickness, c);
        }

        // round joints and caps
        foreach (Vector2 pt in pts)
        {
            AddCircle(vh, pt, (thickness + 7f) * 0.5f, shadow);
            AddCircle(vh, pt, thickness * 0.5f, c);
        }
    }

    void DrawKidsGuide(VertexHelper vh)
    {
        Color32 dots = Time.unscaledTime < wrongTouchUntil ? wrongTouchColor : guideColor;
        for (float s = 0f; s <= total; s += guideDotSpacing)
            AddCircle(vh, PointAt(s), guideDotRadius, dots, 8);

        // Large, obvious targets communicate where to start and where to finish.
        AddCircle(vh, path[0], guideDotRadius * 1.9f, startDotColor, 14);
        AddCircle(vh, path[path.Count - 1], guideDotRadius * 1.9f, finishDotColor, 14);
        AddCircle(vh, path[0], guideDotRadius * 0.75f, Color.white, 12);
        AddCircle(vh, path[path.Count - 1], guideDotRadius * 0.75f, Color.white, 12);
    }

    void ShowWrongTouchFeedback()
    {
        if (!kidsGuideStyle) return;
        wrongTouchUntil = Time.unscaledTime + wrongTouchFlashDuration;
        SetVerticesDirty();
        CancelInvoke(nameof(ClearWrongTouchFeedback));
        Invoke(nameof(ClearWrongTouchFeedback), wrongTouchFlashDuration);
    }

    void ClearWrongTouchFeedback()
    {
        SetVerticesDirty();
    }

    static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < 1e-6f) return;
        d.Normalize();
        Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);

        int idx = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    static void AddCircle(VertexHelper vh, Vector2 centerPos, float radius, Color32 color, int segments = 10)
    {
        int center = vh.currentVertCount;
        vh.AddVert(centerPos, color, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vh.AddVert(centerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % segments);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    // Shows the path in the Scene view so you can see what you placed.
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        List<Transform> list = GetPointTransforms();
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i - 1] == null || list[i] == null) continue;
            Gizmos.DrawLine(list[i - 1].position, list[i].position);
        }
    }

    // Uses the dragged-in array if it has items, otherwise the children of Points Parent.
    List<Transform> GetPointTransforms()
    {
        List<Transform> list = pointBuffer;
        list.Clear();
        if (points != null && points.Length > 0)
        {
            list.AddRange(points);
        }
        else if (pointsParent != null)
        {
            for (int i = 0; i < pointsParent.childCount; i++) list.Add(pointsParent.GetChild(i));
        }
        return list;
    }
}

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

    // ---------- setup ----------

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetDrawing();
    }

    /// <summary>Clears the ink and puts the thumb back on the first point.</summary>
    public void ResetDrawing()
    {
        Canvas.ForceUpdateCanvases();
        BuildPath();
        progress = 0f;
        thumbArc = 0f;
        dragging = false;
        completed = false;
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
    }

    void Update()
    {
        if (thumb == null || path.Count < 2) return;

        // While the child is drawing, the thumb leads ahead along the path.
        // When the child lifts the finger or leaves the path, it comes back
        // to the last drawn point to show where to touch again.
        float target = (dragging && !completed)
            ? Mathf.Min(total, progress + leadDistance)
            : progress;

        if (thumbArc < progress) thumbArc = progress;
        thumbArc = Mathf.MoveTowards(thumbArc, target, thumbSpeed * Time.deltaTime);
        PlaceThumb();
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
            return;
        }

        if (bestArc > progress)
        {
            progress = bestArc;
            PlaceThumb();
            SetVerticesDirty();
        }

        if (progress >= total - 0.01f)
        {
            progress = total;
            completed = true;
            dragging = false;
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
        if (path.Count < 2 || progress <= 0f) return;

        // path from first point up to current progress
        var pts = new List<Vector2> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            if (cum[i] <= progress) pts.Add(path[i]);
            else { pts.Add(PointAt(progress)); break; }
        }

        Color32 c = lineColor;
        float h = thickness * 0.5f;

        // segments
        for (int i = 1; i < pts.Count; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i];
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) continue;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x) * h;

            int idx = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }

        // round joints and caps
        const int seg = 14;
        foreach (Vector2 pt in pts)
        {
            int center = vh.currentVertCount;
            vh.AddVert(pt, c, Vector2.zero);
            for (int k = 0; k < seg; k++)
            {
                float ang = k * Mathf.PI * 2f / seg;
                vh.AddVert(pt + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * h, c, Vector2.zero);
            }
            for (int k = 0; k < seg; k++)
                vh.AddTriangle(center, center + 1 + k, center + 1 + (k + 1) % seg);
        }
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
        var list = new List<Transform>();
        if (points != null && points.Length > 0)
        {
            list.AddRange(points);
        }
        else if (pointsParent != null)
        {
            foreach (Transform t in pointsParent) list.Add(t);
        }
        return list;
    }
}
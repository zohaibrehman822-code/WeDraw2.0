using UnityEngine;

/// <summary>
/// Creates the guide points (P1, P2, P3 ...) for you, placed on top of the BG image.
/// Attach to the "Points" object. Right-click the component header -> "Build Points".
/// Points are given as 0..1 of the BG image (x from left, y from bottom),
/// so they stay correct if you resize the BG. You can still drag any point afterwards.
/// </summary>
public class GuidedPathBuilder : MonoBehaviour
{
    [Tooltip("The BG image (RectTransform) the points are placed on.")]
    [SerializeField] RectTransform area;

    [Tooltip("x from left, y from bottom, both 0..1. Defaults are the digit 1 guide image.")]
    [SerializeField]
    Vector2[] normalizedPoints =
    {
        new Vector2(0.3809f, 0.6484f), // P1: start (green dot)
        new Vector2(0.5469f, 0.7949f), // P2: top of the stem
        new Vector2(0.5469f, 0.1895f), // P3: bottom of the stem
    };

    [ContextMenu("Build Points")]
    public void Build()
    {
        if (area == null) area = transform.parent as RectTransform;
        if (area == null)
        {
            Debug.LogWarning("GuidedPathBuilder: assign the BG RectTransform to 'Area'.");
            return;
        }

        // remove old points
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject old = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(old);
            else DestroyImmediate(old);
        }

        Rect r = area.rect;
        for (int i = 0; i < normalizedPoints.Length; i++)
        {
            Vector2 n = normalizedPoints[i];
            var go = new GameObject("P" + (i + 1), typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);

            Vector2 local = new Vector2(
                Mathf.Lerp(r.xMin, r.xMax, n.x),
                Mathf.Lerp(r.yMin, r.yMax, n.y));
            rt.position = area.TransformPoint(local);
        }
    }
}
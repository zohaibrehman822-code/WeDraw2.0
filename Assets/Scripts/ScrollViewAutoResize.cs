using UnityEngine;
using UnityEngine.UI;

public class ScrollViewAutoResize : MonoBehaviour
{
    [Header("References")]
    public RectTransform content;   // The Content inside ScrollView
    public float spacing = 10f;     // Space between items (if you have spacing)
    public bool includeInactive = false; // If true, counts inactive items too

    void Start()
    {
        ResizeContent();
    }

    /// <summary>
    /// Automatically resize the Content height to match children
    /// </summary>
    public void ResizeContent()
    {
        if (content == null) return;

        float totalHeight = 0f;

        // Loop through each child inside Content
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;

            if (child == null) continue;
            if (!includeInactive && !child.gameObject.activeSelf) continue;

            // Add child height
            totalHeight += child.sizeDelta.y;

            // Add spacing except for the last item
            if (i < content.childCount - 1)
                totalHeight += spacing;
        }

        // Apply new size to Content
        Vector2 newSize = content.sizeDelta;
        newSize.y = totalHeight;
        content.sizeDelta = newSize;
    }
}

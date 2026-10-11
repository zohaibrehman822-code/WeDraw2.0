using UnityEngine;
using UnityEngine.UI;

public class LoopingScrollView : MonoBehaviour
{
    [Header("References")]
    public ScrollRect scrollRect;       // Drag your ScrollRect here
    public RectTransform content;       // Drag Content of ScrollRect
    public RectTransform[] items;       // Drag all your color items here (buttons/images)

    [Header("Settings")]
    public float resetThreshold = 50f;  // How close to bottom before reset

    private float itemHeight;
    private int itemCount;

    void Start()
    {
        if (items.Length == 0) return;

        itemHeight = items[0].sizeDelta.y;
        itemCount = items.Length;
    }

    void Update()
    {
        if (scrollRect == null || content == null) return;

        // Check if user scrolled near bottom
        float contentBottom = content.anchoredPosition.y + scrollRect.viewport.rect.height;

        if (contentBottom >= content.sizeDelta.y - resetThreshold)
        {
            // Reset back to top
            content.anchoredPosition = Vector2.zero;
        }
    }
}

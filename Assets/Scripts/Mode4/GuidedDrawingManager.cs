using UnityEngine;

/// <summary>
/// Attach to a Manager object. Holds all your drawings (Digit-1, Letter-A, ...)
/// and opens the selected one while closing the others.
/// </summary>
public class GuidedDrawingManager : MonoBehaviour
{
    [Tooltip("Drag the root of each drawing here (Digit-1, Letter-A, ...). The order is the index.")]
    [SerializeField] GameObject[] drawings;

    [Tooltip("Drawing to open when the game starts. -1 = open none.")]
    [SerializeField] int openOnStart = -1;

    int currentIndex = -1;

    public int CurrentIndex => currentIndex;
    public int Count => drawings != null ? drawings.Length : 0;

    void Start()
    {
        if (openOnStart >= 0) OpenDrawing(openOnStart);
        else CloseAll();
    }

    /// <summary>Opens the drawing at this index (0 = first in the array) and closes all others.</summary>
    public void OpenDrawing(int index)
    {
        if (drawings == null || index < 0 || index >= drawings.Length)
        {
            Debug.LogWarning($"GuidedDrawingManager: index {index} is out of range (0 to {Count - 1}).", this);
            return;
        }

        for (int i = 0; i < drawings.Length; i++)
        {
            if (drawings[i] == null) continue;
            drawings[i].SetActive(i == index);
        }

        currentIndex = index;
    }

    /// <summary>Opens the next drawing. After the last one it goes back to the first.</summary>
    public void OpenNext()
    {
        if (Count == 0) return;
        OpenDrawing((currentIndex + 1) % Count);
    }

    /// <summary>Closes every drawing.</summary>
    public void CloseAll()
    {
        if (drawings == null) return;
        foreach (GameObject d in drawings)
            if (d != null) d.SetActive(false);
        currentIndex = -1;
    }
}
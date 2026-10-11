using UnityEngine;
using UnityEngine.UI;

public class ColorButton : MonoBehaviour
{
    public DrawingManager drawingManager;

    void Start()
    {
        // FIX #11: Guard against a missing drawingManager reference so the
        // button does not throw a NullReferenceException when clicked.
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (drawingManager == null)
            {
                Debug.LogWarning("[ColorButton] DrawingManager reference is not assigned.");
                return;
            }
            drawingManager.SetBrushColor(GetComponent<Image>().color);
        });
    }
}
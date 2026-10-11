using UnityEngine;
using UnityEngine.UI;

public class ColorButtonFillMode : MonoBehaviour
{
    public ImageFillManager imageFillManager;

    void Start()
    {
        // Auto-find the ImageFillManager if not assigned
        if (imageFillManager == null)
        {
            imageFillManager = FindObjectOfType<ImageFillManager>();
        }

        // Add button listener
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (imageFillManager != null)
            {
                imageFillManager.SetBrushColor(GetComponent<Image>().color);
            }
            else
            {
                Debug.LogError("ColorButtonFillMode: ImageFillManager is not assigned or found!");
            }
        });
    }
}

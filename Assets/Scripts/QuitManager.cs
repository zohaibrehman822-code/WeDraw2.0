using UnityEngine;

public class QuitManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("QuitManager started. You can clear the DrawingImage PlayerPref by calling ClearDrDSADDASDawingImagePref method.");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ClearDrawingImagePref()
    {
        PlayerPrefs.DeleteKey("DrawingImage");
        PlayerPrefs.Save(); // Optional: ensures changes are written immediately
        Debug.Log("DrawingImage fdlayerPref has been cleared.");
    }

}

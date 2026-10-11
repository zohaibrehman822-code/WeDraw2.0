using UnityEngine;

public class DrawingButtonManager : MonoBehaviour
{
    // FIX #12: Cache the ReferenceImageManager reference instead of calling
    // FindObjectOfType on every button click, which is expensive and fragile.
    private ReferenceImageManager referenceImageManager;

    void Start()
    {
        referenceImageManager = FindObjectOfType<ReferenceImageManager>();
        if (referenceImageManager == null)
            Debug.LogWarning("[DrawingButtonManager] ReferenceImageManager not found in scene.");

        int selectedImage = PlayerPrefs.GetInt("DrawingImage", -1);
        Debug.Log($"[DrawingButtonManager] Start: Current DrawingImage = {selectedImage}");
    }

    public void OnDrawingButtonClicked(int imageIndex)
    {
        PlayerPrefs.SetInt("DrawingImage", imageIndex);
        PlayerPrefs.Save();
        Debug.Log($"[DrawingButtonManager] Button clicked, DrawingImage set to index: {imageIndex}");
        UpdateReferenceImage();
    }

    private void UpdateReferenceImage()
    {
        if (referenceImageManager != null)
        {
            referenceImageManager.UpdateImageFromPrefs();
        }
        else
        {
            // FIX #12: Try to find it lazily in case it was added to the scene
            // after this manager's Start() ran (e.g. loaded from another scene).
            referenceImageManager = FindObjectOfType<ReferenceImageManager>();
            if (referenceImageManager != null)
                referenceImageManager.UpdateImageFromPrefs();
            else
                Debug.LogWarning("[DrawingButtonManager] ReferenceImageManager not found in scene.");
        }
    }
}
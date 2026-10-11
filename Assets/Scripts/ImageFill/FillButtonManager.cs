using UnityEngine;

public class FillButtonManager : MonoBehaviour
{
    // Attach this to each button in the Inspector with a different index (1 to 40)
    public void OnFillButtonClicked(int imageIndex)
    {
        PlayerPrefs.SetInt("FillImage", imageIndex);
        PlayerPrefs.Save();

        Debug.Log($"[FillButtonManager] Button clicked, FillImage set to index: {imageIndex}");
        UpdateReferenceImage();
    }

    private void UpdateReferenceImage()
    {
        ReferenceFillImageManager manager = FindObjectOfType<ReferenceFillImageManager>();
        if (manager != null)
        {
            manager.UpdateImageFromPrefs();
        }
        else
        {
            Debug.LogWarning("[FillButtonManager] ReferenceFillImageManager not found in scene.");
        }
    }

    void Start()
    {
        int selectedImage = PlayerPrefs.GetInt("FillImage", -1);
        Debug.Log($"[FillButtonManager] Start: Current FillImage = {selectedImage}");
        Debug.Log("..............");
    }
}

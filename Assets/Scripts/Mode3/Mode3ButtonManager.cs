using UnityEngine;

public class Mode3ButtonManager : MonoBehaviour
{
    // Attach this to each button in the Inspector with a different index (1 to 40)
    public void OnMode3ButtonClicked(int imageIndex)
    {
        PlayerPrefs.SetInt("Mode3Image", imageIndex);
        PlayerPrefs.Save();

        Debug.Log($"[FillButtonManager] Button clicked, FillImage set to index: {imageIndex}");
        UpdateReferenceImage();
    }

    private void UpdateReferenceImage()
    {
        RefernceMode3ImageManager manager = FindObjectOfType<RefernceMode3ImageManager>();
        if (manager != null)
        {
            manager.UpdateImageFromPrefs();
        }
        else
        {
            Debug.LogWarning("[FillButtonManager] RefernceMode3ImageManager not found in scene.");
        }
    }

    void Start()
    {
        int selectedImage = PlayerPrefs.GetInt("Mode3Image", -1);
        Debug.Log($"[FillButtonManager] Start: Current FillImage = {selectedImage}");
        Debug.Log("*****");
    }
}
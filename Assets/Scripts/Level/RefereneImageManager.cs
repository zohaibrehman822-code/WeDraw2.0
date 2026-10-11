using UnityEngine;

public class ReferenceImageManager : MonoBehaviour
{
    [Header("Assign Fruit/Animal GameObjects in Order")]
    public GameObject[] referenceImages;

    void Start()
    {
        Debug.Log("[ReferenceImageManager] Started.");
        UpdateImageFromPrefs();
    }

    public void UpdateImageFromPrefs()
    {
        int selectedImage = PlayerPrefs.GetInt("DrawingImage", -1);
        Debug.Log($"[ReferenceImageManager] UpdateImageFromPrefs: DrawingImage = {selectedImage}");

        if (selectedImage >= 1 && selectedImage <= referenceImages.Length)
        {
            ActivateImageByIndex(selectedImage - 1); // Adjust for 0-based array index
        }
        else
        {
            Debug.LogWarning("[ReferenceImageManager] Invalid or unset DrawingImage PlayerPref. Deactivating all images.");
            DeactivateAllImages();
        }
    }

    private void ActivateImageByIndex(int index)
    {
        if (referenceImages == null) return;

        for (int i = 0; i < referenceImages.Length; i++)
        {
            if (referenceImages[i] != null)
                referenceImages[i].SetActive(i == index);
        }

        Debug.Log($"[ReferenceImageManager] Activated image GameObject at index: {index}");
    }

    private void DeactivateAllImages()
    {
        if (referenceImages == null) return;

        foreach (GameObject img in referenceImages)
        {
            if (img != null)
                img.SetActive(false);
        }

        Debug.Log("[ReferenceImageManager] All reference images deactivated.");
    }
}

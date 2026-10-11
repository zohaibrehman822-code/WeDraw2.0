using UnityEngine;

public class ReferenceFillImageManager : MonoBehaviour
{
    [Header("Assign Fruit/Animal GameObjects in Order")]
    public GameObject[] referenceFillImages;

    // Store a reference to the ImageFillManager for efficiency
    private ImageFillManager imageFillManager;

    void Start()
    {
        Debug.Log("[ReferenceFillImageManager] Started.");
        imageFillManager = FindObjectOfType<ImageFillManager>();
        if (imageFillManager == null)
        {
            Debug.LogError("[ReferenceFillImageManager] ImageFillManager not found in scene!");
        }
        UpdateImageFromPrefs();
    }

    public void UpdateImageFromPrefs()
    {
        int selectedImage = PlayerPrefs.GetInt("FillImage", -1);
        Debug.Log($"[ReferenceFillImageManager] UpdateImageFromPrefs: FillImage = {selectedImage}");

        if (selectedImage >= 1 && selectedImage <= referenceFillImages.Length)
        {
            ActivateImageByIndex(selectedImage - 1); // Adjust for 0-based array index

            // ⭐ CRITICAL FIX: Tell the ImageFillManager to load the new image data
            if (imageFillManager != null)
            {
                imageFillManager.LoadNewImage();
            }
        }
        else
        {
            Debug.LogWarning("[ReferenceFillImageManager] Invalid or unset FillImage PlayerPref. Deactivating all images.");
            DeactivateAllImages();
        }
    }

    private void ActivateImageByIndex(int index)
    {
        if (referenceFillImages == null) return;

        for (int i = 0; i < referenceFillImages.Length; i++)
        {
            if (referenceFillImages[i] != null)
                referenceFillImages[i].SetActive(i == index);
        }

        Debug.Log($"[ReferenceFillImageManager] Activated image from ReferenceFillImageManager** GameObject at index: {index}");
    }

    private void DeactivateAllImages()
    {
        if (referenceFillImages == null) return;

        foreach (GameObject img in referenceFillImages)
        {
            if (img != null)
                img.SetActive(false);
        }

        Debug.Log("[ReferenceFillImageManager] All reference images deactivated.");
    }
}
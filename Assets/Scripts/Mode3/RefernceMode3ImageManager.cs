using UnityEngine;

public class RefernceMode3ImageManager : MonoBehaviour
{
    [Header("Assign Fruit/Animal GameObjects in Order")]
    public GameObject[] referenceMode3Images;

    void Start()
    {
        Debug.Log("[ReferenceFillImageManager] Started.");
        UpdateImageFromPrefs();
    }

    public void UpdateImageFromPrefs()
    {
        int selectedImage = PlayerPrefs.GetInt("Mode3Image", -1);
        Debug.Log($"[ReferenceFillImageManager] UpdateImageFromPrefs: FillImage = {selectedImage}");

        if (selectedImage >= 1 && selectedImage <= referenceMode3Images.Length)
        {
            ActivateImageByIndex(selectedImage - 1); // Adjust for 0-based array index
        }
        else
        {
            Debug.LogWarning("[ReferenceFillImageManager] Invalid or unset FillImage PlayerPref. Deactivating all images.");
            DeactivateAllImages();
        }
    }

    private void ActivateImageByIndex(int index)
    {
        if (referenceMode3Images == null) return;

        for (int i = 0; i < referenceMode3Images.Length; i++)
        {
            if (referenceMode3Images[i] != null)
                referenceMode3Images[i].SetActive(i == index);
        }

        Debug.Log($"[ReferenceFillImageManager] Activated image from ReferenceFillImageManager** GameObject at index: {index}");
    }

    private void DeactivateAllImages()
    {
        if (referenceMode3Images == null) return;

        foreach (GameObject img in referenceMode3Images)
        {
            if (img != null)
                img.SetActive(false);
        }

        Debug.Log("[ReferenceFillImageManager] All reference images deactivated.");
    }
}
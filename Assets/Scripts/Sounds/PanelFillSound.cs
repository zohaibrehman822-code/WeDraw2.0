using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PanelFillSound : MonoBehaviour
{
    public GameObject LoadingPanelPanel;
    public GameObject DrawingPanel;

    [Header("References")]
    public AudioSource audioSource;   // Fill sound
    public Image fillImage;           // Fill Image
    public BackgroundSoundManager soundManager; // <-- ADD THIS

    [Header("Settings")]
    public float fillDuration = 0.25f;

    private bool hasPlayed = false;

    private void OnEnable()
    {
        // Reset fill and flag each time the panel becomes active
        if (fillImage != null) fillImage.fillAmount = 0f;
        hasPlayed = false;

        StartCoroutine(FillRoutine());
    }

    private IEnumerator FillRoutine()
    {
        // Play fill sound once
        if (!hasPlayed && audioSource != null)
        {
            audioSource.Play();
            hasPlayed = true;
        }

        float elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;

            if (fillImage != null)
                fillImage.fillAmount = Mathf.Clamp01(elapsed / fillDuration);

            yield return null;
        }

        Debug.Log("Image Loading Filled **");

        // 🔊 SWITCH TO DRAWING MODE SOUND
        if (soundManager != null)
        {
            soundManager.StopBGSound();
            soundManager.StartDrawingModeSound();
        }

        DrawingPanel.SetActive(true);
        LoadingPanelPanel.SetActive(false);
    }
}

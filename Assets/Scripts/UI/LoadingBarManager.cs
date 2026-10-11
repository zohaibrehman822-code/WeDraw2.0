using UnityEngine;
using System.Collections;

public class LoadingPanelManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loadingPanel;   // Panel to show first
    public GameObject nextPanel;      // Panel to show after sound

    [Header("Audio")]
    public AudioSource loadingSound;  // 2-second intro sound
    public GameObject bgSound;        // GameObject that has an AudioSource

    void Start()
    {
        StartCoroutine(ShowLoadingThenNext());
    }

    private IEnumerator ShowLoadingThenNext()
    {
        // 1️⃣ Show loading panel and hide the next panel
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (nextPanel != null) nextPanel.SetActive(false);

        // 2️⃣ Play the loading sound once
        if (loadingSound != null)
        {
            loadingSound.Play();
            Debug.Log("[LoadingPanel] Loading sound started.");
        }

        // 3️⃣ Wait for the clip length (fall back to 2 sec if no clip)
        float waitTime = (loadingSound != null && loadingSound.clip != null)
                           ? loadingSound.clip.length
                           : 2f;
        yield return new WaitForSeconds(waitTime);

        // 4️⃣ Hide loading panel & show next panel
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (nextPanel != null) nextPanel.SetActive(true);
        Debug.Log("[LoadingPanel] Loading panel hidden, next panel shown.");

        // 5️⃣ Start background sound (get AudioSource from the GameObject)
        if (bgSound != null)
        {
            AudioSource bgSource = bgSound.GetComponent<AudioSource>();
            if (bgSource != null && !bgSource.isPlaying)
            {
                bgSource.Play();
                Debug.Log("[LoadingPanel] Background sound started.");
            }
            else
            {
                Debug.LogWarning("[LoadingPanel] No AudioSource found on bgSound object.");
            }
        }
    }
}

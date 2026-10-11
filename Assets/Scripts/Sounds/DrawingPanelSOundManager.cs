using UnityEngine;

public class DrawingPanelSoundManager : MonoBehaviour
{
    [Header("Assign the AudioSource that holds your refresh sound")]
    public AudioSource RefreshSound;

    [Header("Assign the AudioSource that holds your continuous brushing sound")]
    public AudioSource BrushingSound;

    [Header("Brushing Sound Control")]
    public bool allowBrushingSound = true;

    /// <summary>
    /// FIX #10: Use PlayOneShot so the refresh sound always plays fully from
    /// the start even if called again before the previous play finishes.
    /// The old Play() would silently restart the clip mid-playback.
    /// </summary>
    public void PlayRefreshSound()
    {
        if (RefreshSound == null)
        {
            Debug.LogWarning("[DrawingPanelSoundManager] No RefreshSound assigned.");
            return;
        }
        RefreshSound.PlayOneShot(RefreshSound.clip);
    }

    /// <summary>
    /// Starts the continuous brushing sound if allowed.
    /// </summary>
    public void PlayBrushingSound()
    {
        if (!allowBrushingSound) return;

        if (BrushingSound == null)
        {
            Debug.LogWarning("[DrawingPanelSoundManager] No BrushingSound assigned.");
            return;
        }

        if (!BrushingSound.isPlaying)
        {
            BrushingSound.loop = true;
            BrushingSound.Play();
        }
    }

    /// <summary>
    /// Stops the brushing sound.
    /// </summary>
    public void StopBrushingSound()
    {
        if (BrushingSound != null && BrushingSound.isPlaying)
            BrushingSound.Stop();
    }
}
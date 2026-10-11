using UnityEngine;

public class Mode3SoundManager : MonoBehaviour
{
    [Header("Assign the AudioSource that holds your refresh sound")]
    public AudioSource RefreshSound;

    [Header("Assign the AudioSource that holds your continuous brushing sound")]
    public AudioSource BrushingSound;

    [Header("Brushing Sound Control")]
    public bool allowBrushingSound = true;   // ✅ TRUE / FALSE

    public void PlayRefreshSound()
    {
        if (RefreshSound == null) return;

        if (!RefreshSound.isPlaying)
            RefreshSound.Play();
    }

    public void PlayBrushingSound()
    {
        // ❌ Block sound when brush = None
        if (!allowBrushingSound)
            return;

        if (BrushingSound == null) return;

        if (!BrushingSound.isPlaying)
        {
            BrushingSound.loop = true;
            BrushingSound.Play();
        }
    }

    public void StopBrushingSound()
    {
        if (BrushingSound != null && BrushingSound.isPlaying)
            BrushingSound.Stop();
    }
}

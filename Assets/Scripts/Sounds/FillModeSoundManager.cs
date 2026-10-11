using UnityEngine;

public class FillModeSoundManager : MonoBehaviour
{
    [Header("Assign the AudioSource that holds your sound clips")]
    public AudioSource RefreshSound;
    public AudioSource FilledSound;
    public AudioSource CompletedSound;

    [Header("Fill Sound Control")]
    public bool allowFillSound = true;   // ✅ global switch

    public void PlayRefreshSound()
    {
        if (RefreshSound == null) return;

        if (!RefreshSound.isPlaying)
            RefreshSound.Play();
    }

    public void PlayFilledSound()
    {
        if (!allowFillSound) return;   // ❌ block when None selected
        if (FilledSound == null) return;

        if (!FilledSound.isPlaying)
            FilledSound.Play();
    }

    public void PlayCompletedSound()
    {
        if (!allowFillSound) return;   // ❌ block when None selected
        if (CompletedSound == null) return;

        if (!CompletedSound.isPlaying)
            CompletedSound.Play();
    }
}

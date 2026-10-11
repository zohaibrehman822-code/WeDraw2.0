using UnityEngine;
using UnityEngine.UI;

public class BackgroundSoundManager : MonoBehaviour
{
    public AudioSource bgAudioSource;
    public AudioSource drawingModeAudioSource;
    public AudioSource imageFillModeAudioSource;
    public AudioSource paintModeAudioSource;

    [Header("Volume Control")]
    public Slider volumeSlider;

    [Header("Mute UI")]
    public GameObject muteButton;     // Show when volume = 0
    public GameObject unmuteButton;   // Show when volume > 0

    private float lastVolume = 1f;

    void Awake()
    {
        if (bgAudioSource == null)
            bgAudioSource = GetComponent<AudioSource>();

        if (volumeSlider != null)
            SetAllVolumes(volumeSlider.value);

        UpdateMuteUI();
    }

    void OnEnable()
    {
        StartBGSound();
    }

    void Start()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(SetAllVolumes);
    }

    /* =========================
       VOLUME CONTROL
       ========================= */
    public void SetAllVolumes(float volume)
    {
        if (bgAudioSource != null)
            bgAudioSource.volume = volume;

        if (drawingModeAudioSource != null)
            drawingModeAudioSource.volume = volume;

        if (imageFillModeAudioSource != null)
            imageFillModeAudioSource.volume = volume;

        if (paintModeAudioSource != null)
            paintModeAudioSource.volume = volume;

        // store last non-zero volume
        if (volume > 0.01f)
            lastVolume = volume;

        UpdateMuteUI();
    }

    /* =========================
       MUTE / UNMUTE
       ========================= */
    public void Mute()
    {
        SetAllVolumes(0f);

        if (volumeSlider != null)
            volumeSlider.value = 0f;
    }

    public void Unmute()
    {
        float restoreVolume = lastVolume > 0.01f ? lastVolume : 1f;

        SetAllVolumes(restoreVolume);

        if (volumeSlider != null)
            volumeSlider.value = restoreVolume;
    }

    /* =========================
       UI LOGIC
       ========================= */
    void UpdateMuteUI()
    {
        bool isMuted = bgAudioSource == null || bgAudioSource.volume <= 0.01f;

        // Show MUTE button only when sound is OFF
        if (muteButton != null)
            muteButton.SetActive(isMuted);

        // Show UNMUTE button only when sound is ON
        if (unmuteButton != null)
            unmuteButton.SetActive(!isMuted);
    }

    /* =========================
       BACKGROUND SOUND
       ========================= */
    public void StartBGSound()
    {
        if (bgAudioSource != null && !bgAudioSource.isPlaying)
            bgAudioSource.Play();
    }

    public void StopBGSound()
    {
        if (bgAudioSource != null && bgAudioSource.isPlaying)
            bgAudioSource.Stop();
    }

    /* =========================
       DRAWING MODE SOUND
       ========================= */
    public void StartDrawingModeSound()
    {
        if (drawingModeAudioSource != null && !drawingModeAudioSource.isPlaying)
            drawingModeAudioSource.Play();
    }

    public void StopDrawingModeSound()
    {
        if (drawingModeAudioSource != null && drawingModeAudioSource.isPlaying)
            drawingModeAudioSource.Stop();
    }

    /* =========================
       IMAGE FILL MODE SOUND
       ========================= */
    public void StartImageFillModeSound()
    {
        if (imageFillModeAudioSource != null && !imageFillModeAudioSource.isPlaying)
            imageFillModeAudioSource.Play();
    }

    public void StopImageFillModeSound()
    {
        if (imageFillModeAudioSource != null && imageFillModeAudioSource.isPlaying)
            imageFillModeAudioSource.Stop();
    }

    /* =========================
       PAINT MODE SOUND
       ========================= */
    public void StartPaintModeSound()
    {
        if (paintModeAudioSource != null && !paintModeAudioSource.isPlaying)
            paintModeAudioSource.Play();
    }

    public void StopPaintModeSound()
    {
        if (paintModeAudioSource != null && paintModeAudioSource.isPlaying)
            paintModeAudioSource.Stop();
    }
}
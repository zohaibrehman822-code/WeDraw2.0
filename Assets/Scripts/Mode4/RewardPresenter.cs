using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ONE shared reveal for all drawings. Put it on an object that is always active
/// (for example an empty "RewardManager" with an AudioSource), NOT on the reward image.
/// Shows the picture growing, plays the sound while it grows, and the replay button
/// plays the sound again.
/// </summary>
public class RewardPresenter : MonoBehaviour
{
    [Header("Scene objects")]
    [SerializeField] Image rewardImage;
    [SerializeField] Button replayButton;
    [Tooltip("Left empty, the AudioSource on this object is used.")]
    [SerializeField] AudioSource audioSource;

    [Header("Grow")]
    [SerializeField, Min(0.1f)] float growDuration = 1f;
    [Tooltip("Size at the start of the growth (0 = from nothing, 1 = full size).")]
    [SerializeField, Range(0f, 1f)] float startScale = 0.1f;

    AudioClip currentClip;
    Coroutine routine;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (replayButton != null) replayButton.onClick.AddListener(Replay);
        Hide();
    }

    /// <summary>Shows this picture growing and plays this sound.</summary>
    public void Show(Sprite sprite, AudioClip clip)
    {
        if (rewardImage == null)
        {
            Debug.LogWarning("RewardPresenter: assign the Reward Image.", this);
            return;
        }

        StopGrow();
        currentClip = clip;

        rewardImage.sprite = sprite;
        rewardImage.preserveAspect = true;
        rewardImage.gameObject.SetActive(true);
        rewardImage.rectTransform.localScale = Vector3.one * startScale;

        if (replayButton != null) replayButton.gameObject.SetActive(false);

        PlayClip();
        routine = StartCoroutine(Grow());
    }

    /// <summary>Plays the current sound again. Wired to the replay button automatically.</summary>
    public void Replay()
    {
        PlayClip();
    }

    /// <summary>Hides the picture and the button, and stops the sound.</summary>
    public void Hide()
    {
        StopGrow();
        if (audioSource != null) audioSource.Stop();
        currentClip = null;
        if (rewardImage != null) rewardImage.gameObject.SetActive(false);
        if (replayButton != null) replayButton.gameObject.SetActive(false);
    }

    IEnumerator Grow()
    {
        RectTransform rt = rewardImage.rectTransform;
        float t = 0f;
        while (t < growDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / growDuration);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(k));
            yield return null;
        }

        rt.localScale = Vector3.one;
        if (replayButton != null) replayButton.gameObject.SetActive(true);
        routine = null;
    }

    void StopGrow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    void PlayClip()
    {
        if (audioSource == null || currentClip == null) return;
        audioSource.Stop();
        audioSource.clip = currentClip;
        audioSource.Play();
    }

    // Grows a little past full size and settles back: a soft "pop".
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
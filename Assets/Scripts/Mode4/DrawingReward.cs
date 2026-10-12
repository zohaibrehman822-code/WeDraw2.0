using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained reward for ONE letter prefab (Letter-A, Letter-B ...).
/// Everything it needs lives inside the prefab: the reward image, the replay
/// button and the sound. When the shape has finished sliding (MoveOnComplete),
/// the picture grows with a soft pop while the sound plays. The replay button
/// plays the sound again.
/// Attach to the prefab ROOT, next to MoveOnComplete. An AudioSource is added automatically.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DrawingReward : MonoBehaviour
{
    [Header("Reward (inside this prefab)")]
    [Tooltip("The Image that grows. Put this letter's picture (apple, ball ...) in its Source Image.")]
    [SerializeField] Image rewardImage;

    [Tooltip("The button that plays the sound again.")]
    [SerializeField] Button replayButton;

    [Tooltip("This letter's sound. Plays while the picture grows.")]
    [SerializeField] AudioClip rewardSound;

    [Header("Grow")]
    [SerializeField, Min(0.1f)] float growDuration = 1f;
    [Tooltip("Size at the start of the growth (1 = full size).")]
    [SerializeField, Range(0f, 1f)] float startScale = 0.1f;

    [Header("Source")]
    [Tooltip("Left empty, it is found on this object automatically.")]
    [SerializeField] MoveOnComplete mover;

    [Header("Audio")]
    [Tooltip("AudioSource that contains this letter's pronunciation clip. Defaults to the prefab root AudioSource.")]
    [SerializeField] AudioSource audioSource;
    Coroutine routine;

    void Awake()
    {
        if (audioSource == null)
        {
            if (mover == null) mover = GetComponentInParent<MoveOnComplete>();
            if (mover != null) audioSource = mover.GetComponent<AudioSource>();
        }
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (replayButton != null) replayButton.onClick.AddListener(Replay);
    }

    void OnEnable()
    {
        if (mover == null) mover = GetComponent<MoveOnComplete>();
        if (mover != null) mover.onMoveFinished.AddListener(ShowReward);
        else Debug.LogWarning("DrawingReward: add MoveOnComplete to the same object.", this);

        HideReward();
    }

    void OnDisable()
    {
        if (mover != null) mover.onMoveFinished.RemoveListener(ShowReward);
        routine = null;
        if (audioSource != null) audioSource.Stop();
    }

    /// <summary>Shows the picture growing and plays the sound.</summary>
    public void ShowReward()
    {
        if (rewardImage == null)
        {
            Debug.LogWarning("DrawingReward: assign the Reward Image.", this);
            return;
        }

        StopGrow();

        rewardImage.preserveAspect = true;
        rewardImage.gameObject.SetActive(true);
        rewardImage.rectTransform.localScale = Vector3.one * startScale;
        if (replayButton != null) replayButton.gameObject.SetActive(false);

        PlaySound();
        routine = StartCoroutine(Grow());
    }

    /// <summary>Plays the sound again. Connected to the replay button automatically.</summary>
    public void Replay()
    {
        PlaySound();
    }

    void HideReward()
    {
        StopGrow();
        if (audioSource != null) audioSource.Stop();
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

    void PlaySound()
    {
        if (audioSource == null) return;

        // Existing letter prefabs already carry their pronunciation clip on the
        // AudioSource. Use it as a safe fallback when Reward Sound is unassigned.
        AudioClip clip = rewardSound != null ? rewardSound : audioSource.clip;
        if (clip == null) return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
        Debug.Log($"{clip.name} sound player", this);
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

using UnityEngine;

/// <summary>
/// Attach to the ROOT of each shape (next to MoveOnComplete).
/// Holds THIS letter's reward picture and sound. When the shape has finished
/// sliding, it asks the RewardPresenter to show them.
/// </summary>
public class DrawingReward : MonoBehaviour
{
    [Tooltip("The picture for this letter (A = apple, B = ball ...).")]
    [SerializeField] Sprite rewardPicture;

    [Tooltip("The sound for this letter. Plays while the picture grows, and again on the replay button.")]
    [SerializeField] AudioClip rewardSound;

    [Tooltip("Left empty, it is found on this object automatically.")]
    [SerializeField] MoveOnComplete mover;

    RewardPresenter presenter;

    RewardPresenter Presenter
    {
        get
        {
            if (presenter == null) presenter = FindFirstObjectByType<RewardPresenter>();
            return presenter;
        }
    }

    void OnEnable()
    {
        if (mover == null) mover = GetComponent<MoveOnComplete>();
        if (mover != null) mover.onMoveFinished.AddListener(ShowReward);
        else Debug.LogWarning("DrawingReward: add MoveOnComplete to the same object.", this);

        // A new drawing was opened: hide the previous reward.
        if (Presenter != null) Presenter.Hide();
    }

    void OnDisable()
    {
        if (mover != null) mover.onMoveFinished.RemoveListener(ShowReward);
    }

    public void ShowReward()
    {
        if (Presenter == null)
        {
            Debug.LogWarning("DrawingReward: no RewardPresenter found in the scene.", this);
            return;
        }
        Presenter.Show(rewardPicture, rewardSound);
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to the ROOT of each shape (Digit-1, Letter-A, Letter-B ...).
/// When the drawing inside it is completed, the root slides to X = -125,
/// so the white area, the border and the red ink all move together.
/// When the slide has finished, onMoveFinished is fired (used by DrawingReward).
/// </summary>
public class MoveOnComplete : MonoBehaviour
{
    [Tooltip("The GuidedDrawing (on the Ink object). Left empty, it is found automatically inside this shape.")]
    [SerializeField] GuidedDrawing drawing;

    [Tooltip("What to move. Left empty, it moves THIS object (the shape root).")]
    [SerializeField] Transform target;

    [SerializeField] float targetX = -125f;

    [Tooltip("Seconds. 0 = instant.")]
    [SerializeField, Min(0f)] float duration = 0.5f;

    [Tooltip("When the shape is opened again, put it back at its start position.")]
    [SerializeField] bool resetOnEnable = true;

    [Header("Events")]
    [Tooltip("Fires when the shape has fully arrived at the target X.")]
    public UnityEvent onMoveFinished;

    Vector2 startPos;
    bool hasStart;

    Transform Target => target != null ? target : transform;

    Vector2 GetPos()
    {
        var rt = Target as RectTransform;
        return rt != null ? rt.anchoredPosition : (Vector2)Target.localPosition;
    }

    void SetPos(Vector2 p)
    {
        var rt = Target as RectTransform;
        if (rt != null) rt.anchoredPosition = p;
        else Target.localPosition = new Vector3(p.x, p.y, Target.localPosition.z);
    }

    void Awake()
    {
        startPos = GetPos();
        hasStart = true;
    }

    void OnEnable()
    {
        if (resetOnEnable && hasStart) SetPos(startPos);

        if (drawing == null) drawing = GetComponentInChildren<GuidedDrawing>(true);
        if (drawing != null) drawing.onCompleted.AddListener(Move);
        else Debug.LogWarning("MoveOnComplete: no GuidedDrawing found inside this shape.", this);
    }

    void OnDisable()
    {
        if (drawing != null) drawing.onCompleted.RemoveListener(Move);
        StopAllCoroutines();
    }

    public void Move()
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        Vector2 from = GetPos();
        Vector2 to = new Vector2(targetX, from.y);

        if (duration <= 0f)
        {
            SetPos(to);
            onMoveFinished?.Invoke();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            SetPos(Vector2.LerpUnclamped(from, to, k));
            yield return null;
        }
        SetPos(to);
        onMoveFinished?.Invoke();
    }
}
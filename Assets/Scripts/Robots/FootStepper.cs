using UnityEngine;
using System;
using System.Collections;

public class FootStepper : MonoBehaviour
{
    [Header("Arc Points - Facing Right")]
    public Transform startRight;
    public Transform peakRight;
    public Transform endRight;
    public Transform backRight;

    [Header("Arc Points - Facing Left")]
    public Transform startLeft;
    public Transform peakLeft;
    public Transform endLeft;
    public Transform backLeft;

    private Transform currentStart;
    private Transform currentPeak;
    private Transform currentEnd;
    private Transform currentBack;

    [Header("Jump Arc Points - Right")]
    public Transform jumpUpRight;
    public Transform jumpDownRight;

    [Header("Jump Arc Points - Left")]
    public Transform jumpUpLeft;
    public Transform jumpDownLeft;

    [Header("Foot Target")]
    public Transform footTarget;

    [Header("Step Timings")]
    public float toPeakDuration = 0.15f;
    public float toFarDuration = 0.15f;
    public float toBackDuration = 0.10f;
    public float toStartDuration = 0.25f;

    private Coroutine routine;
    private bool facingRight = true;
    [SerializeField] private GroundChecker groundChecker;
    public bool IsGrounded => groundChecker != null && groundChecker.IsGrounded;

    public void SetFacingDirection(bool isRight)
    {
        facingRight = isRight;
    }

    public void InterruptAndReset()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        var start = facingRight ? startRight : startLeft;
        if (footTarget != null && start != null)
            footTarget.position = start.position;
    }

    public void ToPeak(Action onArrived)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ToPeakRoutine(onArrived));
    }
    public void ToFar(Action onArrived)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ToFarRoutine(onArrived));
    }
    public void ToBack(Action onArrived)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ToBackRoutine(onArrived));
    }
    public void ToStartFromFarOrBack(Action onArrived)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ToStartRoutine(onArrived));
    }

    private IEnumerator ToPeakRoutine(Action onArrived)
    {
        currentStart = facingRight ? startRight : startLeft;
        currentPeak = facingRight ? peakRight : peakLeft;
        currentEnd = facingRight ? endRight : endLeft;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / toPeakDuration;
            footTarget.position = QuadraticBezier(currentStart.position, currentPeak.position, currentEnd.position, t * 0.5f); // half of the arc
            yield return null;
        }
        routine = null;
        onArrived?.Invoke();
    }
    private IEnumerator ToFarRoutine(Action onArrived)
    {
        currentStart = facingRight ? startRight : startLeft;
        currentPeak = facingRight ? peakRight : peakLeft;
        currentEnd = facingRight ? endRight : endLeft;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / toFarDuration;
            footTarget.position = QuadraticBezier(currentStart.position, currentPeak.position, currentEnd.position, 0.5f + t * 0.5f); // second half of the arc
            yield return null;
        }
        routine = null;
        onArrived?.Invoke();
    }
    private IEnumerator ToBackRoutine(Action onArrived)
    {
        currentStart = facingRight ? startRight : startLeft;
        currentBack = facingRight ? backRight : backLeft;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / toBackDuration;
            footTarget.position = Vector3.Lerp(currentStart.position, currentBack.position, t);
            yield return null;
        }
        footTarget.position = currentBack.position;
        routine = null;
        onArrived?.Invoke();
    }
    private IEnumerator ToStartRoutine(Action onArrived)
    {
        // Can be used for Far->Start or Back->Start
        Vector3 from, to;
        if ((footTarget.position - ((facingRight ? endRight : endLeft).position)).sqrMagnitude < 0.01f)
        {
            from = facingRight ? endRight.position : endLeft.position;
        }
        else
        {
            from = facingRight ? backRight.position : backLeft.position;
        }
        to = facingRight ? startRight.position : startLeft.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / toStartDuration;
            footTarget.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        footTarget.position = to;
        routine = null;
        onArrived?.Invoke();
    }
    private Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 ab = Vector3.Lerp(a, b, t);
        Vector3 bc = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(ab, bc, t);
    }

    public void Jump(float upDuration, float downDuration, Action onLanded)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(JumpRoutine(upDuration, downDuration, onLanded));
    }

    private IEnumerator JumpRoutine(float upDuration, float downDuration, Action onLanded)
    {
        Transform jumpUp = facingRight ? jumpUpRight : jumpUpLeft;
        Transform jumpDown = facingRight ? jumpDownRight : jumpDownLeft;
        Vector3 start = footTarget.position;

        // Move up
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / upDuration;
            footTarget.position = Vector3.Lerp(start, jumpUp.position, t);
            yield return null;
        }
        footTarget.position = jumpUp.position;

        // Hang (optional)
        yield return new WaitForSeconds(0.1f);

        // Move down
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / downDuration;
            footTarget.position = Vector3.Lerp(jumpUp.position, jumpDown.position, t);
            yield return null;
        }
        footTarget.position = jumpDown.position;

        routine = null;
        onLanded?.Invoke();
    }

}
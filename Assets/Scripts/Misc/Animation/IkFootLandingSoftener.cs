using UnityEngine;

/// <summary>
/// Reshapes the height curve of the walk's foot IK targets, after the Animator and before the
/// 2D IK solves. The stride stays the original one; only the end of the descent is eased so the
/// foot touches the ground softly, and the lift-off is sped up so the step stays lively.
/// </summary>
/// <remarks>
/// Below <see cref="softZoneHeight"/> above the ground, the animated height h is remapped to
/// zone * f(h / zone). While the foot goes down, f eases in (slope near 0 at contact). While it
/// goes up, f eases out (fast start). Above the zone and on the ground, the animation is untouched.
/// </remarks>
[DefaultExecutionOrder(-50)] // After the Animator, before IKManager2D (-10).
public sealed class IkFootLandingSoftener : MonoBehaviour {
    [Tooltip("Foot IK targets animated by the master rig (LLegSolver_Target, RLegSolver_Target).")]
    [SerializeField] private Transform[] targets = new Transform[0];

    [Header("Ground")]
    [Tooltip("Ground height relative to each target's idle (neutral) height, in rig units.")]
    [SerializeField] private float groundOffset = 0f;
    [Tooltip("Height above the ground where the reshaping starts (rig units). The walk lifts the foot about 2.")]
    [SerializeField, Min(0.01f)] private float softZoneHeight = 0.9f;

    [Header("Curve")]
    [Tooltip("0 = original descent, 1 = foot slows to a stop right before touching the ground.")]
    [SerializeField, Range(0f, 1f)] private float landingSoftness = 0.8f;
    [Tooltip("0 = original lift-off, 1 = foot leaves the ground fast then settles into the swing.")]
    [SerializeField, Range(0f, 1f)] private float liftOffSnap = 0.5f;
    [Tooltip("Animated height changes smaller than this (per frame) do not change the up/down direction.")]
    [SerializeField, Min(0f)] private float directionThreshold = 0.0005f;

    private Vector3[] neutral;
    private float[] previousAnimatedY;
    private bool[] descending;
    private Vector3[] lastWritten;
    private bool[] hasWritten;

    private void Awake() {
        CaptureNeutral();
    }

    /// <summary>Uses the targets' current local positions as the idle stance (feet on the ground).</summary>
    public void CaptureNeutral() {
        int count = targets != null ? targets.Length : 0;
        neutral = new Vector3[count];
        previousAnimatedY = new float[count];
        descending = new bool[count];
        lastWritten = new Vector3[count];
        hasWritten = new bool[count];
        for (int i = 0; i < count; i++) {
            if (targets[i] != null) {
                neutral[i] = targets[i].localPosition;
                previousAnimatedY[i] = neutral[i].y;
            }
        }
    }

    private void LateUpdate() {
        if (neutral == null || neutral.Length != targets.Length) {
            CaptureNeutral();
        }

        for (int i = 0; i < targets.Length; i++) {
            Transform target = targets[i];
            if (target == null) {
                continue;
            }

            Vector3 animated = target.localPosition;
            // If the Animator did not write this frame, the value is still ours: do not reshape twice.
            if (hasWritten[i] && animated == lastWritten[i]) {
                continue;
            }

            float deltaY = animated.y - previousAnimatedY[i];
            if (Mathf.Abs(deltaY) > directionThreshold) {
                descending[i] = deltaY < 0f;
            }
            previousAnimatedY[i] = animated.y;

            float groundY = neutral[i].y + groundOffset;
            float height = animated.y - groundY;
            if (height > 0f && height < softZoneHeight) {
                float x = height / softZoneHeight;
                animated.y = groundY + softZoneHeight * Reshape(x, descending[i]);
            }

            target.localPosition = animated;
            lastWritten[i] = animated;
            hasWritten[i] = true;
        }
    }

    /// <summary>
    /// Maps a normalized height (0 = ground, 1 = top of the soft zone) for the current direction.
    /// Both curves keep f(0) = 0 and f(1) = 1, so the motion stays continuous.
    /// </summary>
    public float Reshape(float x, bool goingDown) {
        x = Mathf.Clamp01(x);
        if (goingDown) {
            // Ease-in towards the ground: slope 2 entering the zone, about 0 at contact.
            return Mathf.Lerp(x, x * x, landingSoftness);
        }

        // Ease-out from the ground: fast lift-off, smooth join with the swing.
        float easeOut = 1f - (1f - x) * (1f - x);
        return Mathf.Lerp(x, easeOut, liftOffSnap);
    }
}

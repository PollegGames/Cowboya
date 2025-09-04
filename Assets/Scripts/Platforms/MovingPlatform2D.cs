using UnityEngine;

/// <summary>
/// Generic component that tracks a Transform's per-physics-step delta movement.
/// Add this to any moving platform object (or its root) that riders stand on.
/// </summary>
[DisallowMultipleComponent]
public class MovingPlatform2D : MonoBehaviour, IMovingPlatform2D
{
    [Header("Tracking Target")]
    [Tooltip("Optional. If null, uses this transform. Set to the actual platform root that moves.")]
    [SerializeField] private Transform trackedTransform;

    private Vector2 lastPosition;
    private Vector2 deltaThisStep;
    private Vector2 velocity;
    private bool initialized;

    /// <summary>
    /// World-space delta position since the last physics step.
    /// </summary>
    public Vector2 DeltaPosition => deltaThisStep;

    /// <summary>
    /// World-space velocity estimated from the last physics step.
    /// </summary>
    public Vector2 Velocity => velocity;

    private Transform Target => trackedTransform ? trackedTransform : transform;

    private void Awake()
    {
        if (trackedTransform == null)
            trackedTransform = transform;
    }

    private void OnEnable()
    {
        initialized = false;
        deltaThisStep = Vector2.zero;
        velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        Vector2 current = Target.position;
        if (!initialized)
        {
            lastPosition = current;
            deltaThisStep = Vector2.zero;
            velocity = Vector2.zero;
            initialized = true;
            return;
        }

        deltaThisStep = current - lastPosition;
        velocity = Time.fixedDeltaTime > 0f ? deltaThisStep / Time.fixedDeltaTime : Vector2.zero;
        lastPosition = current;
    }
}


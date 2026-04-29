using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class CubePickup : MonoBehaviour, IGrabbable
{

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is. Recommended range: 5â€“15.")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [Tooltip("How much the joint resists oscillation. Recommended range: 0â€“1.")]
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [Tooltip("Maximum force the joint can apply. Recommended range: 500â€“3000.")]
    [SerializeField, Range(500f, 3000f)] private float maxForce = 2000f;

    /// <summary>
    /// How springy the joint movement is.
    /// </summary>
    public float Frequency
    {
        get => frequency;
        set
        {
            frequency = value;
            if (joint != null)
                joint.frequency = frequency;
        }
    }

    /// <summary>
    /// How much the joint resists oscillation.
    /// </summary>
    public float DampingRatio
    {
        get => dampingRatio;
        set
        {
            dampingRatio = value;
            if (joint != null)
                joint.dampingRatio = dampingRatio;
        }
    }

    /// <summary>
    /// Maximum force the joint can apply.
    /// </summary>
    public float MaxForce
    {
        get => maxForce;
        set
        {
            maxForce = value;
            if (joint != null)
                joint.maxForce = maxForce;
        }
    }

    [Header("Visuals")]
    [SerializeField, Tooltip("Sorting order applied while the cube is held.")] private int heldSortingOrder = 20;
    [SerializeField, Tooltip("Sorting order applied when the cube is idle.")] private int idleSortingOrder = 0;

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private bool attached = false;
    private bool wasStolen = false;

    public event Action<CubePickup> OnGrabbed;
    public event Action<CubePickup> OnReleased;
    private SpriteRenderer[] spriteRenderers;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();
        CacheSpriteRenderers();
        ApplySortingOrder(idleSortingOrder);
        if (joint != null)
        {
            joint.enabled = false;
            joint.autoConfigureTarget = false;
            joint.target = rb.position;
            joint.frequency = frequency;
            joint.dampingRatio = dampingRatio;
            joint.maxForce = maxForce;
        }
    }

    private void FixedUpdate()
    {
        if (joint == null || !joint.enabled || followTarget == null)
            return;

        joint.target = followTarget.position;
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        if (joint == null)
            joint = GetComponent<TargetJoint2D>();

        if (joint != null)
        {
            if (followTarget != null)
                joint.target = followTarget.position;
            joint.enabled = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(CubePickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(CubePickup)} received a null grab parent.");
            return;
        }

        var player = grabParent.GetComponentInParent<PlayerMovementController>();

        if (!wasStolen && transform.parent != null)
        {
            var brain = transform.parent.GetComponentInParent<RobotBrainNew>();
            var stateController = brain != null ? brain.GetComponent<RobotStateController>() : null;
            if (brain != null && stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
            {
                wasStolen = true;
            }
        }

        attached = true;
        rb.simulated = true;

        transform.SetParent(grabParent, true);
        SetFollowTarget(grabParent);
        ApplySortingOrder(heldSortingOrder);

        OnGrabbed?.Invoke(this);
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (joint == null)
            return;

        if (attached && joint.enabled && followTarget == null)
            joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        if (joint != null)
            joint.enabled = false;

        followTarget = null;
        transform.SetParent(null, true);

        OnReleased?.Invoke(this);
        ApplySortingOrder(idleSortingOrder);
    }

    private void CacheSpriteRenderers()
    {
        if (spriteRenderers != null)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void ApplySortingOrder(int order)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null)
                renderer.sortingOrder = order;
        }
    }
}


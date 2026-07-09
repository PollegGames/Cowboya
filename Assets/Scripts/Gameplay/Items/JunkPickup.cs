using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class JunkPickup : MonoBehaviour, IGrabbable
{
    [Header("Target Joint Settings")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [SerializeField, Range(500f, 3000f)] private float maxForce = 2000f;

    [Header("Visuals")]
    [SerializeField] private int heldSortingOrder = 20;
    [SerializeField] private int idleSortingOrder = 0;

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private Vector2 overrideAttractPoint;
    private bool hasOverrideAttractPoint;
    private bool isHeld;
    private SpriteRenderer[] spriteRenderers;

    public event Action<JunkPickup> OnGrabbed;
    public event Action<JunkPickup> OnReleased;

    public bool IsHeld => isHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
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
        {
            return;
        }

        if (hasOverrideAttractPoint)
        {
            joint.target = overrideAttractPoint;
            hasOverrideAttractPoint = false;
            return;
        }

        joint.target = followTarget.position;
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(JunkPickup)} received a null grab parent.");
            return;
        }

        isHeld = true;
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        transform.SetParent(grabParent, true);
        SetFollowTarget(grabParent);
        ApplySortingOrder(heldSortingOrder);

        OnGrabbed?.Invoke(this);
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (joint == null || !joint.enabled)
        {
            return;
        }

        overrideAttractPoint = attractPoint;
        hasOverrideAttractPoint = true;
        joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        isHeld = false;

        if (joint != null)
        {
            joint.enabled = false;
        }

        followTarget = null;
        hasOverrideAttractPoint = false;
        transform.SetParent(null, true);

        if (throwForce != Vector2.zero)
        {
            rb.AddForce(throwForce, ForceMode2D.Impulse);
        }

        ApplySortingOrder(idleSortingOrder);
        OnReleased?.Invoke(this);
    }

    private void SetFollowTarget(Transform target)
    {
        followTarget = target;

        if (joint == null)
        {
            joint = GetComponent<TargetJoint2D>();
        }

        if (joint == null)
        {
            Debug.LogWarning($"{nameof(JunkPickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
            return;
        }

        joint.target = followTarget.position;
        joint.enabled = true;
    }

    private void ApplySortingOrder(int order)
    {
        if (spriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = order;
            }
        }
    }
}

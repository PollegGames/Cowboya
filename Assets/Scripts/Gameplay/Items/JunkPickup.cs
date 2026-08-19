using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class JunkPickup : MonoBehaviour, IGrabbable, IGrabControllerDetachReceiver
{
    [Header("Identity")]
    [SerializeField] private JunkVariant junkVariant = JunkVariant.Junk1;

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
    private bool isConveyorControlled;
    private Transform currentHolder;
    private UnityEngine.Object grabLockOwner;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] colliders;
    private bool[] originalTriggerStates;

    public event Action<JunkPickup> OnGrabbed;
    public event Action<JunkPickup> OnReleased;

    public bool IsHeld => isHeld;
    public bool IsConveyorControlled => isConveyorControlled;
    public JunkVariant Variant => TryResolveNamedVariant(name, out JunkVariant namedVariant)
        ? namedVariant
        : junkVariant;
    public Transform CurrentHolder => currentHolder;
    public bool IsGrabLocked => grabLockOwner != null;
    public UnityEngine.Object GrabLockOwner => grabLockOwner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        originalTriggerStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            originalTriggerStates[i] = colliders[i].isTrigger;
        }
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
        _ = inventory;
        return !IsGrabLocked;
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(JunkPickup)} received a null grab parent.");
            return;
        }

        SetConveyorControlled(false);
        isHeld = true;
        currentHolder = grabParent;
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
        currentHolder = null;

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

    /// <summary>
    /// Claims exclusive grab access. Repeating the claim with the same owner is idempotent.
    /// </summary>
    public bool TryLockGrab(UnityEngine.Object owner)
    {
        if (owner == null)
        {
            return false;
        }

        if (grabLockOwner != null && grabLockOwner != owner)
        {
            return false;
        }

        grabLockOwner = owner;
        return true;
    }

    /// <summary>
    /// Releases exclusive grab access only for the owner that established it.
    /// </summary>
    public bool UnlockGrab(UnityEngine.Object owner)
    {
        if (owner == null || grabLockOwner == null || grabLockOwner != owner)
        {
            return false;
        }

        grabLockOwner = null;
        return true;
    }

    /// <summary>
    /// Clears the current hand joint without throwing the junk or raising OnReleased.
    /// </summary>
    public void OnDetachedFromGrabController()
    {
        isHeld = false;
        currentHolder = null;

        if (joint == null)
        {
            joint = GetComponent<TargetJoint2D>();
        }

        if (joint != null)
        {
            joint.enabled = false;
        }

        followTarget = null;
        hasOverrideAttractPoint = false;
        transform.SetParent(null, true);
        ApplySortingOrder(idleSortingOrder);
    }

    /// <summary>
    /// Makes junk non-blocking while a machine owns its movement, while keeping its
    /// colliders available to grab detection.
    /// </summary>
    public void SetConveyorControlled(bool controlled)
    {
        if (isConveyorControlled == controlled)
        {
            return;
        }

        isConveyorControlled = controlled;
        if (colliders == null)
        {
            colliders = GetComponentsInChildren<Collider2D>(true);
            originalTriggerStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                originalTriggerStates[i] = colliders[i].isTrigger;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].isTrigger = controlled || originalTriggerStates[i];
            }
        }
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

    private static bool TryResolveNamedVariant(string objectName, out JunkVariant variant)
    {
        variant = JunkVariant.Junk1;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        int cloneSuffix = objectName.IndexOf("(Clone)", StringComparison.Ordinal);
        string normalizedName = cloneSuffix >= 0
            ? objectName.Substring(0, cloneSuffix).Trim()
            : objectName.Trim();

        switch (normalizedName)
        {
            case "Junk_1":
                variant = JunkVariant.Junk1;
                return true;
            case "Junk_2":
                variant = JunkVariant.Junk2;
                return true;
            case "Junk_3":
                variant = JunkVariant.Junk3;
                return true;
            case "Junk_4":
                variant = JunkVariant.Junk4;
                return true;
            case "Junk_5":
                variant = JunkVariant.Junk5;
                return true;
            case "Junk_6":
                variant = JunkVariant.Junk6;
                return true;
            case "Junk_7":
                variant = JunkVariant.Junk7;
                return true;
            case "Junk_8":
                variant = JunkVariant.Junk8;
                return true;
            default:
                return false;
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class BatteryPickup : MonoBehaviour, IGrabbable
{
    [Header("Health settings")]
    [SerializeField] private float healthGain = 10f;

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is.")]
    [SerializeField] private float frequency = 5f;
    [Tooltip("How much the joint resists oscillation.")]
    [SerializeField] private float dampingRatio = 0.8f;
    [Tooltip("Maximum force the joint can apply.")]
    [SerializeField] private float maxForce = 1000f;

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private bool attached = false;
    private bool wasStolen = false;

    public static BatteryPickup PlayerHeldBattery { get; private set; }
    private bool heldByPlayer = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();

        joint.enabled = false;
        joint.autoConfigureTarget = false;
        joint.target = rb.position;
        joint.frequency = frequency;
        joint.dampingRatio = dampingRatio;
        joint.maxForce = maxForce;
    }

    private void FixedUpdate()
    {
        if (joint.enabled && followTarget != null)
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
            Debug.LogWarning($"{nameof(BatteryPickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
        }
    }

    public bool CanBeGrabbed()
    {
        if (PlayerHeldBattery != null && PlayerHeldBattery != this)
            return false;
        return !attached;
    }

    public void OnGrab(Transform grabParent)
    {
        if (PlayerHeldBattery != null && PlayerHeldBattery != this)
            return;

        var player = grabParent.GetComponentInParent<PlayerMovementController>();

        if (!wasStolen && transform.parent != null)
        {
            var enemy = transform.parent.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                var stateController = enemy.GetComponent<RobotStateController>();
                if (stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
                {
                    enemy.OnBatteryStolen(player.gameObject);
                    wasStolen = true;
                }
            }
        }

        attached = true;
        rb.simulated = true;

        var holderState = grabParent.GetComponentInParent<RobotStateController>();
        if (holderState != null)
            holderState.Stats.UpdateHealth(healthGain);

        if (player != null)
        {
            var hip = player.BodyReference;
            if (hip != null)
            {
                PlayerHeldBattery = this;
                heldByPlayer = true;
                SetFollowTarget(hip.transform);

                foreach (var battery in FindObjectsByType<BatteryPickup>(FindObjectsSortMode.None))
                {
                    if (battery != this)
                        Destroy(battery.gameObject);
                }
            }
        }
        else
        {
            SetFollowTarget(grabParent);
        }
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (attached && joint.enabled && followTarget == null)
            joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        joint.enabled = false;
        followTarget = null;

        if (heldByPlayer)
        {
            heldByPlayer = false;
            if (PlayerHeldBattery == this)
                PlayerHeldBattery = null;
        }
    }

    private void OnDestroy()
    {
        if (PlayerHeldBattery == this)
            PlayerHeldBattery = null;
    }

    public static void DropPlayerBattery()
    {
        if (PlayerHeldBattery != null)
        {
            Destroy(PlayerHeldBattery.gameObject);
            PlayerHeldBattery = null;
        }
    }
}

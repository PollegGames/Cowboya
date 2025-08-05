using UnityEngine;

public class PooledEnemy : MonoBehaviour, IPooledObject
{
    [SerializeField] private BodyJointLimiter bodyJointLimiter;
    [SerializeField] private LegJointLimiter legJointLimiter;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private RobotStateController robotStateController;
    [SerializeField] private JointBreaker jointBreaker;

    private Transform[] cachedTransforms;
    private Vector3[] defaultPositions;
    private Quaternion[] defaultRotations;

    private void Awake()
    {
        if (bodyJointLimiter == null)
            bodyJointLimiter = GetComponent<BodyJointLimiter>();
        if (legJointLimiter == null)
            legJointLimiter = GetComponent<LegJointLimiter>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();
        if (robotStateController == null)
            robotStateController = GetComponent<RobotStateController>();
        if (jointBreaker == null)
            jointBreaker = GetComponent<JointBreaker>();

        cachedTransforms = GetComponentsInChildren<Transform>(true);
        defaultPositions = new Vector3[cachedTransforms.Length];
        defaultRotations = new Quaternion[cachedTransforms.Length];
        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            defaultPositions[i] = cachedTransforms[i].localPosition;
            defaultRotations[i] = cachedTransforms[i].localRotation;
        }
    }

    public void OnReleaseToPool()
    {
        foreach (var rb in GetComponentsInChildren<Rigidbody2D>())
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (bodyJointLimiter != null)
            bodyJointLimiter.enabled = true;
        if (legJointLimiter != null)
            legJointLimiter.enabled = true;

        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            cachedTransforms[i].localPosition = defaultPositions[i];
            cachedTransforms[i].localRotation = defaultRotations[i];
        }
    }

    public void OnAcquireFromPool()
    {
        jointBreaker?.RestoreAll();

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        stateMachine?.ChangeState(null);
        robotStateController?.UpdateState(RobotState.Alive);
    }
}

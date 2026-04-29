using UnityEngine;

/// <summary>
/// Pool lifecycle glue for robots using the Heart/Brain/Body/Memory architecture.
/// Resets physical state and intent when the enemy is recycled.
/// </summary>
public class PooledEnemy : MonoBehaviour, IPooledObject
{
    [Header("Core")]
    [SerializeField] private RobotHeartNew heart;
    [SerializeField] private RobotBodyController body;
    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotStateController stateController;

    [Header("Visuals & Physics")]
    [SerializeField] private Animator animator;

    private Transform[] cachedTransforms;
    private Vector3[] defaultPositions;
    private Quaternion[] defaultRotations;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (animator == null)
            animator = GetComponent<Animator>();

        CacheTransforms();
    }

    public void OnReleaseToPool()
    {
        ResetRigidbodies();
        RestoreTransforms();

        // Avoid carrying intent into the next reuse.
        if (heart != null)
            heart.ResetIntentStack(false);
    }

    public void OnAcquireFromPool()
    {
        if (animator != null && animator.isInitialized)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (heart != null)
            heart.ResetIntentStack();
    }

    private void CacheTransforms()
    {
        cachedTransforms = GetComponentsInChildren<Transform>(true);
        defaultPositions = new Vector3[cachedTransforms.Length];
        defaultRotations = new Quaternion[cachedTransforms.Length];
        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            defaultPositions[i] = cachedTransforms[i].localPosition;
            defaultRotations[i] = cachedTransforms[i].localRotation;
        }
    }

    private void ResetRigidbodies()
    {
        foreach (var rb in GetComponentsInChildren<Rigidbody2D>())
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void RestoreTransforms()
    {
        if (cachedTransforms == null || defaultPositions == null || defaultRotations == null)
            return;

        for (int i = 0; i < cachedTransforms.Length; i++)
        {
            cachedTransforms[i].localPosition = defaultPositions[i];
            cachedTransforms[i].localRotation = defaultRotations[i];
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}


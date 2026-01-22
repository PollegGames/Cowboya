using UnityEngine;

public class HeadTargetController : MonoBehaviour
{
    public Transform hips;
    public Transform head;
    public RobotLocomotionController locomotion;

    [Header("Position Settings")]
    public float verticalOffset = 2.5f;
    public float maxMirrorOffset = 3f;

    [Header("Bend Settings")]
    public float maxBendOffset = 0.5f;
    public float bendSpeed = 5f;

    private float currentBend = 0f;

    [SerializeField] private MonoBehaviour directionProviderComponent;
    private ILookDirectionProvider directionProvider;
    private bool providerLogged;

    private void Awake()
    {
        if (directionProviderComponent != null)
            directionProvider = directionProviderComponent as ILookDirectionProvider;

        if (directionProvider == null)
            directionProvider = GetComponentInParent<ILookDirectionProvider>();

        if (directionProvider == null && !providerLogged)
        {
            Debug.LogError("HeadTargetController: ILookDirectionProvider not found.", this);
            providerLogged = true;
        }
    }

    private void OnValidate()
    {
        if (directionProviderComponent == null)
        {
            var provider = GetComponentInParent<ILookDirectionProvider>();
            directionProviderComponent = provider as MonoBehaviour;
        }
    }

    private void LateUpdate()
    {
        if (!hips || !head || !locomotion || directionProvider == null) return;

        // 1. Mirror relative to the head position
        float headOffsetX = head.position.x - hips.position.x;
        float mirroredOffsetX = -headOffsetX;
        mirroredOffsetX = Mathf.Clamp(mirroredOffsetX, -maxMirrorOffset, maxMirrorOffset);

        // 2. Movement direction → bend offset (which way to lean)
        float input = directionProvider.LookDirection.x;
        float targetBend = Mathf.Clamp(input, -1f, 1f) * maxBendOffset;

        // Smooth interpolation to avoid jerks
        currentBend = Mathf.Lerp(currentBend, targetBend, bendSpeed * Time.deltaTime);

        // 3. Position finale
        float totalXOffset = mirroredOffsetX + currentBend;
        transform.position = new Vector3(
            hips.position.x + totalXOffset,
            hips.position.y + verticalOffset,
            transform.position.z
        );
    }
}

using System;
using UnityEngine;

/// <summary>
/// Animates the garage panel along one local scale axis while keeping one edge fixed.
/// </summary>
public class GarageDoorController : MonoBehaviour {
    [Header("Door")]
    [SerializeField] private Transform panelTransform;
    [SerializeField] private Collider blockingCollider;
    [SerializeField] private GarageDoorScaleAxis scaleAxis = GarageDoorScaleAxis.LocalZ;
    [SerializeField] private GarageDoorAnchoredEdge anchoredEdge = GarageDoorAnchoredEdge.Positive;
    [SerializeField, Range(0.01f, 1f)] private float openScaleMultiplier = 0.05f;
    [SerializeField] private GarageDoorState initialState = GarageDoorState.Open;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float closeDuration = 0.6f;
    [SerializeField, Min(0.01f)] private float openDuration = 0.6f;

    private Vector3 closedLocalPosition;
    private Vector3 closedLocalScale;
    private Vector3 anchorLocalPosition;
    private float openAmount;
    private bool initialized;

    public event Action OnOpened;
    public event Action OnClosed;
    public event Action<GarageDoorState> OnStateChanged;

    public GarageDoorState State { get; private set; } = GarageDoorState.Open;
    public bool IsOpen => State == GarageDoorState.Open;
    public bool IsClosed => State == GarageDoorState.Closed;

    private void Awake() {
        Initialize();
    }

    private void OnEnable() {
        Initialize();
    }

    private void Update() {
        StepAnimation(Time.deltaTime);
    }

    /// <summary>
    /// Starts or continues opening the garage door. A closing door reverses from its current position.
    /// </summary>
    [ContextMenu("Open Door")]
    public void OpenDoor() {
        if (!Initialize() || State == GarageDoorState.Open || State == GarageDoorState.Opening)
            return;

        SetState(GarageDoorState.Opening);
    }

    /// <summary>
    /// Starts or continues closing the garage door. An opening door reverses from its current position.
    /// </summary>
    [ContextMenu("Close Door")]
    public void CloseDoor() {
        if (!Initialize() || State == GarageDoorState.Closed || State == GarageDoorState.Closing)
            return;

        SetState(GarageDoorState.Closing);
    }

    /// <summary>
    /// Places the door at its fully open endpoint without animation.
    /// </summary>
    [ContextMenu("Set Open Immediately")]
    public void SetOpenImmediate() {
        if (!Initialize())
            return;

        openAmount = 1f;
        ApplyOpenAmount();
        SetState(GarageDoorState.Open);
    }

    /// <summary>
    /// Places the door at its fully closed endpoint without animation.
    /// </summary>
    [ContextMenu("Set Closed Immediately")]
    public void SetClosedImmediate() {
        if (!Initialize())
            return;

        openAmount = 0f;
        ApplyOpenAmount();
        SetState(GarageDoorState.Closed);
    }

    /// <summary>
    /// Advances an active animation. Exposed to support deterministic Edit Mode tests.
    /// </summary>
    public void StepAnimation(float deltaTime) {
        if (!initialized || deltaTime <= 0f)
            return;

        if (State == GarageDoorState.Opening) {
            openAmount = Mathf.MoveTowards(openAmount, 1f, deltaTime / Mathf.Max(0.01f, openDuration));
            ApplyOpenAmount();
            if (openAmount >= 1f) {
                SetState(GarageDoorState.Open);
                OnOpened?.Invoke();
            }
        }
        else if (State == GarageDoorState.Closing) {
            openAmount = Mathf.MoveTowards(openAmount, 0f, deltaTime / Mathf.Max(0.01f, closeDuration));
            ApplyOpenAmount();
            if (openAmount <= 0f) {
                SetState(GarageDoorState.Closed);
                OnClosed?.Invoke();
            }
        }
    }

    private bool Initialize() {
        if (initialized)
            return true;

        if (panelTransform == null)
            panelTransform = transform.Find("Panel");

        if (panelTransform == null)
            return false;

        if (blockingCollider == null)
            blockingCollider = panelTransform.GetComponent<Collider>();

        closedLocalPosition = panelTransform.localPosition;
        closedLocalScale = panelTransform.localScale;
        anchorLocalPosition = ResolveAnchorLocalPosition();
        initialized = true;
        openAmount = initialState == GarageDoorState.Open ? 1f : 0f;
        ApplyOpenAmount();
        SetState(openAmount >= 1f ? GarageDoorState.Open : GarageDoorState.Closed);
        return true;
    }

    private Vector3 ResolveAnchorLocalPosition() {
        Renderer panelRenderer = panelTransform.GetComponent<Renderer>();
        Bounds localBounds = panelRenderer != null
            ? panelRenderer.localBounds
            : new Bounds(Vector3.zero, Vector3.one);
        bool usePositiveEdge = anchoredEdge == GarageDoorAnchoredEdge.Positive;
        Vector3 anchor = localBounds.center;

        switch (scaleAxis) {
            case GarageDoorScaleAxis.LocalY:
                anchor.y = usePositiveEdge ? localBounds.max.y : localBounds.min.y;
                break;
            case GarageDoorScaleAxis.LocalZ:
                anchor.z = usePositiveEdge ? localBounds.max.z : localBounds.min.z;
                break;
            default:
                anchor.x = usePositiveEdge ? localBounds.max.x : localBounds.min.x;
                break;
        }

        return anchor;
    }

    private void ApplyOpenAmount() {
        float easedOpenAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openAmount));
        float closedAxisScale = GetAxisValue(closedLocalScale);
        Vector3 scale = closedLocalScale;
        SetAxisValue(ref scale, Mathf.Lerp(
            closedAxisScale,
            closedAxisScale * openScaleMultiplier,
            easedOpenAmount));

        Vector3 closedAnchorOffset = panelTransform.localRotation
            * Vector3.Scale(closedLocalScale, anchorLocalPosition);
        Vector3 scaledAnchorOffset = panelTransform.localRotation
            * Vector3.Scale(scale, anchorLocalPosition);
        panelTransform.localScale = scale;
        panelTransform.localPosition = closedLocalPosition + closedAnchorOffset - scaledAnchorOffset;
    }

    private void SetState(GarageDoorState newState) {
        if (State == newState) {
            UpdateBlockingCollider();
            return;
        }

        State = newState;
        UpdateBlockingCollider();
        OnStateChanged?.Invoke(State);
    }

    private void UpdateBlockingCollider() {
        if (blockingCollider != null)
            blockingCollider.enabled = State == GarageDoorState.Closed;
    }

    private float GetAxisValue(Vector3 value) {
        switch (scaleAxis) {
            case GarageDoorScaleAxis.LocalY:
                return value.y;
            case GarageDoorScaleAxis.LocalZ:
                return value.z;
            default:
                return value.x;
        }
    }

    private void SetAxisValue(ref Vector3 value, float axisValue) {
        switch (scaleAxis) {
            case GarageDoorScaleAxis.LocalY:
                value.y = axisValue;
                break;
            case GarageDoorScaleAxis.LocalZ:
                value.z = axisValue;
                break;
            default:
                value.x = axisValue;
                break;
        }
    }
}

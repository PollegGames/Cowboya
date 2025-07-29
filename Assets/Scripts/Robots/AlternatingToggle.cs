using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AlternatingToggle : MonoBehaviour
{
    [SerializeField] private ArcTargetFollower arcTargetFollower; // assign in inspector

    [Header("Punch Targets")]
    public Transform leftArmTarget;
    public Transform rightArmTarget;
    public Transform leftRestPosition;
    public Transform rightRestPosition;
    public Transform toggleTarget; // position de la souris ou un ennemi

    [Header("Arc Control Points")]
    public Transform arcControlRight;
    public Transform arcControlLeft;

    [Header("Timing and Speed")]
    public float toggleDuration = 0.2f;
    public float returnSpeed = 10f;

    [Header("Toggle Hitboxes")]
    public ToggleBox leftArmToggleBox;
    public ToggleBox rightArmToggleBox;

    private bool isToggling = false;
    private Coroutine toggleRoutine;
    private RobotStateController robotBehaviour;
    private InputSystem_Actions controls;
    private bool interactHeld;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
        controls = new InputSystem_Actions();
        controls.Player.Interact.started += _ => interactHeld = true;
        controls.Player.Interact.canceled += _ => interactHeld = false;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Update()
    {
        if (interactHeld && !isToggling && robotBehaviour != null)
        {
            bool facingRight = arcTargetFollower.IsFacingRight;
            Transform armTarget = facingRight ? rightArmTarget : leftArmTarget;
            ToggleBox toggleBox = facingRight ? rightArmToggleBox : leftArmToggleBox;
            float toggleCost = toggleBox.ToggleCost;

            robotBehaviour.PerformAttackbyEnergy(toggleCost);

            toggleRoutine = StartCoroutine(ToggleHoldSequence(armTarget, toggleBox, facingRight));
        }

        if (!interactHeld && isToggling)
        {
            if (toggleRoutine != null)
                StopCoroutine(toggleRoutine);

            bool facingRight = arcTargetFollower.IsFacingRight;
            Transform armTarget = facingRight ? rightArmTarget : leftArmTarget;
            ToggleBox toggleBox = facingRight ? rightArmToggleBox : leftArmToggleBox;
            Transform restPosition = facingRight ? rightRestPosition : leftRestPosition;

            toggleBox?.Deactivate();
            StartCoroutine(ReturnToRest(armTarget, restPosition));
            isToggling = false;
        }
    }

    private IEnumerator ToggleHoldSequence(Transform armTarget, ToggleBox toggleBox, bool facingRight)
    {
        isToggling = true;
        toggleBox?.Activate();

        // Move to target position with arc
        Vector3 start = armTarget.position;
        Vector3 end = toggleTarget.position;
        Transform controlPoint = facingRight ? arcControlRight : arcControlLeft;
        Vector3 control = controlPoint != null ? controlPoint.position : (start + end) / 2 + Vector3.up * 0.5f;

        float timer = 0f;
        while (timer < toggleDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / toggleDuration);
            armTarget.position = GetQuadraticBezierPoint(start, control, end, t);
            yield return null;
        }

        // While holding, keep following the toggleTarget
        while (interactHeld)
        {
            armTarget.position = toggleTarget.position;
            yield return null;
        }
    }

    private IEnumerator ReturnToRest(Transform armTarget, Transform restPosition)
    {
        while (Vector3.Distance(armTarget.position, restPosition.position) > 0.01f)
        {
            armTarget.position = Vector3.MoveTowards(armTarget.position, restPosition.position, returnSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private Vector3 GetQuadraticBezierPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return Mathf.Pow(1 - t, 2) * a +
               2 * (1 - t) * t * b +
               Mathf.Pow(t, 2) * c;
    }
}
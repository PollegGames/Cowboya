using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MeshRenderer))]
public class LiftController : MonoBehaviour
{
    public enum LiftState { Idle, Warning, Moving }

    [Header("References")]
    public MeshRenderer lightRenderer;
    public Collider2D floorCollider;

    [Header("Movement")]
    public Vector2 moveDirection = Vector2.up;
    public float moveDistance = 5f;
    public float moveSpeed = 2f;
    public float waitAtTop = 1f;

    [Header("Visual Warning Timings")]
    public float greenDelay = 0.5f;
    public float orangeDelay = 2f;
    public float redDelay = 2f;

    [Header("Light Colors")]
    public string colorProperty = "_Color";
    public Color idleColor = Color.white;
    public Color greenColor = Color.green;
    public Color orangeColor = new Color(1f, 0.5f, 0f);
    public Color redColor = Color.red;

    [Header("State Flags")]
    public bool isWall = false;
    public bool isLocked = false;

    [Header("Events")]
    public UnityEvent onOutboundArrival;
    public UnityEvent onReturnArrival;

    private Vector3 startPos, endPos;
    private LiftState currentState = LiftState.Idle;
    private Coroutine flashingRoutine;
    private Coroutine checkRoutine;

    private int entitiesInside = 0;

    private void Awake()
    {
        startPos = transform.position;
        endPos = startPos + (Vector3)(moveDirection.normalized * moveDistance);
        UpdateLight();
    }

    private void OnEnable()
    {
        checkRoutine = StartCoroutine(CheckLoop());
    }

    private void OnDisable()
    {
        if (checkRoutine != null)
            StopCoroutine(checkRoutine);
    }

    private IEnumerator CheckLoop()
    {
        var wait = new WaitForSeconds(1f); // configurable interval
        while (true)
        {
            EvaluateLiftState();
            yield return wait;
        }
    }

    public void OnEntityEnterZone() => entitiesInside++;
    public void OnEntityExitZone() => entitiesInside = Mathf.Max(0, entitiesInside - 1);

    public void EvaluateLiftState()
    {
        if (currentState != LiftState.Idle || isLocked || isWall) return;
        if (entitiesInside > 0)
        {
            currentState = LiftState.Warning; // ✅ Immediately change state
            StartCoroutine(LiftSequence());
        }
    }


    private IEnumerator LiftSequence()
    {
        currentState = LiftState.Warning;
        UpdateLight();

        yield return new WaitForSeconds(greenDelay);
        SetLight(greenColor);
        yield return new WaitForSeconds(orangeDelay);
        SetLight(orangeColor);
        yield return new WaitForSeconds(redDelay);
        SetLight(redColor);

        // **New check:** if nobody's aboard, abort
        if (entitiesInside <= 0)
        {
            currentState = LiftState.Idle;
            UpdateLight();
            yield break;
        }
        currentState = LiftState.Moving;
        UpdateLight();

        yield return MoveTo(endPos);
        onOutboundArrival?.Invoke();

        yield return new WaitForSeconds(waitAtTop);
        if (moveDirection == Vector2.down)
        {
            floorCollider.enabled = false;
            lightRenderer.enabled = false;
        }

        yield return MoveTo(startPos);
        onReturnArrival?.Invoke();

        floorCollider.enabled = true;
        lightRenderer.enabled = true;

        currentState = LiftState.Idle;
        UpdateLight();
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        EvaluateLiftState();
    }


    private IEnumerator FlashingRedIdle()
    {
        while (isLocked)
        {
            SetLight(redColor);
            yield return new WaitForSeconds(0.5f);
            SetLight(idleColor);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void UpdateLight()
    {
        if (isLocked) return;

        switch (currentState)
        {
            case LiftState.Idle: SetLight(idleColor); break;
            case LiftState.Warning: SetLight(greenColor); break;
            case LiftState.Moving: SetLight(redColor); break;
        }
    }

    private void SetLight(Color color)
    {
        if (!lightRenderer) return;
        var mat = lightRenderer.material;
        if (mat.HasProperty(colorProperty))
            mat.SetColor(colorProperty, color);
        else
            mat.color = color;
    }
}

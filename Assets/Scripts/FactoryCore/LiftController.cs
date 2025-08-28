using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    // internal
    private LiftState currentState = LiftState.Idle;
    private Coroutine checkRoutine;

    private Transform lightRoot;          // transform we actually move
    private Vector3 lightStartPos;
    private Vector3 lightEndPos;

    private int entitiesInside = 0;

    private void Awake()
    {
        if (!lightRenderer)
        {
            Debug.LogError("LiftController requires a MeshRenderer reference.", this);
            return;
        }

        lightRoot = lightRenderer.transform;
        lightStartPos = lightRoot.position;
        lightEndPos = lightStartPos + (Vector3)(moveDirection.normalized * moveDistance);

        UpdateLight();
    }

    private void OnEnable()
    {
        checkRoutine = StartCoroutine(CheckLoop());
    }

    private void OnDisable()
    {
        if (checkRoutine != null) StopCoroutine(checkRoutine);
    }

    private IEnumerator CheckLoop()
    {
        var wait = new WaitForSeconds(1f);
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
            currentState = LiftState.Warning;
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

        if (entitiesInside <= 0)
        {
            currentState = LiftState.Idle;
            UpdateLight();
            yield break;
        }

        currentState = LiftState.Moving;
        UpdateLight();

        // move ONLY the light
        yield return MoveLightTo(lightEndPos);
        onOutboundArrival?.Invoke();

        yield return new WaitForSeconds(waitAtTop);

        if (moveDirection == Vector2.down)
        {
            if (floorCollider) floorCollider.enabled = false;
            if (lightRenderer) lightRenderer.enabled = false;
        }

        yield return MoveLightTo(lightStartPos);
        onReturnArrival?.Invoke();

        if (floorCollider) floorCollider.enabled = true;
        if (lightRenderer) lightRenderer.enabled = true;

        currentState = LiftState.Idle;
        UpdateLight();
    }

    private IEnumerator MoveLightTo(Vector3 target)
    {
        if (!lightRoot) yield break;

        while (Vector3.Distance(lightRoot.position, target) > 0.01f)
        {
            lightRoot.position = Vector3.MoveTowards(
                lightRoot.position,
                target,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }
        lightRoot.position = target;
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
        if (mat.HasProperty(colorProperty)) mat.SetColor(colorProperty, color);
        else mat.color = color;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        EvaluateLiftState();
    }
}

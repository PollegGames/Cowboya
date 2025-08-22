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
    public Rigidbody2D platformRb;

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

    private Vector2 startPos, endPos;
    private LiftState currentState = LiftState.Idle;
    private Coroutine flashingRoutine;
    private Coroutine checkRoutine;

    private Rigidbody2D rb;
    private Vector2 prevPosition;
    private float prevRotation;
    private readonly HashSet<Rigidbody2D> passengers = new HashSet<Rigidbody2D>();
    private Vector2 moveTarget;
    private bool isMoving = false;

    private int entitiesInside = 0;

    public Vector2 PlatformVelocity { get; private set; }

    private void Awake()
    {
        rb = floorCollider ? floorCollider.attachedRigidbody : platformRb;
        if (rb == null)
        {
            Debug.LogError("LiftController requires a Rigidbody2D reference.", this);
            return;
        }
        rb.isKinematic = true;
        startPos = rb.position;
        endPos = startPos + moveDirection.normalized * moveDistance;
        moveTarget = startPos;
        prevPosition = startPos;
        prevRotation = rb.rotation;
        UpdateLight();
    }

    private void OnEnable()
    {
        passengers.Clear();
        checkRoutine = StartCoroutine(CheckLoop());
    }

    private void OnDisable()
    {
        passengers.Clear();
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

    private IEnumerator MoveTo(Vector2 target)
    {
        moveTarget = target;
        isMoving = true;
        while (isMoving)
            yield return null;
    }

    private void FixedUpdate()
    {
        Vector2 oldPos = rb.position;
        float oldRot = rb.rotation;

        if (isMoving)
        {
            Vector2 newPos = Vector2.MoveTowards(oldPos, moveTarget, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
            rb.MoveRotation(rb.rotation);

            if (Vector2.Distance(newPos, moveTarget) <= 0.0001f)
            {
                rb.MovePosition(moveTarget);
                isMoving = false;
                passengers.Clear();
            }
        }

        Vector2 deltaPos = rb.position - oldPos;
        float deltaRot = rb.rotation - oldRot;
        PlatformVelocity = deltaPos / Time.fixedDeltaTime;

        foreach (var rider in passengers)
        {
            Vector2 riderPos = rider.position + deltaPos;
            if (deltaRot != 0f)
            {
                Vector2 dir = riderPos - rb.position;
                dir = Quaternion.Euler(0f, 0f, deltaRot) * dir;
                riderPos = rb.position + dir;
                rider.MoveRotation(rider.rotation + deltaRot);
            }
            rider.MovePosition(riderPos);
        }

        prevPosition = rb.position;
        prevRotation = rb.rotation;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f && contact.otherCollider == floorCollider)
            {
                if (collision.rigidbody != null)
                    passengers.Add(collision.rigidbody);
                break;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody != null)
            passengers.Remove(collision.rigidbody);
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

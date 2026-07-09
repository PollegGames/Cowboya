using System.Collections.Generic;
using UnityEngine;

public enum JunkConveyorSide
{
    Auto,
    Left,
    Right
}

public class JunkController : MonoBehaviour
{
    [Header("Conveyor Points")]
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform midPoint;
    [SerializeField] private Transform rightPoint;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float speed = 1.5f;
    [SerializeField, Min(0.001f)] private float reachDistance = 0.05f;
    [SerializeField] private bool destroyAtExit = true;

    [Header("Capture")]
    [SerializeField] private bool autoCatchSceneJunk = true;
    [SerializeField, Min(0.01f)] private float catchDistanceFromLine = 0.75f;
    [SerializeField, Min(0.01f)] private float catchScanInterval = 0.15f;
    [SerializeField] private JunkConveyorSide midpointSide = JunkConveyorSide.Auto;

    private readonly List<ControlledJunk> controlledJunk = new List<ControlledJunk>();
    private float nextScanTime;
    private bool sendMidpointLeftNext;

    private struct ControlledJunk
    {
        public JunkPickup Junk;
        public Rigidbody2D Body;
        public Transform Target;
        public RigidbodyType2D OriginalBodyType;
    }

    private void Awake()
    {
        ResolvePointReferences();
    }

    private void FixedUpdate()
    {
        if (autoCatchSceneJunk && Time.time >= nextScanTime)
        {
            CatchNearbyJunk();
            nextScanTime = Time.time + catchScanInterval;
        }

        MoveControlledJunk();
    }

    private void OnDisable()
    {
        for (int i = controlledJunk.Count - 1; i >= 0; i--)
        {
            ReleaseControlledJunk(i, true);
        }
    }

    /// <summary>
    /// Registers a junk object with the conveyor if it is close enough to the conveyor line.
    /// </summary>
    public void RegisterJunk(JunkPickup junk)
    {
        if (!CanControl(junk) || IsControlled(junk) || !TryGetTarget(junk.transform.position, out Transform target))
            return;

        Rigidbody2D body = junk.GetComponent<Rigidbody2D>();
        RigidbodyType2D originalBodyType = body != null ? body.bodyType : RigidbodyType2D.Dynamic;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        junk.OnGrabbed += HandleJunkGrabbed;

        controlledJunk.Add(new ControlledJunk
        {
            Junk = junk,
            Body = body,
            Target = target,
            OriginalBodyType = originalBodyType
        });
    }

    private void CatchNearbyJunk()
    {
        JunkPickup[] junkInScene = FindObjectsByType<JunkPickup>(FindObjectsSortMode.None);
        for (int i = 0; i < junkInScene.Length; i++)
        {
            RegisterJunk(junkInScene[i]);
        }
    }

    private void MoveControlledJunk()
    {
        for (int i = controlledJunk.Count - 1; i >= 0; i--)
        {
            ControlledJunk entry = controlledJunk[i];
            if (entry.Junk == null)
            {
                controlledJunk.RemoveAt(i);
                continue;
            }

            if (entry.Junk.IsHeld)
            {
                ReleaseControlledJunk(i, false);
                continue;
            }

            Vector2 currentPosition = entry.Body != null ? entry.Body.position : (Vector2)entry.Junk.transform.position;
            Vector2 targetPosition = entry.Target.position;
            Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, speed * Time.fixedDeltaTime);

            if (entry.Body != null)
                entry.Body.MovePosition(nextPosition);
            else
                entry.Junk.transform.position = nextPosition;

            if (Vector2.Distance(nextPosition, targetPosition) <= reachDistance)
            {
                JunkPickup reachedJunk = entry.Junk;
                ReleaseControlledJunk(i, false);

                if (destroyAtExit && reachedJunk != null)
                    Destroy(reachedJunk.gameObject);
            }
        }
    }

    private bool CanControl(JunkPickup junk)
    {
        return junk != null
            && !junk.IsHeld
            && leftPoint != null
            && midPoint != null
            && rightPoint != null;
    }

    private bool IsControlled(JunkPickup junk)
    {
        for (int i = 0; i < controlledJunk.Count; i++)
        {
            if (controlledJunk[i].Junk == junk)
                return true;
        }

        return false;
    }

    private bool TryGetTarget(Vector2 position, out Transform target)
    {
        target = null;

        Vector2 left = leftPoint.position;
        Vector2 middle = midPoint.position;
        Vector2 right = rightPoint.position;

        bool closeToLeftSegment = IsCloseToSegment(position, left, middle);
        bool closeToRightSegment = IsCloseToSegment(position, middle, right);
        if (!closeToLeftSegment && !closeToRightSegment)
            return false;

        if (closeToLeftSegment && !closeToRightSegment)
        {
            target = leftPoint;
            return true;
        }

        if (closeToRightSegment && !closeToLeftSegment)
        {
            target = rightPoint;
            return true;
        }

        target = GetMidpointTarget(position);
        return target != null;
    }

    private Transform GetMidpointTarget(Vector2 position)
    {
        if (midpointSide == JunkConveyorSide.Left)
            return leftPoint;

        if (midpointSide == JunkConveyorSide.Right)
            return rightPoint;

        float middleX = midPoint.position.x;
        if (position.x < middleX)
            return leftPoint;

        if (position.x > middleX)
            return rightPoint;

        sendMidpointLeftNext = !sendMidpointLeftNext;
        return sendMidpointLeftNext ? leftPoint : rightPoint;
    }

    private bool IsCloseToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= Mathf.Epsilon)
            return false;

        float projection = Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared;
        if (projection < 0f || projection > 1f)
            return false;

        Vector2 closestPoint = segmentStart + segment * projection;
        return Vector2.Distance(point, closestPoint) <= catchDistanceFromLine;
    }

    private void HandleJunkGrabbed(JunkPickup junk)
    {
        for (int i = controlledJunk.Count - 1; i >= 0; i--)
        {
            if (controlledJunk[i].Junk == junk)
            {
                ReleaseControlledJunk(i, true);
                return;
            }
        }
    }

    private void ReleaseControlledJunk(int index, bool restoreBodyType)
    {
        ControlledJunk entry = controlledJunk[index];
        controlledJunk.RemoveAt(index);

        if (entry.Junk != null)
            entry.Junk.OnGrabbed -= HandleJunkGrabbed;

        if (restoreBodyType && entry.Body != null)
            entry.Body.bodyType = entry.OriginalBodyType;
    }

    private void ResolvePointReferences()
    {
        if (leftPoint == null)
            leftPoint = transform.Find("LeftPoint");

        if (midPoint == null)
            midPoint = transform.Find("MidPoint");

        if (rightPoint == null)
            rightPoint = transform.Find("RightPoint");
    }

    private void OnDrawGizmosSelected()
    {
        ResolvePointReferences();

        if (leftPoint == null || midPoint == null || rightPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(leftPoint.position, midPoint.position);
        Gizmos.DrawLine(midPoint.position, rightPoint.position);
        Gizmos.DrawWireSphere(leftPoint.position, reachDistance);
        Gizmos.DrawWireSphere(rightPoint.position, reachDistance);
    }
}

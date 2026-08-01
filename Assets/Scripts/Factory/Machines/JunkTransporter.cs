using System.Collections.Generic;
using UnityEngine;

public class JunkTransporter : MonoBehaviour
{
    private const float TransportedLocalZ = 0f;

    [Header("Path Points")]
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform midPointLeft;
    [SerializeField] private Transform rightPoint;
    [SerializeField] private Transform midPointRight;

    [Header("Junk")]
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private GameObject[] junkPrefabs;
    [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
    [SerializeField, Min(0.001f)] private float reachDistance = 0.05f;

    [Header("Capture")]
    [SerializeField] private bool autoCatchSceneJunk = true;
    [SerializeField, Min(0.01f)] private float catchDistanceFromLine = 0.75f;
    [SerializeField, Min(0.01f)] private float catchScanInterval = 0.15f;

    [Header("Spawning")]
    [SerializeField, Min(0.01f)] private float minimumSpawnInterval = 1f;
    [SerializeField, Min(0.01f)] private float maximumSpawnInterval = 3f;
    [SerializeField, Min(1)] private int maximumActiveJunk = 12;

    private readonly List<TransportedJunk> activeJunk = new List<TransportedJunk>();
    private float nextSpawnTime;
    private float nextCatchScanTime;

    private class TransportedJunk
    {
        public GameObject GameObject;
        public Transform Transform;
        public Transform Destination;
        public Rigidbody2D Body;
        public JunkPickup Pickup;
        public MoveWithPlayerPosition PlayerMovement;
        public Vector3 PathPosition;
    }

    private void Awake()
    {
        ResolvePointReferences();
        ResolveRoomManager();
    }

    private void OnEnable()
    {
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (autoCatchSceneJunk && Time.time >= nextCatchScanTime)
        {
            CatchNearbyJunk();
            nextCatchScanTime = Time.time + catchScanInterval;
        }

        if (Time.time >= nextSpawnTime)
        {
            if (activeJunk.Count < maximumActiveJunk)
                SpawnJunk();

            ScheduleNextSpawn();
        }

        MoveJunk();
    }

    private void OnDisable()
    {
        for (int i = activeJunk.Count - 1; i >= 0; i--)
            RemoveJunk(i, true);
    }

    /// <summary>
    /// Spawns a random junk prefab at a random side of the transporter.
    /// </summary>
    public void SpawnJunk()
    {
        if (!HasValidConfiguration())
            return;

        bool spawnOnLeft = Random.value < 0.5f;
        Transform spawnPoint = spawnOnLeft ? leftPoint : rightPoint;
        Transform destination = spawnOnLeft ? midPointLeft : midPointRight;
        GameObject prefab = GetRandomPrefab();

        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, transform);
        MoveWithPlayerPosition playerMovement = instance.GetComponent<MoveWithPlayerPosition>();
        if (playerMovement != null)
        {
            playerMovement.roomManager = roomManager;
            playerMovement.SetBaseLocalPosition(instance.transform.localPosition);
        }

        Vector3 localPosition = instance.transform.localPosition;
        localPosition.z = TransportedLocalZ;
        instance.transform.localPosition = localPosition;
        if (playerMovement != null)
            playerMovement.SetBaseLocalPosition(localPosition);

        Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
        }

        JunkPickup pickup = instance.GetComponent<JunkPickup>();
        if (pickup != null)
        {
            pickup.SetConveyorControlled(true);
            pickup.OnGrabbed += HandleJunkGrabbed;
        }

        activeJunk.Add(new TransportedJunk
        {
            GameObject = instance,
            Transform = instance.transform,
            Destination = destination,
            Body = body,
            Pickup = pickup,
            PlayerMovement = playerMovement,
            PathPosition = localPosition
        });
    }

    /// <summary>
    /// Registers loose junk with the nearest transporter line when it is close enough.
    /// </summary>
    public void RegisterJunk(JunkPickup pickup)
    {
        if (pickup == null
            || pickup.IsHeld
            || pickup.IsConveyorControlled
            || IsTransporting(pickup)
            || !TryGetDestination(pickup.transform.position, out Transform destination))
        {
            return;
        }

        Rigidbody2D body = pickup.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
        }

        MoveWithPlayerPosition playerMovement = pickup.GetComponent<MoveWithPlayerPosition>();
        if (playerMovement != null)
        {
            playerMovement.roomManager = roomManager;
            playerMovement.enabled = true;
        }

        pickup.transform.SetParent(transform, true);
        Vector3 pathPosition = pickup.transform.localPosition;
        pathPosition.z = TransportedLocalZ;
        pickup.transform.localPosition = pathPosition;
        if (playerMovement != null)
            playerMovement.RebaseLocalPosition(pathPosition);

        pickup.SetConveyorControlled(true);
        pickup.OnGrabbed += HandleJunkGrabbed;
        activeJunk.Add(new TransportedJunk
        {
            GameObject = pickup.gameObject,
            Transform = pickup.transform,
            Destination = destination,
            Body = body,
            Pickup = pickup,
            PlayerMovement = playerMovement,
            PathPosition = pathPosition
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

    private bool IsTransporting(JunkPickup pickup)
    {
        for (int i = 0; i < activeJunk.Count; i++)
        {
            if (activeJunk[i].Pickup == pickup)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetDestination(Vector2 position, out Transform destination)
    {
        destination = null;
        if (leftPoint == null || midPointLeft == null || rightPoint == null || midPointRight == null)
        {
            return false;
        }

        float leftDistance = DistanceToSegment(position, leftPoint.position, midPointLeft.position);
        float rightDistance = DistanceToSegment(position, rightPoint.position, midPointRight.position);
        float nearestDistance = Mathf.Min(leftDistance, rightDistance);
        if (nearestDistance > catchDistanceFromLine)
        {
            return false;
        }

        destination = leftDistance <= rightDistance ? midPointLeft : midPointRight;
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, segmentStart);
        }

        float projection = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared);
        Vector2 closestPoint = segmentStart + segment * projection;
        return Vector2.Distance(point, closestPoint);
    }

    private void MoveJunk()
    {
        for (int i = activeJunk.Count - 1; i >= 0; i--)
        {
            TransportedJunk junk = activeJunk[i];
            if (junk.GameObject == null)
            {
                activeJunk.RemoveAt(i);
                continue;
            }

            Vector3 destinationPosition = junk.Destination.localPosition;
            destinationPosition.z = TransportedLocalZ;
            junk.PathPosition = Vector3.MoveTowards(
                junk.PathPosition,
                destinationPosition,
                moveSpeed * Time.deltaTime);
            if (junk.PlayerMovement != null)
                junk.PlayerMovement.SetBaseLocalPosition(junk.PathPosition);
            else
                junk.Transform.localPosition = junk.PathPosition;

            if (Vector3.Distance(junk.PathPosition, destinationPosition) <= reachDistance)
                RemoveJunk(i, true);
        }
    }

    private void HandleJunkGrabbed(JunkPickup pickup)
    {
        for (int i = activeJunk.Count - 1; i >= 0; i--)
        {
            if (activeJunk[i].Pickup != pickup)
                continue;

            RemoveJunk(i, false);
            return;
        }
    }

    private void RemoveJunk(int index, bool destroyJunk)
    {
        TransportedJunk junk = activeJunk[index];
        activeJunk.RemoveAt(index);

        if (junk.Pickup != null)
        {
            junk.Pickup.OnGrabbed -= HandleJunkGrabbed;
            junk.Pickup.SetConveyorControlled(false);
        }

        if (destroyJunk)
        {
            if (junk.GameObject != null)
                Destroy(junk.GameObject);
        }
        else
        {
            if (junk.PlayerMovement != null)
                junk.PlayerMovement.enabled = false;

            if (junk.Body != null)
                junk.Body.simulated = true;
        }
    }

    private GameObject GetRandomPrefab()
    {
        int startIndex = Random.Range(0, junkPrefabs.Length);
        for (int offset = 0; offset < junkPrefabs.Length; offset++)
        {
            GameObject prefab = junkPrefabs[(startIndex + offset) % junkPrefabs.Length];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private bool HasValidConfiguration()
    {
        return leftPoint != null
            && midPointLeft != null
            && rightPoint != null
            && midPointRight != null
            && junkPrefabs != null
            && junkPrefabs.Length > 0;
    }

    private void ScheduleNextSpawn()
    {
        float minimum = Mathf.Min(minimumSpawnInterval, maximumSpawnInterval);
        float maximum = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
        nextSpawnTime = Time.time + Random.Range(minimum, maximum);
    }

    private void ResolvePointReferences()
    {
        if (leftPoint == null)
            leftPoint = transform.Find("LeftPoint");

        if (midPointLeft == null)
            midPointLeft = transform.Find("MidPointLeft");

        if (rightPoint == null)
            rightPoint = transform.Find("RightPoint");

        if (midPointRight == null)
            midPointRight = transform.Find("MidPointRight");
    }

    private void ResolveRoomManager()
    {
        if (roomManager == null)
            roomManager = GetComponentInParent<RoomManager>();
    }

    private void OnDrawGizmosSelected()
    {
        ResolvePointReferences();
        ResolveRoomManager();

        Gizmos.color = Color.yellow;
        if (leftPoint != null && midPointLeft != null)
            Gizmos.DrawLine(leftPoint.position, midPointLeft.position);

        if (rightPoint != null && midPointRight != null)
            Gizmos.DrawLine(rightPoint.position, midPointRight.position);
    }
}

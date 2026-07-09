using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadRobotSpawner : MonoBehaviour
{
    private static readonly string[] SpawnPointNames =
    {
        "SpawnPoint",
        "Spawn Point",
        "SpawnPoiint",
        "DeadRobotSpawnPoint",
        "Dead Robot Spawn Point"
    };

    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> robotPrefabs = new List<GameObject>();
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private int maxSpawnedBodies = 20;
    [SerializeField] private bool randomRobot = true;

    [Header("Placement")]
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private Transform spawnedParent;
    [SerializeField] private Vector2 randomOffset = new Vector2(1f, 0.25f);
    [SerializeField] private bool matchSpawnerRotation;

    [Header("Spawn Impulse")]
    [SerializeField] private bool applySpawnImpulse = true;
    [SerializeField] private Vector2 horizontalImpulseRange = new Vector2(2f, 5f);
    [SerializeField] private Vector2 verticalImpulseRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 angularImpulseRange = new Vector2(-30f, 30f);
    [SerializeField, Range(0f, 1f)] private float childImpulseScale = 0.25f;

    private readonly List<GameObject> spawnedBodies = new List<GameObject>();
    private Coroutine spawnRoutine;
    private int nextPrefabIndex;
    private bool warnedMissingPrefabs;

    private void Awake()
    {
        ResolveSpawnOrigin();
    }

    private void OnEnable()
    {
        ResolveSpawnOrigin();

        if (spawnOnStart)
            SpawnNow();

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    /// <summary>
    /// Spawns one robot body and immediately puts it in the dead state.
    /// </summary>
    public void SpawnNow()
    {
        CleanupMissingBodies();

        if (maxSpawnedBodies > 0 && spawnedBodies.Count >= maxSpawnedBodies)
            return;

        GameObject prefab = SelectPrefab();
        if (prefab == null)
            return;

        Transform origin = ResolveSpawnOrigin();
        Transform parent = spawnedParent != null ? spawnedParent : transform.parent;
        Vector3 position = origin.position + new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(-randomOffset.y, randomOffset.y),
            0f);
        Quaternion rotation = matchSpawnerRotation ? origin.rotation : Quaternion.identity;

        GameObject body = Instantiate(prefab, position, rotation, parent);
        PrepareDeadBody(body);
        ApplySpawnImpulse(body);
        spawnedBodies.Add(body);
    }

    private Transform ResolveSpawnOrigin()
    {
        if (spawnOrigin != null)
            return spawnOrigin;

        foreach (string spawnPointName in SpawnPointNames)
        {
            Transform child = transform.Find(spawnPointName);
            if (child != null)
            {
                spawnOrigin = child;
                return spawnOrigin;
            }
        }

        return transform;
    }

    private IEnumerator SpawnRoutine()
    {
        float delay = Mathf.Max(0.1f, spawnInterval);
        while (enabled)
        {
            yield return new WaitForSeconds(delay);
            SpawnNow();
        }
    }

    private GameObject SelectPrefab()
    {
        if (robotPrefabs == null || robotPrefabs.Count == 0)
        {
            if (!warnedMissingPrefabs)
            {
                warnedMissingPrefabs = true;
                Debug.LogWarning($"{nameof(DeadRobotSpawner)} '{name}' has no robot prefabs assigned.", this);
            }
            return null;
        }

        if (randomRobot)
            return robotPrefabs[Random.Range(0, robotPrefabs.Count)];

        GameObject prefab = robotPrefabs[nextPrefabIndex % robotPrefabs.Count];
        nextPrefabIndex++;
        return prefab;
    }

    private static void PrepareDeadBody(GameObject body)
    {
        if (body == null)
            return;

        RobotStateController state = body.GetComponent<RobotStateController>();
        if (state != null)
        {
            if (state.Stats == null)
                state.Stats = new EnemyRobotFactory().CreateRobot();

            state.SetInitialDeadState();
            return;
        }

        JointBreaker jointBreaker = body.GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
    }

    private void ApplySpawnImpulse(GameObject body)
    {
        if (!applySpawnImpulse || body == null)
            return;

        Rigidbody2D[] rigidbodies = body.GetComponentsInChildren<Rigidbody2D>();
        if (rigidbodies.Length == 0)
            return;

        float direction = Random.value < 0.5f ? -1f : 1f;
        Rigidbody2D mainBody = body.GetComponent<Rigidbody2D>();
        if (mainBody == null)
            mainBody = rigidbodies[0];

        ApplyImpulse(mainBody, CreateImpulse(direction, 1f), RandomRange(angularImpulseRange));

        if (childImpulseScale <= 0f)
            return;

        foreach (Rigidbody2D rigidbody in rigidbodies)
        {
            if (rigidbody == null || rigidbody == mainBody)
                continue;

            float variation = Random.Range(0.5f, 1f);
            Vector2 impulse = CreateImpulse(direction, childImpulseScale * variation);
            float angularImpulse = RandomRange(angularImpulseRange) * childImpulseScale;
            ApplyImpulse(rigidbody, impulse, angularImpulse);
        }
    }

    private Vector2 CreateImpulse(float direction, float scale)
    {
        float horizontalImpulse = RandomRange(horizontalImpulseRange) * direction;
        float verticalImpulse = RandomRange(verticalImpulseRange);
        return new Vector2(horizontalImpulse, verticalImpulse) * scale;
    }

    private static void ApplyImpulse(Rigidbody2D rigidbody, Vector2 impulse, float angularImpulse)
    {
        if (rigidbody == null || rigidbody.bodyType != RigidbodyType2D.Dynamic)
            return;

        rigidbody.AddForce(impulse, ForceMode2D.Impulse);
        rigidbody.AddTorque(angularImpulse, ForceMode2D.Impulse);
    }

    private static float RandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max);
    }

    private void CleanupMissingBodies()
    {
        for (int i = spawnedBodies.Count - 1; i >= 0; i--)
        {
            if (spawnedBodies[i] == null)
                spawnedBodies.RemoveAt(i);
        }
    }
}

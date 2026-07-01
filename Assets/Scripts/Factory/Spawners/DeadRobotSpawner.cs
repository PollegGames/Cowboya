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

    private void CleanupMissingBodies()
    {
        for (int i = spawnedBodies.Count - 1; i >= 0; i--)
        {
            if (spawnedBodies[i] == null)
                spawnedBodies.RemoveAt(i);
        }
    }
}

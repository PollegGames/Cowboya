using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Presents the free cubes owned by one laboratory visit as loose physics objects.
/// </summary>
[DisallowMultipleComponent]
public sealed class LaboratoryCollectedCubeSpawner : MonoBehaviour {
    private static readonly LaboratoryCubeType[] SpawnOrder = {
        LaboratoryCubeType.White,
        LaboratoryCubeType.MaxHealth,
        LaboratoryCubeType.MaxEnergy,
        LaboratoryCubeType.EnergyRecharge,
        LaboratoryCubeType.AttackDamage
    };

    [Header("Cube Prefabs")]
    [SerializeField] private CubePickup whiteCubePrefab;
    [SerializeField] private CubePickup maxHealthCubePrefab;
    [SerializeField] private CubePickup maxEnergyCubePrefab;
    [SerializeField] private CubePickup energyRechargeCubePrefab;
    [SerializeField] private CubePickup attackDamageCubePrefab;

    [Header("Drop")]
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private Transform spawnedCubeParent;
    [SerializeField, Min(0f)] private float fallHeight = 2f;
    [SerializeField, Min(0f)] private float horizontalScatter = 0.25f;
    [SerializeField] private float spawnDepth = 0.01f;
    [SerializeField, Min(0f)] private float spawnIntervalSeconds = 0.08f;

    private readonly int[] remainingSnapshotCounts = new int[SpawnOrder.Length];
    private Coroutine spawnRoutine;
    private int initializedVisitId = -1;
    private int nextTypeIndex;
    private long snapshotCubeCount;
    private long spawnedCubeCount;
    private bool isSpawning;

    public int InitializedVisitId => initializedVisitId;
    public long SnapshotCubeCount => snapshotCubeCount;
    public long SpawnedCubeCount => spawnedCubeCount;
    public long RemainingCubeCount => Math.Max(0L, snapshotCubeCount - spawnedCubeCount);
    public bool IsSpawning => isSpawning;

    /// <summary>
    /// Assigns the exact cube prefabs and presentation settings used by this chute.
    /// </summary>
    public void Configure(
        Transform origin,
        Transform cubeParent,
        CubePickup whitePrefab,
        CubePickup maxHealthPrefab,
        CubePickup maxEnergyPrefab,
        CubePickup energyRechargePrefab,
        CubePickup attackDamagePrefab,
        float intervalSeconds = 0.08f,
        float height = 2f,
        float scatter = 0.25f,
        float depth = 0.01f) {
        spawnOrigin = origin;
        spawnedCubeParent = cubeParent;
        whiteCubePrefab = whitePrefab;
        maxHealthCubePrefab = maxHealthPrefab;
        maxEnergyCubePrefab = maxEnergyPrefab;
        energyRechargeCubePrefab = energyRechargePrefab;
        attackDamageCubePrefab = attackDamagePrefab;
        spawnIntervalSeconds = Mathf.Max(0f, intervalSeconds);
        fallHeight = Mathf.Max(0f, height);
        horizontalScatter = Mathf.Max(0f, scatter);
        spawnDepth = depth;
    }

    /// <summary>
    /// Snapshots and presents all laboratory-free cubes for the active visit.
    /// Repeating the same visit does not create another snapshot or duplicate cubes.
    /// </summary>
    public bool InitializeForVisit(LaboratoryProgress progress, int visitId) {
        if (progress == null) {
            Debug.LogError(
                $"{nameof(LaboratoryCollectedCubeSpawner)} requires laboratory progress.",
                this);
            return false;
        }

        if (visitId < 0
            || !progress.HasActiveVisit
            || progress.ActiveVisitId != visitId) {
            Debug.LogError(
                $"{nameof(LaboratoryCollectedCubeSpawner)} can only initialize for "
                + "the active laboratory visit.",
                this);
            return false;
        }

        if (initializedVisitId == visitId) {
            ResumeSnapshotIfNeeded();
            return true;
        }

        int[] snapshot = new int[SpawnOrder.Length];
        long snapshotTotal = 0L;
        bool hasMissingPrefab = false;
        for (int i = 0; i < SpawnOrder.Length; i++) {
            int count = Math.Max(0, progress.GetLaboratoryFreeCubeCount(SpawnOrder[i]));
            snapshot[i] = count;
            snapshotTotal += count;

            if (count > 0 && GetPrefab(SpawnOrder[i]) == null) {
                hasMissingPrefab = true;
                Debug.LogError(
                    $"{nameof(LaboratoryCollectedCubeSpawner)} is missing the "
                    + $"{SpawnOrder[i]} cube prefab required by visit {visitId}.",
                    this);
            }
        }

        if (hasMissingPrefab) {
            return false;
        }

        StopSpawnRoutine();
        Array.Copy(snapshot, remainingSnapshotCounts, snapshot.Length);
        initializedVisitId = visitId;
        nextTypeIndex = 0;
        snapshotCubeCount = snapshotTotal;
        spawnedCubeCount = 0L;

        ResumeSnapshotIfNeeded();
        return true;
    }

    private void OnEnable() {
        ResumeSnapshotIfNeeded();
    }

    private void OnDisable() {
        StopSpawnRoutine();
    }

    private void ResumeSnapshotIfNeeded() {
        if (!isActiveAndEnabled || isSpawning || RemainingCubeCount <= 0L) {
            return;
        }

        isSpawning = true;
        spawnRoutine = StartCoroutine(SpawnSnapshotRoutine(initializedVisitId));
    }

    private IEnumerator SpawnSnapshotRoutine(int visitId) {
        while (isActiveAndEnabled && initializedVisitId == visitId) {
            if (!TrySpawnNextCube()) {
                break;
            }

            if (spawnIntervalSeconds > 0f) {
                yield return new WaitForSeconds(spawnIntervalSeconds);
            }
            else {
                yield return null;
            }
        }

        isSpawning = false;
        spawnRoutine = null;
    }

    private bool TrySpawnNextCube() {
        if (!TryFindNextTypeIndex(out int typeIndex)) {
            return false;
        }

        LaboratoryCubeType type = SpawnOrder[typeIndex];
        CubePickup prefab = GetPrefab(type);
        if (prefab == null) {
            Debug.LogError(
                $"{nameof(LaboratoryCollectedCubeSpawner)} lost its {type} cube prefab "
                + "while presenting the visit snapshot.",
                this);
            return false;
        }

        Transform origin = spawnOrigin != null ? spawnOrigin : transform;
        Vector3 spawnPosition = origin.position;
        spawnPosition += Vector3.up * fallHeight;
        spawnPosition += Vector3.right * UnityEngine.Random.Range(
            -horizontalScatter,
            horizontalScatter);
        spawnPosition.z = spawnDepth;

        CubePickup cube = Instantiate(
            prefab,
            spawnPosition,
            prefab.transform.rotation,
            spawnedCubeParent);
        PrepareAsFreeCube(cube);

        remainingSnapshotCounts[typeIndex]--;
        nextTypeIndex = (typeIndex + 1) % SpawnOrder.Length;
        spawnedCubeCount++;
        return true;
    }

    private bool TryFindNextTypeIndex(out int typeIndex) {
        for (int offset = 0; offset < SpawnOrder.Length; offset++) {
            int index = (nextTypeIndex + offset) % SpawnOrder.Length;
            if (remainingSnapshotCounts[index] > 0) {
                typeIndex = index;
                return true;
            }
        }

        typeIndex = -1;
        return false;
    }

    private CubePickup GetPrefab(LaboratoryCubeType type) {
        switch (type) {
            case LaboratoryCubeType.White:
                return whiteCubePrefab;
            case LaboratoryCubeType.MaxHealth:
                return maxHealthCubePrefab;
            case LaboratoryCubeType.MaxEnergy:
                return maxEnergyCubePrefab;
            case LaboratoryCubeType.EnergyRecharge:
                return energyRechargeCubePrefab;
            case LaboratoryCubeType.AttackDamage:
                return attackDamageCubePrefab;
            default:
                return null;
        }
    }

    private static void PrepareAsFreeCube(CubePickup cube) {
        if (cube == null) {
            return;
        }

        TargetJoint2D targetJoint = cube.GetComponent<TargetJoint2D>();
        if (targetJoint != null) {
            targetJoint.enabled = false;
        }

        Rigidbody2D body = cube.GetComponent<Rigidbody2D>();
        if (body == null) {
            return;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }

    private void StopSpawnRoutine() {
        if (spawnRoutine != null) {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        isSpawning = false;
    }
}

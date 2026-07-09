using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum SpawnGarbagePanelAxis
{
    LocalX,
    LocalY,
    LocalZ
}

public class SpawnGarbageController : MonoBehaviour
{
    private const string DefaultPanelName = "SpawnGarbage Bottom";
    private const string JunkResourcesPath = "Prefabs/IntereableObjects";

    [Header("Panel")]
    [SerializeField] private Transform panelTransform;
    [SerializeField] private SpawnGarbagePanelAxis retractAxis = SpawnGarbagePanelAxis.LocalX;
    [SerializeField, Range(0.01f, 1f)] private float openScaleMultiplier = 0.05f;

    [FormerlySerializedAs("openLocalPositionOffset")]
    [HideInInspector]
    [SerializeField] private Vector3 openPositionOffset;
    [SerializeField] private float openWorldUpOffset = 1f;

    [Header("Timing")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float initialDelay = 0f;
    [SerializeField, Min(0.1f)] private float cycleInterval = 5f;
    [SerializeField, Min(0.01f)] private float retractDuration = 0.35f;
    [SerializeField, Min(0f)] private float openHoldDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float stretchDuration = 0.35f;

    [Header("Junk Spawning")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform spawnedParent;
    [SerializeField, Min(0)] private int minJunkToSpawn = 2;
    [SerializeField, Min(0)] private int maxJunkToSpawn = 3;
    [SerializeField, Min(0f)] private float spawnScatterRadius = 0.35f;
    [SerializeField] private GameObject[] junkPrefabs;

    private static GameObject[] cachedResourceJunkPrefabs;

    private Coroutine cycleRoutine;
    private Coroutine oneShotRoutine;
    private Vector3 closedLocalPosition;
    private Vector3 closedLocalScale;
    private bool initialized;
    private bool isCycling;

    public event Action OnPanelOpenReady;

    public bool IsCycling => isCycling;

    private void Awake()
    {
        InitializePanelState();
    }

    private void OnEnable()
    {
        InitializePanelState();

        if (playOnEnable)
            cycleRoutine = StartCoroutine(CyclePanelRoutine());
    }

    private void OnDisable()
    {
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }

        if (oneShotRoutine != null)
        {
            StopCoroutine(oneShotRoutine);
            oneShotRoutine = null;
        }

        isCycling = false;
        ResetPanel();
    }

    /// <summary>
    /// Plays one retract and stretch cycle if no cycle is already running.
    /// </summary>
    public void PlayCycle()
    {
        if (!isActiveAndEnabled || isCycling)
            return;

        oneShotRoutine = StartCoroutine(PlayPanelCycleRoutine());
    }

    /// <summary>
    /// Restores the panel to the closed position and scale captured at startup.
    /// </summary>
    public void ResetPanel()
    {
        if (!initialized || panelTransform == null)
            return;

        panelTransform.localPosition = closedLocalPosition;
        panelTransform.localScale = closedLocalScale;
    }

    private IEnumerator CyclePanelRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (enabled)
        {
            yield return PlayPanelCycleRoutine();
            yield return new WaitForSeconds(cycleInterval);
        }
    }

    private IEnumerator PlayPanelCycleRoutine()
    {
        InitializePanelState();

        if (panelTransform == null)
            yield break;

        isCycling = true;

        yield return AnimatePanelRoutine(0f, 1f, retractDuration);
        SpawnRandomJunkBatch();
        OnPanelOpenReady?.Invoke();

        if (openHoldDuration > 0f)
            yield return new WaitForSeconds(openHoldDuration);

        yield return AnimatePanelRoutine(1f, 0f, stretchDuration);

        ResetPanel();
        isCycling = false;
        oneShotRoutine = null;
    }

    private IEnumerator AnimatePanelRoutine(float fromOpenAmount, float toOpenAmount, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            float normalizedTime = elapsed / safeDuration;
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            float openAmount = Mathf.Lerp(fromOpenAmount, toOpenAmount, easedTime);

            ApplyPanelOpenAmount(openAmount);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPanelOpenAmount(toOpenAmount);
    }

    private void ApplyPanelOpenAmount(float openAmount)
    {
        float clampedOpenAmount = Mathf.Clamp01(openAmount);
        float closedAxisScale = GetAxisValue(closedLocalScale);
        float openAxisScale = closedAxisScale * openScaleMultiplier;
        float currentAxisScale = Mathf.Lerp(closedAxisScale, openAxisScale, clampedOpenAmount);

        Vector3 scale = closedLocalScale;
        SetAxisValue(ref scale, currentAxisScale);

        panelTransform.localScale = scale;
        Vector3 localOffset = Vector3.Lerp(Vector3.zero, openPositionOffset, clampedOpenAmount);
        Vector3 worldUpOffset = Vector3.up * Mathf.Lerp(0f, openWorldUpOffset, clampedOpenAmount);

        panelTransform.localPosition = closedLocalPosition + localOffset;
        panelTransform.position += worldUpOffset;
    }

    private void InitializePanelState()
    {
        if (initialized)
            return;

        ResolvePanelTransform();

        if (panelTransform == null)
        {
            Debug.LogWarning($"{nameof(SpawnGarbageController)} on '{name}' could not find a panel transform.", this);
            return;
        }

        closedLocalPosition = panelTransform.localPosition;
        closedLocalScale = panelTransform.localScale;
        initialized = true;
    }

    private void SpawnRandomJunkBatch()
    {
        GameObject[] availablePrefabs = GetAvailableJunkPrefabs();
        if (availablePrefabs.Length == 0)
        {
            Debug.LogWarning($"{nameof(SpawnGarbageController)} on '{name}' has no junk prefabs available.", this);
            return;
        }

        int lowCount = Mathf.Max(0, Mathf.Min(minJunkToSpawn, maxJunkToSpawn));
        int highCount = Mathf.Max(lowCount, Mathf.Max(minJunkToSpawn, maxJunkToSpawn));
        int spawnCount = Mathf.Min(UnityEngine.Random.Range(lowCount, highCount + 1), availablePrefabs.Length);
        if (spawnCount <= 0)
            return;

        Transform origin = ResolveSpawnPoint();
        Transform parent = spawnedParent != null ? spawnedParent : transform.parent;
        List<GameObject> choices = new List<GameObject>(availablePrefabs);

        for (int i = 0; i < spawnCount; i++)
        {
            int prefabIndex = UnityEngine.Random.Range(0, choices.Count);
            GameObject prefab = choices[prefabIndex];
            choices.RemoveAt(prefabIndex);

            if (prefab == null)
                continue;

            Vector2 scatter = UnityEngine.Random.insideUnitCircle * spawnScatterRadius;
            Vector3 spawnPosition = origin.position + new Vector3(scatter.x, scatter.y, 0f);
            Instantiate(prefab, spawnPosition, prefab.transform.rotation, parent);
        }
    }

    private GameObject[] GetAvailableJunkPrefabs()
    {
        if (junkPrefabs != null && junkPrefabs.Length > 0)
            return FilterJunkPrefabs(junkPrefabs);

        if (cachedResourceJunkPrefabs == null)
            cachedResourceJunkPrefabs = FilterJunkPrefabs(Resources.LoadAll<GameObject>(JunkResourcesPath));

        return cachedResourceJunkPrefabs;
    }

    private static GameObject[] FilterJunkPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return new GameObject[0];

        List<GameObject> filteredPrefabs = new List<GameObject>();
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null && prefab.GetComponent<JunkPickup>() != null)
                filteredPrefabs.Add(prefab);
        }

        return filteredPrefabs.ToArray();
    }

    private Transform ResolveSpawnPoint()
    {
        if (spawnPoint != null)
            return spawnPoint;

        Transform foundSpawnPoint = transform.Find("SpawnPoint");
        if (foundSpawnPoint == null)
            foundSpawnPoint = transform.Find("Spawn Point");

        spawnPoint = foundSpawnPoint != null ? foundSpawnPoint : transform;
        return spawnPoint;
    }

    private void ResolvePanelTransform()
    {
        if (panelTransform != null)
            return;

        Transform foundPanel = transform.Find(DefaultPanelName);
        if (foundPanel != null)
        {
            panelTransform = foundPanel;
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.Equals(DefaultPanelName, StringComparison.OrdinalIgnoreCase))
            {
                panelTransform = child;
                return;
            }
        }
    }

    private float GetAxisValue(Vector3 value)
    {
        switch (retractAxis)
        {
            case SpawnGarbagePanelAxis.LocalY:
                return value.y;
            case SpawnGarbagePanelAxis.LocalZ:
                return value.z;
            default:
                return value.x;
        }
    }

    private void SetAxisValue(ref Vector3 value, float axisValue)
    {
        switch (retractAxis)
        {
            case SpawnGarbagePanelAxis.LocalY:
                value.y = axisValue;
                break;
            case SpawnGarbagePanelAxis.LocalZ:
                value.z = axisValue;
                break;
            default:
                value.x = axisValue;
                break;
        }
    }
}

using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the reusable Part 4 capabilities to the approved room and machine prefabs.
/// </summary>
[InitializeOnLoad]
public static class WorkerCollectorPart4PrefabBuilder
{
    private const string GarageMachinePath =
        "Assets/Resources/Prefabs/Map/Basic/Machines/GarageCubes.prefab";
    private const string RestRoomPath = "Assets/Resources/Prefabs/Map/ROOM_resting.prefab";
    private const string ConveyorRoomPath = "Assets/Resources/Prefabs/Map/ROOM_Conveyor.prefab";
    private const string WorkerCollectorPath =
        "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";

    static WorkerCollectorPart4PrefabBuilder()
    {
        EditorApplication.delayCall += ApplyIfRequired;
    }

    [MenuItem("Tools/CowBoya/Apply Worker Collector Part 4 Prefabs")]
    public static void Apply()
    {
        ConfigureGarageMachine();
        ConfigureRestRoom();
        RemoveTemporaryConveyorDropOff();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Worker Collector Part 4 reusable prefabs are configured.");
    }

    private static void ApplyIfRequired()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        GameObject garage = AssetDatabase.LoadAssetAtPath<GameObject>(GarageMachinePath);
        GameObject rest = AssetDatabase.LoadAssetAtPath<GameObject>(RestRoomPath);
        if (garage == null || rest == null)
            return;
        if (garage.GetComponent<GarageCubeStorage>() != null
            && rest.GetComponent<WorkerCollectorSpawnRestProvider>() != null)
            return;
        Apply();
    }

    private static void ConfigureGarageMachine()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GarageMachinePath);
        if (root == null)
            throw new InvalidOperationException("Garage machine prefab could not be loaded.");
        try
        {
            Transform slotsRoot = FindOrCreate(root.transform, "Slots");
            Transform[] slots = new Transform[GarageCubeStorage.Capacity];
            const float horizontalSpacing = 2.8f;
            const float verticalSpacing = 2.8f;
            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = FindOrCreate(slotsRoot, $"Slot_{i:00}");
                int column = i % 3;
                int row = i / 3;
                ConfigureMarker(slot, new Vector3(
                    (column - 1) * horizontalSpacing, -0.1f, (row - 1) * verticalSpacing));
                slots[i] = slot;
            }

            Transform deliveryPoint = FindOrCreate(root.transform, "DeliveryPoint");
            ConfigureMarker(deliveryPoint, new Vector3(0f, 0f, -5.6f));
            Transform waitPoint = FindOrCreate(root.transform, "WorkerWaitPoint");
            ConfigureMarker(waitPoint, new Vector3(0f, 0f, -6.8f));

            GarageCubeStorage storage = GetOrAdd<GarageCubeStorage>(root);
            storage.ConfigureSlots(slots);
            GarageDoorController door = root.GetComponent<GarageDoorController>();
            if (door == null)
                throw new InvalidOperationException("The approved garage door controller is missing.");
            GarageCubeProcessor processor = GetOrAdd<GarageCubeProcessor>(root);
            processor.Configure(storage, door, 0.5f);
            GetOrAdd<GarageCubeCoordinator>(root);
            WorkerCollectorDropOffProvider provider = GetOrAdd<WorkerCollectorDropOffProvider>(root);
            provider.Configure(null, deliveryPoint, waitPoint, storage, processor);

            GarageCubeConveyorController incompatible = root.GetComponent<GarageCubeConveyorController>();
            if (incompatible != null)
                UnityEngine.Object.DestroyImmediate(incompatible);

            Save(root, GarageMachinePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureRestRoom()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RestRoomPath);
        if (root == null)
            throw new InvalidOperationException("Rest room prefab could not be loaded.");
        try
        {
            RoomWaypoint waypoint = FindWaypoint(root, WaypointType.Rest) ?? FindWaypoint(root, WaypointType.Center);
            Transform spawnPoint = FindOrCreate(root.transform, "WorkerCollectorSpawnPoint");
            Transform restPoint = FindOrCreate(root.transform, "WorkerCollectorRestPoint");
            Vector3 markerPosition = waypoint != null ? waypoint.transform.localPosition : Vector3.zero;
            ConfigureMarker(spawnPoint, markerPosition);
            ConfigureMarker(restPoint, markerPosition);

            GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPath);
            if (workerPrefab == null)
                throw new InvalidOperationException("Worker Collector prefab could not be loaded.");
            WorkerCollectorSpawnRestProvider provider = GetOrAdd<WorkerCollectorSpawnRestProvider>(root);
            provider.Configure(workerPrefab, spawnPoint, waypoint, restPoint, 5f, 1);
            Save(root, RestRoomPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveTemporaryConveyorDropOff()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ConveyorRoomPath);
        if (root == null)
            throw new InvalidOperationException("Conveyor room prefab could not be loaded.");
        try
        {
            WorkerCollectorDropOffProvider temporary = root.GetComponent<WorkerCollectorDropOffProvider>();
            if (temporary != null)
                UnityEngine.Object.DestroyImmediate(temporary);
            Save(root, ConveyorRoomPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static RoomWaypoint FindWaypoint(GameObject root, WaypointType type)
    {
        RoomWaypoint[] waypoints = root.GetComponentsInChildren<RoomWaypoint>(true);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null && waypoints[i].type == type)
                return waypoints[i];
        }
        return null;
    }

    private static Transform FindOrCreate(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;
        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void ConfigureMarker(Transform marker, Vector3 localPosition)
    {
        marker.localPosition = localPosition;
        marker.localRotation = Quaternion.identity;
        marker.localScale = Vector3.one;
    }

    private static void Save(GameObject root, string path)
    {
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        if (!success)
            throw new InvalidOperationException($"Unity failed to save '{path}'.");
    }
}

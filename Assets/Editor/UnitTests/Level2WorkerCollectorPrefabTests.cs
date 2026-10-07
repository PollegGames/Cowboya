using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Level2WorkerCollectorPrefabTests
{
    private const string WorkerPrefabPath = "Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab";
    private const string WorkerCollectorPrefabPath = "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";
    private const string NormalCubePrefabPath = "Assets/Resources/Prefabs/IntereableObjects/CubeNormal.prefab";
    private const string ConveyorRoomPrefabPath = "Assets/Resources/Prefabs/Map/ROOM_Conveyor.prefab";
    private const string RestRoomPrefabPath = "Assets/Resources/Prefabs/Map/ROOM_resting.prefab";
    private const string GarageMachinePrefabPath =
        "Assets/Resources/Prefabs/Map/Basic/Machines/GarageCubes.prefab";

    [Test]
    public void RobotRoles_PreserveExistingSerializedValues_AndAppendWorkerCollector()
    {
        Assert.AreEqual(0, (int)RobotRole.Worker);
        Assert.AreEqual(1, (int)RobotRole.SecurityGuard);
        Assert.AreEqual(2, (int)RobotRole.WorkerSpawner);
        Assert.AreEqual(3, (int)RobotRole.Follower);
        Assert.AreEqual(4, (int)RobotRole.Boss);
        Assert.AreEqual(5, (int)RobotRole.Collector);
        Assert.AreEqual(6, (int)RobotRole.WorkerCollector);
    }

    [Test]
    public void Prefab_HasOneCompleteWorkerCollectorPipeline()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPrefabPath);

        Assert.IsNotNull(prefab);
        Assert.AreEqual("WorkerCollector", prefab.name);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<RobotMemoryNew>(true).Length);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<RobotBrainNew>(true).Length);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<RobotHeartNew>(true).Length);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<RobotBodyController>(true).Length);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<WorkerCollectorBodyController>(true).Length);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<RobotObjectArmReachController>(true).Length);
        Assert.IsNull(prefab.GetComponent<WorkerCollectorArmReachTestIsolation>());
        Assert.AreEqual(RobotRole.WorkerCollector, prefab.GetComponent<RobotHeartNew>().Role);
    }

    [Test]
    public void Prefab_HasConfiguredCarryAnchor()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPrefabPath);
        WorkerCollectorBodyController controller = prefab.GetComponent<WorkerCollectorBodyController>();

        Assert.IsNotNull(controller);
        Assert.IsNotNull(controller.CarryAnchor);
        Assert.AreEqual("CubeCarryAnchor", controller.CarryAnchor.name);
        Assert.IsNotNull(controller.CarryAnchor.parent);
        Assert.AreEqual("RHand_Effector", controller.CarryAnchor.parent.name);
    }

    [Test]
    public void Prefab_PickupArrivalAllowsGroundedBodyVerticalOffset()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPrefabPath);
        WorkerCollectorBodyController controller = prefab.GetComponent<WorkerCollectorBodyController>();

        Assert.IsNotNull(controller);
        Assert.AreEqual(0.15f, controller.PickupArrivalThreshold.x, 0.001f);
        Assert.AreEqual(2f, controller.PickupArrivalThreshold.y, 0.001f);
    }

    [Test]
    public void RuntimePrefab_UsesRightHandEffectorAlignedCarryAnchor()
    {
        GameObject instance = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPrefabPath));
        try
        {
            WorkerCollectorBodyController controller =
                instance.GetComponent<WorkerCollectorBodyController>();

            Assert.IsNotNull(controller.CarryAnchor);
            Assert.AreEqual("CubeCarryAnchor", controller.CarryAnchor.name);
            Assert.IsNotNull(controller.CarryAnchor.parent);
            Assert.AreEqual("RHand_Effector", controller.CarryAnchor.parent.name);
            Assert.IsTrue(controller.CarryAnchor.IsChildOf(instance.transform));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void WorkerCollectorHeart_DefaultsToHarmlessStandby()
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WorkerCollectorPrefabPath));
        try
        {
            RobotHeartNew heart = instance.GetComponent<RobotHeartNew>();
            heart.ConfigureRole(heart.Role, resetStack: true);
            Assert.IsNotNull(heart.CurrentTask);
            Assert.AreEqual(RobotTaskType.WorkerCollectorStandby, heart.CurrentTask.Type);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void OriginalWorkerPrefab_RemainsWorkerWithoutCollectorShell()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPrefabPath);

        Assert.IsNotNull(prefab);
        Assert.AreEqual(RobotRole.Worker, prefab.GetComponent<RobotHeartNew>().Role);
        Assert.IsNull(prefab.GetComponent<WorkerCollectorBodyController>());
        Assert.IsNull(prefab.transform.Find("CubeCarryAnchor"));
    }

    [Test]
    public void NormalCubePrefab_IsMarkedAsWhiteCollectorCargo()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NormalCubePrefabPath);

        Assert.IsNotNull(prefab);
        WhiteCubeCargo cargo = prefab.GetComponent<WhiteCubeCargo>();
        Assert.IsNotNull(cargo);
        Assert.AreSame(prefab.GetComponent<CubePickup>(), cargo.Pickup);
    }

    [Test]
    public void ConveyorRoom_RegistersOnlyWhiteCubeSourceCapability()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConveyorRoomPrefabPath);

        Assert.IsNotNull(prefab);
        Assert.IsNotNull(prefab.GetComponent<WorkerCollectorWhiteCubeSourceProvider>());
        Assert.IsNull(prefab.GetComponent<WorkerCollectorDropOffProvider>());
    }

    [Test]
    public void RestRoom_ProvidesConfiguredSingleCollectorSpawnAndRestCapability()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RestRoomPrefabPath);

        Assert.IsNotNull(prefab);
        WorkerCollectorSpawnRestProvider provider = prefab.GetComponent<WorkerCollectorSpawnRestProvider>();
        Assert.IsNotNull(provider);
        Assert.AreEqual(1, provider.MaximumLiveCollectors);
        Assert.AreEqual(5f, provider.RestDuration);
        Assert.IsNotNull(provider.RestWaypoint);
        Assert.IsNotNull(prefab.transform.Find("WorkerCollectorSpawnPoint"));
        Assert.IsNotNull(prefab.transform.Find("WorkerCollectorRestPoint"));
        Assert.AreEqual(Quaternion.identity, provider.SpawnRotation);
        Assert.AreNotEqual(prefab.transform.Find("WorkerCollectorSpawnPoint").rotation, provider.SpawnRotation);
    }

    [Test]
    public void GarageMachine_HasNineExplicitSlotsProcessorDoorAndDestination()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GarageMachinePrefabPath);

        Assert.IsNotNull(prefab);
        GarageCubeStorage storage = prefab.GetComponent<GarageCubeStorage>();
        GarageCubeProcessor processor = prefab.GetComponent<GarageCubeProcessor>();
        WorkerCollectorDropOffProvider destination = prefab.GetComponent<WorkerCollectorDropOffProvider>();
        Assert.IsNotNull(storage);
        Assert.IsNotNull(processor);
        Assert.IsNotNull(destination);
        Assert.IsNotNull(prefab.GetComponent<GarageDoorController>());
        Assert.IsNotNull(destination.DeliveryPoint);
        Assert.IsNotNull(destination.WaitPoint);
        Assert.AreEqual("DeliveryPoint", destination.DeliveryPoint.name);
        Assert.AreEqual("WorkerWaitPoint", destination.WaitPoint.name);
        Assert.AreNotSame(destination.DeliveryPoint, destination.WaitPoint);
        Assert.AreEqual(GarageCubeStorage.Capacity, storage.Slots.Count);
        for (int i = 0; i < GarageCubeStorage.Capacity; i++)
        {
            Assert.IsNotNull(storage.Slots[i]);
            Assert.AreEqual($"Slot_{i:00}", storage.Slots[i].name);
        }
        Assert.IsNull(prefab.GetComponent<GarageCubeConveyorController>());
    }
}

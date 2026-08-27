using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Level2WorkerCollectorPrefabTests
{
    private const string WorkerPrefabPath = "Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab";
    private const string WorkerCollectorPrefabPath = "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";

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
        Assert.AreEqual(prefab.transform, controller.CarryAnchor.parent);
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
}

using System.Collections.Generic;
using CowBoya.Robots;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RobotObjectArmReachControllerTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void SelectArm_TargetOnRight_SelectsRightArm()
    {
        CowboyArmSide result = RobotObjectArmReachController.SelectArm(
            1f, 0.1f, CowboyArmSide.Left);

        Assert.AreEqual(CowboyArmSide.Right, result);
    }

    [Test]
    public void SelectArm_TargetOnLeft_SelectsLeftArm()
    {
        CowboyArmSide result = RobotObjectArmReachController.SelectArm(
            -1f, 0.1f, CowboyArmSide.Right);

        Assert.AreEqual(CowboyArmSide.Left, result);
    }

    [TestCase(CowboyArmSide.Left)]
    [TestCase(CowboyArmSide.Right)]
    public void SelectArm_TargetInsideDeadZone_PreservesPreviousArm(CowboyArmSide previous)
    {
        CowboyArmSide result = RobotObjectArmReachController.SelectArm(0.05f, 0.1f, previous);

        Assert.AreEqual(previous, result);
    }

    [Test]
    public void CalculateReachPoint_TargetWithinRadius_ProjectsToFixedOrbit()
    {
        Vector3 result = RobotObjectArmReachController.CalculateReachPoint(
            new Vector3(1f, 2f, -4f),
            new Vector3(2f, 3f, 10f),
            3f);

        Vector3 expected = new Vector3(
            1f + 3f / Mathf.Sqrt(2f),
            2f + 3f / Mathf.Sqrt(2f),
            -4f);
        Assert.That(result.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.AreEqual(expected.z, result.z);
    }

    [Test]
    public void CalculateReachPoint_TargetOutsideRadius_ProjectsToFixedOrbit()
    {
        Vector3 result = RobotObjectArmReachController.CalculateReachPoint(
            Vector3.zero,
            new Vector3(6f, 8f, 20f),
            3f);

        Assert.That(Vector3.Distance(new Vector3(1.8f, 2.4f, 0f), result), Is.LessThan(0.0001f));
    }

    [Test]
    public void CalculateReachPoint_TargetAtCenter_ReturnsCenter()
    {
        Vector3 center = new Vector3(1f, 2f, -4f);

        Vector3 result = RobotObjectArmReachController.CalculateReachPoint(center, center, 3f);

        Assert.AreEqual(center, result);
    }

    [Test]
    public void CalculatePickupReachPoint_NearbyCube_DoesNotOvershoot() {
        Vector3 result = RobotObjectArmReachController.CalculatePickupReachPoint(
            new Vector3(1f, 2f, -4f), new Vector3(2f, 3f, 10f), 3f);

        Assert.AreEqual(new Vector3(2f, 3f, -4f), result);
    }

    [Test]
    public void CalculatePickupReachPoint_DistantCube_ClampsToReachLimit() {
        Vector3 result = RobotObjectArmReachController.CalculatePickupReachPoint(
            Vector3.zero, new Vector3(6f, 8f, 20f), 3f);

        Assert.That(result.x, Is.EqualTo(1.8f).Within(0.0001f));
        Assert.That(result.y, Is.EqualTo(2.4f).Within(0.0001f));
        Assert.AreEqual(0f, result.z);
    }

    [TestCase(-1f, CowboyArmSide.Left)]
    [TestCase(1f, CowboyArmSide.Right)]
    public void SetPickupTarget_WithinReach_MovesSelectedArmToCube(float side, CowboyArmSide expectedArm) {
        Transform root = CreateObject("Robot").transform;
        Transform left = CreateChild(root, "LArm_Solver_Target", Vector3.left);
        Transform right = CreateChild(root, "RArm_Solver_Target", Vector3.right);
        Transform target = CreateObject("Cube").transform;
        target.position = new Vector3(side * 1.5f, 0.5f, 10f);
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, null, left, right);

        controller.SetPickupTarget(target);
        controller.Tick(1f);

        Transform selected = expectedArm == CowboyArmSide.Left ? left : right;
        Assert.AreEqual(expectedArm, controller.ActiveArm);
        Assert.AreEqual(new Vector3(side * 1.5f, 0.5f, 0f), selected.position);
    }

    [TestCase(-1f, CowboyArmSide.Left)]
    [TestCase(1f, CowboyArmSide.Right)]
    public void HoldCurrentPose_PreservesActiveHandDuringCarry_AndClearReturnsToRest(
        float side, CowboyArmSide expectedArm) {
        Transform root = CreateObject("Robot").transform;
        Transform left = CreateChild(root, "LArm_Solver_Target", Vector3.left);
        Transform right = CreateChild(root, "RArm_Solver_Target", Vector3.right);
        Transform leftHand = CreateChild(root, "LHand_Effector", Vector3.left);
        Transform rightHand = CreateChild(root, "RHand_Effector", Vector3.right);
        Transform target = CreateObject("Cube").transform;
        target.position = new Vector3(side * 1.5f, 0.5f, 0f);
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, null, left, right);
        controller.SetPickupTarget(target);
        controller.Tick(1f);
        Transform selected = expectedArm == CowboyArmSide.Left ? left : right;
        Vector3 carryPose = selected.localPosition;

        controller.HoldCurrentPose();
        target.position = Vector3.up * 20f;
        root.position = Vector3.right * 4f;
        controller.Tick(1f);

        Assert.IsNull(controller.Target, "Held cargo must not feed its changing position back into arm aim.");
        Assert.AreEqual(expectedArm, controller.ActiveArm);
        Assert.AreSame(expectedArm == CowboyArmSide.Left ? leftHand : rightHand, controller.ActiveHandEffector);
        Assert.AreEqual(carryPose, selected.localPosition);

        controller.ClearTarget();
        controller.Tick(1f);

        Assert.IsNull(controller.ActiveArm);
        Assert.AreEqual(Vector3.left, left.localPosition);
        Assert.AreEqual(Vector3.right, right.localPosition);
    }

    [Test]
    public void SetTarget_AfterPickupHold_ResumesFixedOrbitAim() {
        Transform root = CreateObject("Robot").transform;
        Transform left = CreateChild(root, "LArm_Solver_Target", Vector3.left);
        Transform right = CreateChild(root, "RArm_Solver_Target", Vector3.right);
        Transform target = CreateObject("Cube").transform;
        target.position = Vector3.right * 1.5f;
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, null, left, right);
        controller.SetPickupTarget(target);
        controller.Tick(1f);
        controller.HoldCurrentPose();

        controller.SetTarget(target);
        controller.Tick(1f);

        Assert.AreEqual(Vector3.right * controller.ReachRadius, right.position);
    }

    [Test]
    public void Tick_TargetChangesSide_SwitchesArmsAndReturnsPreviousArmToRest()
    {
        Transform root = CreateObject("Robot").transform;
        Transform left = CreateChild(root, "LArm_Solver_Target", new Vector3(-1f, 0f, 0f));
        Transform right = CreateChild(root, "RArm_Solver_Target", new Vector3(1f, 0f, 0f));
        Transform target = CreateObject("Target").transform;
        target.position = Vector3.right * 2f;
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, target, left, right);

        controller.Tick(1f);
        Assert.AreEqual(CowboyArmSide.Right, controller.ActiveArm);
        Assert.AreEqual(Vector3.right * 3f, right.position);

        target.position = Vector3.left * 2f;
        controller.Tick(1f);

        Assert.AreEqual(CowboyArmSide.Left, controller.ActiveArm);
        Assert.AreEqual(Vector3.left * 3f, left.position);
        Assert.AreEqual(new Vector3(1f, 0f, 0f), right.localPosition);
    }

    [Test]
    public void Tick_MissingPreferredArm_FallsBackToAvailableArm()
    {
        Transform root = CreateObject("Robot").transform;
        Transform right = CreateChild(root, "OnlyRight", Vector3.right);
        Transform target = CreateObject("Target").transform;
        target.position = Vector3.left * 2f;
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, target, null, right);

        controller.Tick(1f);

        Assert.AreEqual(CowboyArmSide.Right, controller.ActiveArm);
    }

    [Test]
    public void ClearTarget_ReturnsBothArmsToRest()
    {
        Transform root = CreateObject("Robot").transform;
        Transform left = CreateChild(root, "Left", Vector3.left);
        Transform right = CreateChild(root, "Right", Vector3.right);
        Transform target = CreateObject("Target").transform;
        target.position = Vector3.right * 2f;
        RobotObjectArmReachController controller = root.gameObject.AddComponent<RobotObjectArmReachController>();
        controller.Configure(root, target, left, right);
        controller.Tick(1f);
        Assert.AreNotEqual(Vector3.right, right.localPosition);

        controller.ClearTarget();
        controller.Tick(1f);

        Assert.IsNull(controller.ActiveArm);
        Assert.AreEqual(Vector3.left, left.localPosition);
        Assert.AreEqual(Vector3.right, right.localPosition);
    }

    [Test]
    public void TestingSceneBot_WiresWorkerToSceneCubeAndIsolation()
    {
        const string ScenePath = "Assets/Scenes/TestingSceneBot.unity";
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool sceneWasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!sceneWasAlreadyLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            RobotObjectArmReachController reachController = null;
            WorkerCollectorArmReachTestIsolation isolation = null;
            GameObject cube = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "WorkerCollector")
                {
                    reachController = roots[i].GetComponent<RobotObjectArmReachController>();
                    isolation = roots[i].GetComponent<WorkerCollectorArmReachTestIsolation>();
                }
                else if (roots[i].name == "CubeNormal")
                {
                    cube = roots[i];
                }
            }

            Assert.IsNotNull(reachController);
            Assert.IsNotNull(isolation);
            Assert.IsNotNull(cube);
            Assert.AreSame(cube.transform, reachController.Target);
        }
        finally
        {
            if (!sceneWasAlreadyLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    [Test]
    public void WorkerPrefab_UpperArmBinderPairsFlowFromMasterToPhysicsPuppet()
    {
        const string PrefabPath = "Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab);

        SimplePuppetBinder binder = prefab.GetComponent<SimplePuppetBinder>();
        Assert.IsNotNull(binder);

        int verifiedArmPairs = 0;
        for (int i = 0; i < binder.Pairs.Count; i++)
        {
            SimplePuppetBinder.BonePair pair = binder.Pairs[i];
            if (pair == null || pair.Master == null || pair.Master.name != "LArm_Bone" && pair.Master.name != "RArm_Bone")
                continue;

            Assert.IsTrue(pair.Master.IsChildOf(binder.MasterRoot));
            Assert.IsTrue(pair.Puppet.IsChildOf(binder.PuppetRoot));
            Assert.IsNotNull(pair.Puppet.GetComponent<Rigidbody2D>());
            verifiedArmPairs++;
        }

        Assert.AreEqual(2, verifiedArmPairs);
    }

    private GameObject CreateObject(string name)
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = CreateObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = localPosition;
        return child;
    }
}

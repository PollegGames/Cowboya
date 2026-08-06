using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SpawnRobotCollectorControllerTests
{
    private readonly List<GameObject> createdObjects = new();
    private SimulationMode2D previousSimulationMode;

    [SetUp]
    public void SetUp()
    {
        previousSimulationMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
    }

    [TearDown]
    public void TearDown()
    {
        Physics2D.simulationMode = previousSimulationMode;
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void ScanForDeadRobots_ResolvesDeadRobotFromChildColliderOnlyOnce()
    {
        PositionTriggerZone zone = CreateZone();
        SpawnRobotCollectorController collector = CreateCollector(zone);
        RobotStateController robot = CreateRobot(Vector2.zero, RobotState.Dead, 2);

        Physics2D.SyncTransforms();

        Assert.IsTrue(collector.ScanForDeadRobots());
        Assert.IsFalse(collector.ScanForDeadRobots());
        Assert.AreEqual(RobotState.Dead, robot.CurrentState);
    }

    [Test]
    public void ScanForDeadRobots_DetectsRobotThatDiesWhileRemainingInsideZone()
    {
        PositionTriggerZone zone = CreateZone();
        SpawnRobotCollectorController collector = CreateCollector(zone);
        RobotStateController robot = CreateRobot(Vector2.zero, RobotState.Alive, 1);

        Physics2D.SyncTransforms();
        Assert.IsFalse(collector.ScanForDeadRobots());

        robot.SetInitialDeadState();

        Assert.IsTrue(collector.ScanForDeadRobots());
        Assert.IsFalse(collector.ScanForDeadRobots());
    }

    [Test]
    public void ScanForDeadRobots_IgnoresLivingRobotsAndCollidersOutsideConfiguredLayer()
    {
        PositionTriggerZone zone = CreateZone();
        SpawnRobotCollectorController collector = CreateCollector(zone);
        CreateRobot(Vector2.zero, RobotState.Alive, 1);

        GameObject wrongLayerRobotObject = CreateObject("Wrong Layer Robot");
        RobotStateController wrongLayerRobot = wrongLayerRobotObject.AddComponent<RobotStateController>();
        wrongLayerRobot.SetInitialDeadState();
        GameObject bodyPart = CreateObject("Wrong Layer Body Part");
        bodyPart.transform.SetParent(wrongLayerRobotObject.transform);
        bodyPart.AddComponent<BoxCollider2D>();

        Physics2D.SyncTransforms();

        Assert.IsFalse(collector.ScanForDeadRobots());
    }

    [Test]
    public void ScanForDeadRobots_QueuesOneCollectableForMultiplePhysicalPartColliders()
    {
        PositionTriggerZone zone = CreateZone();
        SpawnRobotCollectorController collector = CreateCollector(zone);
        RobotStateController robot = CreatePhysicalRobot(Vector2.zero, RobotState.Dead, 2);

        Physics2D.SyncTransforms();

        Assert.IsTrue(collector.ScanForDeadRobots());
        Assert.AreEqual(1, collector.QueuedTargetCount);
        Assert.IsFalse(collector.ScanForDeadRobots());
        Assert.AreEqual(1, collector.QueuedTargetCount);
        Assert.AreEqual(RobotState.Dead, robot.CurrentState);
    }

    [Test]
    public void MissionMarkers_AreSampledLiveFromTheMovingSpawnHierarchy()
    {
        GameObject root = CreateObject("Collector Machine");
        Transform movingSpawn = CreateChild(root.transform, "SpawnPoint", new Vector3(1f, 2f, 0f));
        Transform launch = CreateChild(movingSpawn, "LaunchExitPoint", new Vector3(3f, 0f, 0f));
        Transform dock = CreateChild(movingSpawn, "DockApproachPoint", new Vector3(2f, 1f, 0f));
        Transform intake = CreateChild(movingSpawn, "IntakePoint", Vector3.zero);
        GameObject intakeZoneObject = CreateObject("CollectorIntakeZone");
        intakeZoneObject.transform.SetParent(movingSpawn, false);
        BoxCollider2D intakeZone = intakeZoneObject.AddComponent<BoxCollider2D>();
        intakeZone.isTrigger = true;
        SpawnRobotCollectorController controller = root.AddComponent<SpawnRobotCollectorController>();
        controller.ConfigureMissionReferences(null, movingSpawn, launch, dock, intake, intakeZone);

        Vector2 initialLaunch = controller.GetLaunchExitPosition();
        movingSpawn.position += new Vector3(5f, -2f, 0f);

        Assert.AreEqual(initialLaunch + new Vector2(5f, -2f), controller.GetLaunchExitPosition());
        Assert.AreEqual((Vector2)dock.position, controller.GetDockApproachPosition());
        Assert.AreEqual((Vector2)intake.position, controller.GetIntakePosition());
    }

    [Test]
    public void PositionCollectorAtSpawn_DoesNotCopyMachinePitchIntoThe2DRig()
    {
        GameObject machine = CreateObject("Collector Machine");
        machine.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        CreateChild(machine.transform, "Spawn Top", Vector3.zero);
        CreateChild(machine.transform, "Spawn Bottom", Vector3.zero);
        Transform spawn = CreateChild(machine.transform, "SpawnPoint", new Vector3(2f, 1f, 0f));
        SpawnRobotCollectorController controller = machine.AddComponent<SpawnRobotCollectorController>();
        controller.enabled = false;
        controller.ConfigureMissionReferences(null, spawn, spawn, spawn, spawn, null);

        GameObject collector = CreateObject("CollectorRobot_Fly(Clone)");
        collector.transform.localScale = Vector3.one * 0.4f;
        GameObject bodyObject = CreateObject("bone_Body");
        bodyObject.transform.SetParent(collector.transform, false);
        bodyObject.transform.localPosition = new Vector3(0.3f, 8.4f, 0f);
        Rigidbody2D body = bodyObject.AddComponent<Rigidbody2D>();

        InvokePrivate(controller, "PositionCollectorAtSpawn", collector);

        Assert.That(Quaternion.Angle(collector.transform.rotation, Quaternion.identity),
            Is.LessThan(0.001f));
        Assert.That(Vector2.Distance(body.position, spawn.position), Is.LessThan(0.001f));
        Assert.AreEqual(Vector3.one * 0.4f, collector.transform.localScale);
    }

    [Test]
    public void AuthoredDockPath_ReachesCollectorIntakeZone()
    {
        GameObject machinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Map/Basic/Machines/SpawnRobotCollector.prefab");
        GameObject collectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab");
        Assert.IsNotNull(machinePrefab);
        Assert.IsNotNull(collectorPrefab);
        GameObject machineObject = Object.Instantiate(machinePrefab);
        createdObjects.Add(machineObject);
        SpawnRobotCollectorController machine =
            machineObject.GetComponent<SpawnRobotCollectorController>();
        Assert.IsNotNull(machine);
        InvokePrivate(machine, "ApplyPanelOpenAmount", 1f);

        RobotStateController targetState = CreatePhysicalRobot(
            new Vector2(-100f, -100f),
            RobotState.Dead,
            1);
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(targetState);
        Assert.IsTrue(target.TryClaim(41, machine, out CollectorTargetClaim claim));
        CollectorMissionAssignment assignment = new CollectorMissionAssignment(
            41,
            machine,
            target,
            claim);

        GameObject collector = Object.Instantiate(collectorPrefab);
        createdObjects.Add(collector);
        collector.SetActive(false);
        collector.GetComponent<RobotHeartNew>().enabled = false;
        collector.GetComponent<RobotBrainNew>().enabled = false;
        collector.GetComponent<CollectorRobotObservationBridge>().enabled = false;
        collector.SetActive(true);
        CollectorRobotBodyController body =
            collector.GetComponent<CollectorRobotBodyController>();
        Assert.IsNotNull(body);
        body.ResetPhysicalState();
        Vector2 dockPosition = machine.GetDockApproachPosition();
        collector.transform.position += (Vector3)(dockPosition - body.BodyRigidbody.position);
        Physics2D.SyncTransforms();

        body.BeginDocking(assignment);
        for (int i = 0; i < 500 && !CollectorIntersectsIntake(collector, machine.IntakeZone); i++)
        {
            body.StepPhysics(0.02f);
            Physics2D.Simulate(0.02f);
        }

        Assert.IsTrue(
            CollectorIntersectsIntake(collector, machine.IntakeZone),
            $"Collector stopped at {body.BodyRigidbody.position}, target "
            + $"{machine.GetIntakePosition()}, velocity {body.BodyRigidbody.linearVelocity}. "
            + DescribeIntakeGeometry(collector, machine.IntakeZone));
    }

    [Test]
    public void Panels_CollapseTowardOuterEdgesWhileBackgroundStaysFixed()
    {
        GameObject root = CreateObject("Collector");
        root.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Transform top = CreatePrimitiveChild(root.transform, "Spawn Top", new Vector3(0f, 0f, 1f));
        Transform bottom = CreatePrimitiveChild(root.transform, "Spawn Bottom", new Vector3(0f, 0f, -1f));
        Transform background = CreatePrimitiveChild(root.transform, "Spawn Background", Vector3.zero);
        Vector3 topClosedPosition = top.localPosition;
        Vector3 bottomClosedPosition = bottom.localPosition;
        Vector3 topClosedScale = top.localScale;
        Vector3 bottomClosedScale = bottom.localScale;
        Vector3 backgroundClosedPosition = background.localPosition;
        Vector3 backgroundClosedScale = background.localScale;
        Renderer topRenderer = top.GetComponent<Renderer>();
        Renderer bottomRenderer = bottom.GetComponent<Renderer>();
        float fixedTopEdge = topRenderer.bounds.max.y;
        float movingTopEdge = topRenderer.bounds.min.y;
        float fixedBottomEdge = bottomRenderer.bounds.min.y;
        float movingBottomEdge = bottomRenderer.bounds.max.y;

        SpawnRobotCollectorController collector = root.AddComponent<SpawnRobotCollectorController>();
        collector.enabled = false;
        SetPrivateField(collector, "topFixedEdgeLocalPosition", 0.5f);
        SetPrivateField(collector, "bottomFixedEdgeLocalPosition", -0.5f);
        InvokePrivate(collector, "Initialize");
        InvokePrivate(collector, "ApplyPanelOpenAmount", 1f);

        Assert.That(topRenderer.bounds.max.y, Is.EqualTo(fixedTopEdge).Within(0.0001f));
        Assert.Greater(topRenderer.bounds.min.y, movingTopEdge - 0.0001f);
        Assert.That(bottomRenderer.bounds.min.y, Is.EqualTo(fixedBottomEdge).Within(0.0001f));
        Assert.Less(bottomRenderer.bounds.max.y, movingBottomEdge + 0.0001f);
        Assert.Less(top.localScale.z, topClosedScale.z);
        Assert.Less(bottom.localScale.z, bottomClosedScale.z);
        Assert.AreEqual(backgroundClosedPosition, background.localPosition);
        Assert.AreEqual(backgroundClosedScale, background.localScale);

        collector.ResetPanels();

        Assert.AreEqual(topClosedPosition, top.localPosition);
        Assert.AreEqual(bottomClosedPosition, bottom.localPosition);
        Assert.AreEqual(topClosedScale, top.localScale);
        Assert.AreEqual(bottomClosedScale, bottom.localScale);
        Assert.AreEqual(backgroundClosedPosition, background.localPosition);
        Assert.AreEqual(backgroundClosedScale, background.localScale);
    }

    private PositionTriggerZone CreateZone()
    {
        GameObject zoneObject = CreateObject("Robot Detection Zone");
        PositionTriggerZone zone = zoneObject.AddComponent<PositionTriggerZone>();
        zone.zoneSize = new Vector2(10f, 10f);
        zone.detectionLayer = LayerMask.GetMask("Enemy");
        return zone;
    }

    private SpawnRobotCollectorController CreateCollector(PositionTriggerZone zone)
    {
        GameObject collectorObject = CreateObject("Collector");
        CreateChild(collectorObject.transform, "Spawn Top", Vector3.zero);
        CreateChild(collectorObject.transform, "Spawn Bottom", Vector3.zero);
        SpawnRobotCollectorController collector = collectorObject.AddComponent<SpawnRobotCollectorController>();
        collector.enabled = false;
        SetPrivateField(collector, "robotDetectionZone", zone);
        return collector;
    }

    private RobotStateController CreateRobot(Vector2 position, RobotState state, int colliderCount)
    {
        GameObject robotObject = CreateObject("Robot");
        robotObject.transform.position = position;
        RobotStateController robot = robotObject.AddComponent<RobotStateController>();

        for (int i = 0; i < colliderCount; i++)
        {
            GameObject bodyPart = CreateObject($"Body Part {i}");
            bodyPart.layer = LayerMask.NameToLayer("Enemy");
            bodyPart.transform.SetParent(robotObject.transform);
            bodyPart.transform.localPosition = new Vector3(i * 0.1f, 0f, 0f);
            bodyPart.AddComponent<BoxCollider2D>();
        }

        if (state == RobotState.Dead)
            robot.SetInitialDeadState();

        return robot;
    }

    private RobotStateController CreatePhysicalRobot(Vector2 position, RobotState state, int partCount)
    {
        GameObject robotObject = CreateObject("Physical Robot");
        robotObject.transform.position = position;
        RobotStateController robot = robotObject.AddComponent<RobotStateController>();

        for (int i = 0; i < partCount; i++)
        {
            GameObject bodyPart = CreateObject($"Physical Part {i}");
            bodyPart.layer = LayerMask.NameToLayer("Enemy");
            bodyPart.transform.SetParent(robotObject.transform);
            bodyPart.transform.localPosition = new Vector3(i * 0.1f, 0f, 0f);
            bodyPart.AddComponent<Rigidbody2D>();
            bodyPart.AddComponent<BoxCollider2D>();
        }

        if (state == RobotState.Dead)
            robot.SetInitialDeadState();

        return robot;
    }

    private Transform CreateChild(Transform parent, string objectName, Vector3 localPosition)
    {
        GameObject child = CreateObject(objectName);
        child.transform.SetParent(parent);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private Transform CreatePrimitiveChild(Transform parent, string objectName, Vector3 localPosition)
    {
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.name = objectName;
        createdObjects.Add(child);
        child.transform.SetParent(parent);
        child.transform.localPosition = localPosition;
        return child.transform;
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static bool CollectorIntersectsIntake(GameObject collector, Collider2D intake)
    {
        if (collector == null || intake == null)
            return false;

        Bounds intakeBounds = intake.bounds;
        Collider2D[] colliders = collector.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null
                && collider.enabled
                && !collider.isTrigger
                && BoundsOverlapIn2D(intakeBounds, collider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BoundsOverlapIn2D(Bounds first, Bounds second)
    {
        return first.min.x <= second.max.x
            && first.max.x >= second.min.x
            && first.min.y <= second.max.y
            && first.max.y >= second.min.y;
    }

    private static string DescribeIntakeGeometry(GameObject collector, Collider2D intake)
    {
        List<string> descriptions = new List<string>
        {
            $"Intake bounds center={intake.bounds.center}, extents={intake.bounds.extents}"
        };
        Collider2D[] colliders = collector.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            descriptions.Add(
                $"{collider.name}: trigger={collider.isTrigger}, enabled={collider.enabled}, "
                + $"center={collider.bounds.center}, extents={collider.bounds.extents}");
        }

        return string.Join("; ", descriptions);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            GetArgumentTypes(arguments),
            null);
        Assert.IsNotNull(method, $"Expected method '{methodName}'.");
        method.Invoke(target, arguments);
    }

    private static System.Type[] GetArgumentTypes(object[] arguments)
    {
        System.Type[] argumentTypes = new System.Type[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            argumentTypes[i] = arguments[i].GetType();

        return argumentTypes;
    }
}

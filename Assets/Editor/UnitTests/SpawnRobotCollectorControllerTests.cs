using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SpawnRobotCollectorControllerTests
{
    private readonly List<GameObject> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
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
        Assert.Greater(topRenderer.bounds.min.y, movingTopEdge);
        Assert.That(bottomRenderer.bounds.min.y, Is.EqualTo(fixedBottomEdge).Within(0.0001f));
        Assert.Less(bottomRenderer.bounds.max.y, movingBottomEdge);
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

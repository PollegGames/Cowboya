using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class StaticLevelPathTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
                Object.DestroyImmediate(createdObject);
        }
        createdObjects.Clear();
    }

    [Test]
    public void BidirectionalRoute_CreatesBothDirections()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        SetRoutes(fixture.Path, new StaticWaypointRoute("Main", StaticRouteDirection.Bidirectional, new[] { a, b }));

        Assert.IsTrue(fixture.Path.TryInitializeNavigation(fixture.Service));
        CollectionAssert.Contains(a.Neighbors, b);
        CollectionAssert.Contains(b.Neighbors, a);
    }

    [Test]
    public void ForwardOnlyRoute_CreatesOnlyForwardDirections()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        RoomWaypoint c = CreateWaypoint("C", fixture.Room, Vector3.right * 2f);
        SetRoutes(fixture.Path, new StaticWaypointRoute("Lift", StaticRouteDirection.ForwardOnly, new[] { a, b, c }));

        Assert.IsTrue(fixture.Path.TryInitializeNavigation(fixture.Service));
        CollectionAssert.Contains(a.Neighbors, b);
        CollectionAssert.Contains(b.Neighbors, c);
        CollectionAssert.DoesNotContain(b.Neighbors, a);
        CollectionAssert.DoesNotContain(c.Neighbors, b);
    }

    [Test]
    public void SharedJunction_CombinesRoutesIntoOneGraph()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint junction = CreateWaypoint("Junction", fixture.Room, Vector3.right);
        RoomWaypoint mainEnd = CreateWaypoint("MainEnd", fixture.Room, Vector3.right * 2f);
        RoomWaypoint branchEnd = CreateWaypoint("BranchEnd", fixture.Room, Vector3.up);
        SetRoutes(
            fixture.Path,
            new StaticWaypointRoute("Main", StaticRouteDirection.Bidirectional, new[] { a, junction, mainEnd }),
            new StaticWaypointRoute("Branch", StaticRouteDirection.Bidirectional, new[] { junction, branchEnd }));

        Assert.IsTrue(fixture.Path.TryInitializeNavigation(fixture.Service));
        CollectionAssert.AreEqual(new[] { a, junction, branchEnd }, fixture.Service.FindWorldPath(a, branchEnd));
    }

    [Test]
    public void DuplicateConnections_AreAddedOnlyOnce()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        SetRoutes(
            fixture.Path,
            new StaticWaypointRoute("First", StaticRouteDirection.ForwardOnly, new[] { a, b }),
            new StaticWaypointRoute("Duplicate", StaticRouteDirection.ForwardOnly, new[] { a, b }));

        List<StaticWaypointConnection> connections = fixture.Path.GetDirectedConnections();
        Assert.AreEqual(1, connections.Count);
    }

    [Test]
    public void IncludeUnavailableFlag_DoesNotChangeStaticGraph()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        b.IsAvailable = false;
        SetRoutes(fixture.Path, new StaticWaypointRoute("Main", StaticRouteDirection.Bidirectional, new[] { a, b }));
        Assert.IsTrue(fixture.Path.TryInitializeNavigation(fixture.Service));

        fixture.Service.BuildAllNeighbors(false);
        Assert.IsTrue(a.Neighbors.Contains(b) && b.Neighbors.Contains(a));
        fixture.Service.BuildAllNeighbors(true);
        Assert.IsTrue(a.Neighbors.Contains(b) && b.Neighbors.Contains(a));
    }

    [Test]
    public void StaticMode_DoesNotInferSameRoomOrSameAxisConnections()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        RoomWaypoint unconfigured = CreateWaypoint("Unconfigured", fixture.Room, Vector3.right * 2f);
        SetRoutes(fixture.Path, new StaticWaypointRoute("Main", StaticRouteDirection.ForwardOnly, new[] { a, b }));
        fixture.Service.RegisterRoomWaypoints(fixture.Room, new[] { a, b, unconfigured });
        Assert.IsTrue(fixture.Service.ConfigureGraph(WaypointGraphMode.StaticExplicit, fixture.Path));

        fixture.Service.BuildAllNeighbors(true);

        Assert.IsEmpty(unconfigured.Neighbors);
        CollectionAssert.DoesNotContain(b.Neighbors, unconfigured);
    }

    [Test]
    public void GeneratedMode_RetainsSameRoomAutomaticConnections()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint b = CreateWaypoint("B", fixture.Room, Vector3.right);
        a.IsAvailable = true;
        b.IsAvailable = true;
        fixture.Service.RegisterRoomWaypoints(fixture.Room, new[] { a, b });

        fixture.Service.BuildAllNeighbors();

        Assert.AreEqual(WaypointGraphMode.Generated, fixture.Service.GraphMode);
        CollectionAssert.Contains(a.Neighbors, b);
        CollectionAssert.Contains(b.Neighbors, a);
    }

    [Test]
    public void Validation_ReportsNullShortRouteAndDuplicateIdentity()
    {
        NavigationFixture fixture = CreateFixture();
        RoomWaypoint a = CreateWaypoint("A", fixture.Room, Vector3.zero);
        RoomWaypoint duplicate = CreateWaypoint("DuplicateA", fixture.Room, Vector3.zero);
        SetRoutes(
            fixture.Path,
            new StaticWaypointRoute("TooShort", StaticRouteDirection.Bidirectional, new[] { a }),
            new StaticWaypointRoute("NullEntry", StaticRouteDirection.Bidirectional, new RoomWaypoint[] { a, null }),
            new StaticWaypointRoute("DuplicateIdentity", StaticRouteDirection.Bidirectional, new[] { a, duplicate }));

        Assert.IsFalse(fixture.Path.ValidateRoutes(out List<string> errors, out _));
        Assert.IsTrue(errors.Exists(error => error.Contains("at least two")));
        Assert.IsTrue(errors.Exists(error => error.Contains("null waypoint")));
        Assert.IsTrue(errors.Exists(error => error.Contains("share identity")));
    }

    [Test]
    public void FindEnabledInScene_UsesAuthoredEnabledAndActiveState()
    {
        GameObject activeObject = CreateObject("ActiveStaticPath");
        StaticLevelPath activePath = activeObject.AddComponent<StaticLevelPath>();
        GameObject disabledObject = CreateObject("DisabledStaticPath");
        StaticLevelPath disabledPath = disabledObject.AddComponent<StaticLevelPath>();
        disabledPath.enabled = false;
        GameObject inactiveObject = CreateObject("InactiveStaticPath");
        StaticLevelPath inactivePath = inactiveObject.AddComponent<StaticLevelPath>();
        inactiveObject.SetActive(false);

        List<StaticLevelPath> found = StaticLevelPath.FindEnabledInScene();

        CollectionAssert.Contains(found, activePath);
        CollectionAssert.DoesNotContain(found, disabledPath);
        CollectionAssert.DoesNotContain(found, inactivePath);
    }

    private NavigationFixture CreateFixture()
    {
        GameObject serviceObject = CreateObject("WaypointServiceFixture");
        serviceObject.SetActive(false);
        WaypointRegistry registry = serviceObject.AddComponent<WaypointRegistry>();
        WaypointPathFinder pathFinder = serviceObject.AddComponent<WaypointPathFinder>();
        WaypointService service = serviceObject.AddComponent<WaypointService>();
        SetField(pathFinder, "registryBehaviour", registry);
        SetField(service, "registryBehaviour", registry);
        SetField(service, "pathFinderBehaviour", pathFinder);
        InvokeAwake(pathFinder);
        InvokeAwake(service);
        serviceObject.SetActive(true);

        GameObject roomObject = CreateObject("Room");
        RoomManager room = roomObject.AddComponent<RoomManager>();
        GameObject pathObject = CreateObject("StaticPath");
        StaticLevelPath path = pathObject.AddComponent<StaticLevelPath>();
        return new NavigationFixture(service, path, room);
    }

    private RoomWaypoint CreateWaypoint(string name, RoomManager room, Vector3 position)
    {
        GameObject waypointObject = CreateObject(name);
        waypointObject.transform.position = position;
        RoomWaypoint waypoint = waypointObject.AddComponent<RoomWaypoint>();
        waypoint.parentRoom = room;
        return waypoint;
    }

    private GameObject CreateObject(string name)
    {
        var createdObject = new GameObject(name);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static void SetRoutes(StaticLevelPath path, params StaticWaypointRoute[] routes)
    {
        SetField(path, "routes", new List<StaticWaypointRoute>(routes));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(awake, $"Missing Awake on {target.GetType().Name}");
        awake.Invoke(target, null);
    }

    private readonly struct NavigationFixture
    {
        public NavigationFixture(WaypointService service, StaticLevelPath path, RoomManager room)
        {
            Service = service;
            Path = path;
            Room = room;
        }

        public WaypointService Service { get; }
        public StaticLevelPath Path { get; }
        public RoomManager Room { get; }
    }
}

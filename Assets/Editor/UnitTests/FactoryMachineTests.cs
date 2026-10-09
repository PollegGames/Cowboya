using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FactoryMachineTests
{
    [Test]
    public void Awake_ResolvesSiblingConveyorFromGeneratedRoomHierarchy()
    {
        GameObject roomObject = new GameObject("GeneratedWorkRoom");
        roomObject.AddComponent<RoomManager>();

        GameObject conveyorObject = new GameObject("CubeController");
        conveyorObject.transform.SetParent(roomObject.transform);
        CubeConveyorController conveyor = conveyorObject.AddComponent<CubeConveyorController>();

        GameObject deskObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deskObject.name = "WorkingDesk";
        deskObject.transform.SetParent(roomObject.transform);
        FactoryMachine machine = deskObject.AddComponent<FactoryMachine>();

        FieldInfo conveyorField = typeof(FactoryMachine).GetField(
            "cubeConveyorController",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.AreSame(conveyor, conveyorField.GetValue(machine));

        Object.DestroyImmediate(roomObject);
    }

    [Test]
    public void WorkRoom_WorkWaypointOverlapsWorkingDeskSlot()
    {
        GameObject roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Map/ROOM_Work.prefab");
        GameObject room = Object.Instantiate(roomPrefab);

        RoomWaypoint workWaypoint = null;
        foreach (RoomWaypoint waypoint in room.GetComponentsInChildren<RoomWaypoint>(true))
        {
            if (waypoint.type == WaypointType.Work)
            {
                workWaypoint = waypoint;
                break;
            }
        }

        WorkerSlot workerSlot = room.GetComponentInChildren<WorkerSlot>(true);
        BoxCollider2D slotCollider = workerSlot != null
            ? workerSlot.GetComponent<BoxCollider2D>()
            : null;

        Assert.IsNotNull(workWaypoint);
        Assert.IsNotNull(slotCollider);
        Assert.IsTrue(
            slotCollider.OverlapPoint(workWaypoint.WorldPos),
            $"Work waypoint {workWaypoint.WorldPos} must overlap WorkingDesk slot {slotCollider.bounds}.");

        Object.DestroyImmediate(room);
    }
}

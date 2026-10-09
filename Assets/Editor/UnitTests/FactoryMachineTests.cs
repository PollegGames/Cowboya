using System.Reflection;
using NUnit.Framework;
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
}

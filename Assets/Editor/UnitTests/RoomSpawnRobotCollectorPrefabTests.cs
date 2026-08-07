using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class RoomSpawnRobotCollectorPrefabTests {
    private const string MapPrefabFolder = "Assets/Resources/Prefabs/Map/";
    private static readonly Vector3 ExpectedLocalPosition = new Vector3(-4.13f, 0f, 0.64f);

    [TestCase("ROOM_Furnace")]
    [TestCase("ROOM_Junks")]
    [TestCase("ROOM_Work")]
    [TestCase("ROOM_security")]
    [TestCase("ROOM_reception")]
    [TestCase("ROOM_Spawning")]
    [TestCase("ROOM_lift")]
    [TestCase("ROOM_Deads")]
    public void IncludedRoomHasOneConfiguredSpawnRobotCollector(string roomName) {
        GameObject room = LoadRoomPrefab(roomName);
        RoomManager roomManager = room.GetComponent<RoomManager>();
        SpawnRobotCollectorController[] collectors =
            room.GetComponentsInChildren<SpawnRobotCollectorController>(true);

        Assert.IsNotNull(roomManager, $"{roomName} must have a RoomManager on its root.");
        Assert.AreEqual(1, collectors.Length,
            $"{roomName} must contain exactly one SpawnRobotCollectorController.");

        SpawnRobotCollectorController collector = collectors[0];
        Assert.AreSame(room.transform, collector.transform.parent,
            $"The collector in {roomName} must be parented directly to the room root.");
        Assert.AreEqual(ExpectedLocalPosition, collector.transform.localPosition,
            $"The collector in {roomName} has an unexpected local position.");
        Assert.AreEqual(Quaternion.identity, collector.transform.localRotation,
            $"The collector in {roomName} must use identity local rotation.");
        Assert.AreEqual(Vector3.one, collector.transform.localScale,
            $"The collector in {roomName} must use unit local scale.");

        WarpMeshXYSkew[] warpMeshes = collector.GetComponentsInChildren<WarpMeshXYSkew>(true);
        MoveWithPlayerPosition[] movingElements =
            collector.GetComponentsInChildren<MoveWithPlayerPosition>(true);

        Assert.Greater(warpMeshes.Length, 0,
            $"The collector in {roomName} must contain WarpMeshXYSkew components.");
        Assert.Greater(movingElements.Length, 0,
            $"The collector in {roomName} must contain MoveWithPlayerPosition components.");

        foreach (WarpMeshXYSkew warpMesh in warpMeshes) {
            Assert.AreSame(roomManager, warpMesh.roomManager,
                $"{warpMesh.name} in {roomName} must reference the owning RoomManager.");
        }

        foreach (MoveWithPlayerPosition movingElement in movingElements) {
            Assert.AreSame(roomManager, movingElement.roomManager,
                $"{movingElement.name} in {roomName} must reference the owning RoomManager.");
        }
    }

    [TestCase("ROOM_Start")]
    [TestCase("ROOM_End")]
    public void ExcludedRoomHasNoSpawnRobotCollector(string roomName) {
        GameObject room = LoadRoomPrefab(roomName);
        SpawnRobotCollectorController[] collectors =
            room.GetComponentsInChildren<SpawnRobotCollectorController>(true);

        Assert.AreEqual(0, collectors.Length,
            $"{roomName} must not contain a SpawnRobotCollectorController.");
    }

    private static GameObject LoadRoomPrefab(string roomName) {
        string path = $"{MapPrefabFolder}{roomName}.prefab";
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(room, $"Could not load room prefab at {path}.");
        return room;
    }
}

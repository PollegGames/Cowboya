using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LaboratoryCubeMachineTests {
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown() {
        foreach (GameObject go in created) {
            if (go != null) {
                Object.DestroyImmediate(go);
            }
        }

        created.Clear();
    }

    [Test]
    public void WhiteMachine_ConsumesCarriedWhiteCubeFromSecondVisit() {
        LaboratoryProgress progress = ProgressOnSecondVisit(LaboratoryCubeType.White, 1);
        LaboratoryCubeMachine machine = CreateMachine(LaboratoryCubeMachineKind.WhiteCubeRoomBuilder);
        CubePickup cube = CreateCube<CubePickup>("WhiteCube");

        Assert.IsTrue(machine.TryAccept(cube, null, progress));

        Assert.AreEqual(1, progress.WhiteCubeMachineBank);
        Assert.AreEqual(0, progress.LaboratoryFreeWhiteCubeCount);
        Assert.IsTrue(cube == null, "The accepted cube is destroyed.");
    }

    [Test]
    public void WhiteMachine_RefusesColoredCube() {
        LaboratoryProgress progress = ProgressOnSecondVisit(LaboratoryCubeType.MaxEnergy, 1);
        LaboratoryCubeMachine machine = CreateMachine(LaboratoryCubeMachineKind.WhiteCubeRoomBuilder);
        CubePickup cube = CreateColoredCube(CubeUpgradeType.MaxEnergy);

        Assert.IsFalse(machine.TryAccept(cube, null, progress));
        Assert.IsTrue(cube != null);
        Assert.AreEqual(1, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxEnergy));
    }

    [Test]
    public void WhiteMachine_RefusesDuringFirstVisit() {
        var progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.White));
        Assert.IsTrue(progress.TryBeginVisit(1));
        LaboratoryCubeMachine machine = CreateMachine(LaboratoryCubeMachineKind.WhiteCubeRoomBuilder);
        CubePickup cube = CreateCube<CubePickup>("WhiteCube");

        Assert.IsFalse(machine.TryAccept(cube, null, progress));
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);
    }

    [Test]
    public void ResolveCubeType_MapsUpgradeComponents() {
        Assert.AreEqual(LaboratoryCubeType.White, LaboratoryCubeMachine.ResolveCubeType(CreateCube<CubePickup>("White")));
        Assert.AreEqual(LaboratoryCubeType.MaxHealth, LaboratoryCubeMachine.ResolveCubeType(CreateColoredCube(CubeUpgradeType.MaxHealth)));
        Assert.AreEqual(LaboratoryCubeType.MaxEnergy, LaboratoryCubeMachine.ResolveCubeType(CreateColoredCube(CubeUpgradeType.MaxEnergy)));
        Assert.AreEqual(LaboratoryCubeType.EnergyRecharge, LaboratoryCubeMachine.ResolveCubeType(CreateColoredCube(CubeUpgradeType.EnergyRecharge)));
        Assert.AreEqual(LaboratoryCubeType.AttackDamage, LaboratoryCubeMachine.ResolveCubeType(CreateColoredCube(CubeUpgradeType.AttackDamage)));
    }

    [Test]
    public void LaboratoryRoom_WithoutRunShowsOnlyTheSpawnRoom() {
        Assert.IsTrue(CreateRoom(LaboratoryRoomId.WhiteCubes).ResolveUnlocked(null));
        Assert.IsFalse(CreateRoom(LaboratoryRoomId.ColorCubes).ResolveUnlocked(null));
    }

    [Test]
    public void LaboratoryRoom_FollowsRunProgress() {
        LaboratoryProgress progress = ProgressOnSecondVisit(LaboratoryCubeType.White, 1);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit());

        Assert.IsTrue(CreateRoom(LaboratoryRoomId.ColorCubes).ResolveUnlocked(progress));
        Assert.IsFalse(CreateRoom(LaboratoryRoomId.Stats).ResolveUnlocked(progress));
    }

    [Test]
    public void PassageGate_LocksDoorsOfLockedRoomsOnly() {
        var lift = new GameObject("Lift");
        created.Add(lift);
        DoorController leftDoor = CreateDoor(lift.transform, Vector2.left);
        DoorController rightDoor = CreateDoor(lift.transform, Vector2.right);
        LaboratoryPassageGate gate = lift.AddComponent<LaboratoryPassageGate>();
        SetField(gate, "leftRoom", LaboratoryRoomId.WhiteCubes);
        SetField(gate, "rightRoom", LaboratoryRoomId.ColorCubes);

        gate.Apply(new LaboratoryProgress());
        Assert.IsFalse(leftDoor.IsExternallyLocked, "The spawn room is always reachable.");
        Assert.IsTrue(rightDoor.IsExternallyLocked, "The colored-cube room starts locked.");

        LaboratoryProgress progress = ProgressOnSecondVisit(LaboratoryCubeType.White, 1);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit());
        gate.Apply(progress);
        Assert.IsFalse(rightDoor.IsExternallyLocked);
    }

    private static DoorController CreateDoor(Transform parent, Vector2 direction) {
        var go = new GameObject(direction.x < 0f ? "LeftDoor" : "RightDoor");
        go.transform.SetParent(parent, false);
        DoorController door = go.AddComponent<DoorController>();
        door.moveDirection = direction;
        return door;
    }

    private static void SetField(object target, string field, object value) {
        target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    [Test]
    public void LaboratoryRoomPrefabs_HaveRoomIdsAndExitDoors() {
        AssertRoomPrefab("ROOM_Laboratory_1", LaboratoryRoomId.WhiteCubes, StaticDoorState.VictoryExit, StaticDoorState.Unlocked);
        AssertRoomPrefab("ROOM_Laboratory_2", LaboratoryRoomId.ColorCubes, StaticDoorState.VictoryExit, StaticDoorState.Unlocked);
        AssertRoomPrefab("ROOM_Laboratory_3", LaboratoryRoomId.Stats, StaticDoorState.Unlocked, StaticDoorState.VictoryExit);
        AssertRoomPrefab("ROOM_Laboratory_4", LaboratoryRoomId.Upgrades, StaticDoorState.Unlocked, StaticDoorState.VictoryExit);
    }

    private static void AssertRoomPrefab(string prefabName, LaboratoryRoomId expectedId, StaticDoorState left, StaticDoorState right) {
        string path = $"Assets/Resources/Prefabs/Map/{prefabName}.prefab";
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(prefab, path);

        LaboratoryRoom room = prefab.GetComponent<LaboratoryRoom>();
        Assert.IsNotNull(room, $"{prefabName} needs a LaboratoryRoom on its root.");
        Assert.AreEqual(expectedId, room.RoomId, prefabName);

        StaticRoomDoorConfig doors = prefab.GetComponent<StaticRoomDoorConfig>();
        Assert.IsNotNull(doors, prefabName);
        Assert.AreEqual(left, doors.LeftDoorState, $"{prefabName} left door");
        Assert.AreEqual(right, doors.RightDoorState, $"{prefabName} right door");

        LaboratoryExitPoint exit = prefab.GetComponentInChildren<LaboratoryExitPoint>(true);
        Assert.IsNotNull(exit, $"{prefabName} needs an authored LaboratoryExitPoint.");
        Assert.IsTrue(exit.GetComponent<Collider2D>().isTrigger, $"{prefabName} exit must be a trigger.");
        Assert.AreEqual(Quaternion.identity, exit.transform.rotation, $"{prefabName} exit must stay in the XY plane.");

        LaboratoryCubeMachine machine = prefab.GetComponentInChildren<LaboratoryCubeMachine>(true);
        if (expectedId == LaboratoryRoomId.WhiteCubes || expectedId == LaboratoryRoomId.ColorCubes) {
            Assert.IsNotNull(machine, $"{prefabName} needs an authored CubeIntake.");
            Assert.AreEqual(
                expectedId == LaboratoryRoomId.WhiteCubes
                    ? LaboratoryCubeMachineKind.WhiteCubeRoomBuilder
                    : LaboratoryCubeMachineKind.ColorCubeUpgrade,
                machine.Kind,
                prefabName);
            Assert.AreEqual(Quaternion.identity, machine.transform.rotation, $"{prefabName} intake must stay in the XY plane.");
        }
        else {
            Assert.IsNull(machine, $"{prefabName} has no cube machine yet.");
        }
    }

    private LaboratoryProgress ProgressOnSecondVisit(LaboratoryCubeType freeType, int amount) {
        var progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryBeginVisit(1));
        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.IsTrue(progress.TryStoreIncomingCube(freeType, amount));
        Assert.IsTrue(progress.TryBeginVisit(3));
        return progress;
    }

    private LaboratoryCubeMachine CreateMachine(LaboratoryCubeMachineKind kind) {
        var go = new GameObject("CubeIntake");
        created.Add(go);
        go.AddComponent<BoxCollider2D>();
        LaboratoryCubeMachine machine = go.AddComponent<LaboratoryCubeMachine>();
        machine.Configure(kind);
        return machine;
    }

    private T CreateCube<T>(string name) where T : Component {
        var go = new GameObject(name);
        created.Add(go);
        go.AddComponent<BoxCollider2D>();
        return go.AddComponent<T>();
    }

    private CubePickup CreateColoredCube(CubeUpgradeType type) {
        CubePickup cube = CreateCube<CubePickup>($"{type}Cube");
        CubeUpgrade upgrade = cube.gameObject.AddComponent<CubeUpgrade>();
        typeof(CubeUpgrade)
            .GetField("upgradeType", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(upgrade, type);
        return cube;
    }

    private LaboratoryRoom CreateRoom(LaboratoryRoomId id) {
        var go = new GameObject($"Room_{id}");
        created.Add(go);
        LaboratoryRoom room = go.AddComponent<LaboratoryRoom>();
        typeof(LaboratoryRoom)
            .GetField("roomId", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(room, id);
        return room;
    }
}

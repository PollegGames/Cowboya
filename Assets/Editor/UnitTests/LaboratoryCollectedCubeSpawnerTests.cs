using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class LaboratoryCollectedCubeSpawnerTests {
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown() {
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null) {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Snapshot_SpawnsExactPrefabsInRoundRobinAsFreeFallingCubes() {
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.White, 2));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxHealth, 2));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxEnergy));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.EnergyRecharge));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.AttackDamage));
        Assert.IsTrue(progress.TryBeginVisit(7));
        int[] freeCountsBeforePresentation = progress.GetLaboratoryFreeCubeSnapshot();

        Transform origin = CreateObject("Drop origin").transform;
        origin.position = new Vector3(4f, 6f, -3f);
        Transform output = CreateObject("Spawned cubes").transform;
        CubePickup white = CreateCubePrefab("White");
        CubePickup maxHealth = CreateCubePrefab("MaxHealth");
        CubePickup maxEnergy = CreateCubePrefab("MaxEnergy");
        CubePickup recharge = CreateCubePrefab("EnergyRecharge");
        CubePickup attack = CreateCubePrefab("AttackDamage");
        LaboratoryCollectedCubeSpawner spawner = CreateObject("Spawner")
            .AddComponent<LaboratoryCollectedCubeSpawner>();
        spawner.enabled = false;
        spawner.Configure(
            origin,
            output,
            white,
            maxHealth,
            maxEnergy,
            recharge,
            attack,
            intervalSeconds: 0.05f,
            height: 3f,
            scatter: 0f,
            depth: 0.125f);

        Assert.IsTrue(spawner.InitializeForVisit(progress, 7));
        while (InvokeTrySpawnNextCube(spawner)) {
        }

        CubePickup[] spawned = output.GetComponentsInChildren<CubePickup>();
        Assert.AreEqual(7, spawned.Length);
        CollectionAssert.AreEqual(
            new[] {
                "White(Clone)",
                "MaxHealth(Clone)",
                "MaxEnergy(Clone)",
                "EnergyRecharge(Clone)",
                "AttackDamage(Clone)",
                "White(Clone)",
                "MaxHealth(Clone)"
            },
            GetNames(spawned));

        for (int i = 0; i < spawned.Length; i++) {
            Rigidbody2D body = spawned[i].GetComponent<Rigidbody2D>();
            TargetJoint2D joint = spawned[i].GetComponent<TargetJoint2D>();

            Assert.IsNotNull(body);
            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
            Assert.IsTrue(body.simulated);
            Assert.AreEqual(Vector2.zero, body.linearVelocity);
            Assert.AreEqual(0f, body.angularVelocity);
            Assert.IsNotNull(joint);
            Assert.IsFalse(joint.enabled);
            Assert.AreEqual(new Vector3(4f, 9f, 0.125f), spawned[i].transform.position);
        }

        Assert.AreEqual(7L, spawner.SnapshotCubeCount);
        Assert.AreEqual(7L, spawner.SpawnedCubeCount);
        Assert.AreEqual(0L, spawner.RemainingCubeCount);
        CollectionAssert.AreEqual(
            freeCountsBeforePresentation,
            progress.GetLaboratoryFreeCubeSnapshot());
    }

    [Test]
    public void InitializeForVisit_RepeatingVisitDoesNotResnapshotOrDuplicate() {
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.White));
        Assert.IsTrue(progress.TryBeginVisit(2));

        Transform output = CreateObject("Spawned cubes").transform;
        CubePickup white = CreateCubePrefab("White");
        LaboratoryCollectedCubeSpawner spawner = CreateObject("Spawner")
            .AddComponent<LaboratoryCollectedCubeSpawner>();
        spawner.enabled = false;
        spawner.Configure(
            spawner.transform,
            output,
            white,
            null,
            null,
            null,
            null,
            scatter: 0f);

        Assert.IsTrue(spawner.InitializeForVisit(progress, 2));
        Assert.IsTrue(InvokeTrySpawnNextCube(spawner));
        Assert.IsFalse(InvokeTrySpawnNextCube(spawner));
        Assert.AreEqual(1, output.childCount);

        Assert.IsTrue(spawner.InitializeForVisit(progress, 2));

        Assert.IsFalse(InvokeTrySpawnNextCube(spawner));
        Assert.AreEqual(1, output.childCount);
        Assert.AreEqual(1L, spawner.SnapshotCubeCount);
        Assert.AreEqual(1, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));
    }

    [Test]
    public void InitializeForVisit_MissingRequiredExactPrefabRejectsWholeSnapshot() {
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.AttackDamage));
        Assert.IsTrue(progress.TryBeginVisit(4));

        LaboratoryCollectedCubeSpawner spawner = CreateObject("Spawner")
            .AddComponent<LaboratoryCollectedCubeSpawner>();
        spawner.enabled = false;
        spawner.Configure(
            spawner.transform,
            null,
            CreateCubePrefab("White"),
            CreateCubePrefab("MaxHealth"),
            CreateCubePrefab("MaxEnergy"),
            CreateCubePrefab("EnergyRecharge"),
            null);
        LogAssert.Expect(
            LogType.Error,
            "LaboratoryCollectedCubeSpawner is missing the AttackDamage cube prefab "
            + "required by visit 4.");

        Assert.IsFalse(spawner.InitializeForVisit(progress, 4));

        Assert.AreEqual(-1, spawner.InitializedVisitId);
        Assert.AreEqual(0L, spawner.SnapshotCubeCount);
        Assert.AreEqual(0L, spawner.SpawnedCubeCount);
        Assert.AreEqual(
            1,
            progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.AttackDamage));
    }

    [Test]
    public void SpawnCubesCollectedPrefab_HasSpawnerOriginAndEveryCubePrefab() {
        GameObject machinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Map/Basic/Machines/SpawnCubesCollected.prefab");

        Assert.IsNotNull(machinePrefab);
        LaboratoryCollectedCubeSpawner spawner =
            machinePrefab.GetComponent<LaboratoryCollectedCubeSpawner>();
        Assert.IsNotNull(spawner);

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        AssertAssigned(serializedSpawner, "spawnOrigin", "SpawnPoint");
        AssertAssigned(serializedSpawner, "whiteCubePrefab", "CubeNormal");
        AssertAssigned(serializedSpawner, "maxHealthCubePrefab", "CubeMaxHealth");
        AssertAssigned(serializedSpawner, "maxEnergyCubePrefab", "CubeMaxEnergy");
        AssertAssigned(serializedSpawner, "energyRechargeCubePrefab", "CubeReloadEnergy");
        AssertAssigned(serializedSpawner, "attackDamageCubePrefab", "CubeAttackDamage");
        Assert.Greater(
            serializedSpawner.FindProperty("fallHeight").floatValue,
            0f);
        Assert.Greater(
            serializedSpawner.FindProperty("spawnIntervalSeconds").floatValue,
            0f);

        GameObject laboratoryRoom = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Map/ROOM_Laboratory_1.prefab");
        Assert.IsNotNull(laboratoryRoom);
        Assert.IsNotNull(
            laboratoryRoom.GetComponentInChildren<LaboratoryCollectedCubeSpawner>(true));
    }

    private CubePickup CreateCubePrefab(string objectName) {
        GameObject cubeObject = CreateObject(objectName);
        cubeObject.AddComponent<BoxCollider2D>();
        CubePickup cube = cubeObject.AddComponent<CubePickup>();
        Rigidbody2D body = cube.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = false;
        cube.GetComponent<TargetJoint2D>().enabled = true;
        return cube;
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static bool InvokeTrySpawnNextCube(
        LaboratoryCollectedCubeSpawner spawner) {
        MethodInfo method = typeof(LaboratoryCollectedCubeSpawner).GetMethod(
            "TrySpawnNextCube",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (bool)method.Invoke(spawner, null);
    }

    private static string[] GetNames(CubePickup[] cubes) {
        string[] names = new string[cubes.Length];
        for (int i = 0; i < cubes.Length; i++) {
            names[i] = cubes[i].name;
        }

        return names;
    }

    private static void AssertAssigned(
        SerializedObject serializedObject,
        string propertyName,
        string expectedObjectName) {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.IsNotNull(property, propertyName);
        Assert.IsNotNull(property.objectReferenceValue, propertyName);
        Assert.AreEqual(expectedObjectName, property.objectReferenceValue.name, propertyName);
    }
}

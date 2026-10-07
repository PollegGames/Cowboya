using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WorkerCollectorServiceProviderTests {
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown() {
        for (int i = created.Count - 1; i >= 0; i--) {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void Source_SelectsNearestAvailableCubeInsteadOfFirstHierarchyEntry() {
        WorkerCollectorWhiteCubeSourceProvider source = Create("Source")
            .AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        WhiteCubeCargo first = CreateCargo(source.transform, "First", new Vector2(2f, 0f));
        WhiteCubeCargo nearest = CreateCargo(source.transform, "Nearest", new Vector2(0.5f, 0f));
        WhiteCubeCargo held = CreateCargo(source.transform, "PlayerHeld", new Vector2(0.1f, 0f));
        held.Pickup.OnGrab(Create("PlayerHand").transform);
        WhiteCubeCargo inactive = CreateCargo(source.transform, "Inactive", Vector2.zero);
        inactive.gameObject.SetActive(false);

        Assert.IsTrue(source.TryClaimCube(Create("Worker"), out WhiteCubeCargo selected, out WhiteCubeClaim claim));

        Assert.AreSame(nearest, selected);
        Assert.IsTrue(nearest.IsClaimValid(claim));
        Assert.IsTrue(first.IsAvailable);
    }

    [Test]
    public void Source_CannotClaimAcrossTheLevelFromSpawn() {
        WorkerCollectorWhiteCubeSourceProvider source = Create("Source")
            .AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        WhiteCubeCargo cargo = CreateCargo(source.transform, "Cube", new Vector2(20f, 0f));
        GameObject worker = Create("Worker");

        Assert.IsFalse(source.TryClaimCube(worker, out _, out _));
        Assert.IsTrue(cargo.IsAvailable);

        worker.transform.position = new Vector3(19f, 0f, 0f);
        Assert.IsTrue(source.TryClaimCube(worker, out WhiteCubeCargo selected, out _));
        Assert.AreSame(cargo, selected);
    }

    [Test]
    public void Conveyor_ContinuesMovingClaimedCubeUntilItIsGrabbed() {
        GameObject conveyorObject = Create("Conveyor");
        GarageCubeConveyorController conveyor = conveyorObject.AddComponent<GarageCubeConveyorController>();
        WhiteCubeCargo cargo = CreateCargo(conveyorObject.transform, "Cube", Vector2.zero);
        Transform exit = Create("Exit").transform;
        exit.position = new Vector3(10f, 0f, 0f);
        SetField(conveyor, "exitPoint", exit);
        SetField(conveyor, "currentCube", cargo.Pickup);
        SetField(conveyor, "speed", 2f);
        Assert.IsTrue(cargo.TryClaim(Create("Worker"), out WhiteCubeClaim claim));

        conveyor.StepMovement(0.5f);

        Assert.AreEqual(1f, cargo.transform.position.x, 0.001f);
        Assert.IsTrue(cargo.IsClaimValid(claim));
        Assert.AreEqual(WhiteCubeCargoState.Claimed, cargo.State);
        // Leave normal fixture teardown responsible for destroying this edit-mode cube.
        SetField(conveyor, "currentCube", null);
    }

    private WhiteCubeCargo CreateCargo(Transform parent, string name, Vector2 position) {
        GameObject cube = Create(name);
        cube.transform.SetParent(parent, false);
        Rigidbody2D body = cube.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.position = position;
        cube.AddComponent<TargetJoint2D>();
        cube.AddComponent<BoxCollider2D>();
        CubePickup pickup = cube.AddComponent<CubePickup>();
        typeof(CubePickup).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(pickup, null);
        return cube.AddComponent<WhiteCubeCargo>();
    }

    private GameObject Create(string name) {
        GameObject result = new GameObject(name);
        created.Add(result);
        return result;
    }

    private static void SetField(object target, string name, object value) {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        field.SetValue(target, value);
    }
}

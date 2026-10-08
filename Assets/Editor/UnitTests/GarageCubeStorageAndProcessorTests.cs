using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GarageCubeStorageAndProcessorTests
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
    public void Storage_FillsBottomFirst_AndPlayerRemovalPreservesExactHole()
    {
        GarageSetup setup = CreateGarage();
        int interruptedCount = 0;
        setup.Processor.OnBatchInterrupted += () => interruptedCount++;
        WhiteCubeCargo[] cubes = Fill(setup.Storage, 9);

        Assert.AreEqual(9, setup.Storage.OccupiedCount);
        for (int i = 0; i < cubes.Length; i++)
        {
            Assert.AreSame(cubes[i], setup.Storage.GetStoredCube(i));
            Assert.AreSame(setup.Storage.Slots[i], cubes[i].transform.parent);
            Assert.AreEqual(RigidbodyType2D.Kinematic, cubes[i].GetComponent<Rigidbody2D>().bodyType);
        }

        Transform playerHand = Create("PlayerHand").transform;
        cubes[4].Pickup.OnGrab(playerHand);

        Assert.AreEqual(8, setup.Storage.OccupiedCount);
        Assert.AreEqual(1, interruptedCount);
        Assert.IsNull(setup.Storage.GetStoredCube(4));
        Assert.AreEqual(RigidbodyType2D.Dynamic, cubes[4].GetComponent<Rigidbody2D>().bodyType);

        WhiteCubeCargo replacement = CreateCargo("Replacement");
        GameObject owner = Create("ReplacementOwner");
        Assert.IsTrue(replacement.TryClaim(owner, out WhiteCubeClaim claim));
        Assert.IsTrue(setup.Storage.TryReserve(owner, out GarageSlotReservation reservation));
        Assert.AreEqual(4, reservation.SlotIndex);
        Assert.IsTrue(setup.Storage.TryAccept(replacement, claim, reservation));
        Assert.AreSame(replacement, setup.Storage.GetStoredCube(4));
    }

    [Test]
    public void Storage_KeepsAcceptedCubeUpright_WhenSlotIsRotated()
    {
        GarageSetup setup = CreateGarage();
        setup.Storage.Slots[0].rotation = Quaternion.Euler(90f, 0f, 0f);
        WhiteCubeCargo cargo = CreateCargo("UprightCube");
        GameObject owner = Create("UprightCubeOwner");

        Assert.IsTrue(cargo.TryClaim(owner, out WhiteCubeClaim claim));
        Assert.IsTrue(setup.Storage.TryReserve(owner, out GarageSlotReservation reservation));
        Assert.IsTrue(setup.Storage.TryAccept(cargo, claim, reservation));

        Assert.Less(Quaternion.Angle(Quaternion.identity, cargo.transform.rotation), 0.01f);
        Assert.AreEqual(new Vector3(0f, 0.01f, 0f), cargo.transform.localPosition);
    }

    [Test]
    public void Processor_InterruptedClosing_ReopensWithoutProcessing()
    {
        GarageSetup setup = CreateGarage();
        WhiteCubeCargo[] cubes = Fill(setup.Storage, 9);

        Assert.AreEqual(GarageProcessorState.Closing, setup.Processor.State);
        cubes[8].Pickup.OnGrab(Create("PlayerHand").transform);

        Assert.AreEqual(8, setup.Storage.OccupiedCount);
        Assert.AreEqual(GarageProcessorState.Opening, setup.Processor.State);
        setup.Door.StepAnimation(2f);
        setup.Processor.StepProcessing(2f);
        Assert.AreEqual(0, setup.Processor.CompletedBatchCount);
        Assert.AreEqual(8, setup.Storage.OccupiedCount);
        Assert.IsTrue(setup.Door.IsOpen);
    }

    [Test]
    public void Processor_PlayerRemovalDuringClosedHold_ReportsInterruptedAndReopens() {
        GarageSetup setup = CreateGarage();
        int interruptedCount = 0;
        setup.Processor.OnBatchInterrupted += () => interruptedCount++;
        WhiteCubeCargo[] cubes = Fill(setup.Storage, 9);
        setup.Door.StepAnimation(2f);
        Assert.AreEqual(GarageProcessorState.Processing, setup.Processor.State);

        cubes[4].Pickup.OnGrab(Create("PlayerHand").transform);
        setup.Processor.StepProcessing(2f);

        Assert.AreEqual(1, interruptedCount);
        Assert.AreEqual(8, setup.Storage.OccupiedCount);
        Assert.AreEqual(0, setup.Processor.CompletedBatchCount);
        Assert.AreEqual(GarageProcessorState.Opening, setup.Processor.State);
        setup.Door.StepAnimation(2f);
        Assert.IsTrue(setup.Processor.IsAccepting);
    }

    [Test]
    public void Processor_ValidClosedBatch_RemovesExactlyNine_AndReopens()
    {
        GarageSetup setup = CreateGarage();
        int eventCount = 0;
        setup.Processor.OnBatchCompleted += _ => eventCount++;
        Fill(setup.Storage, 9);

        setup.Door.StepAnimation(2f);
        Assert.AreEqual(GarageProcessorState.Processing, setup.Processor.State);
        setup.Processor.StepProcessing(1f);

        Assert.AreEqual(0, setup.Storage.OccupiedCount);
        Assert.AreEqual(1, setup.Processor.CompletedBatchCount);
        Assert.AreEqual(1, eventCount);
        Assert.AreEqual(GarageProcessorState.Opening, setup.Processor.State);
        setup.Door.StepAnimation(2f);
        Assert.IsTrue(setup.Door.IsOpen);
        Assert.IsTrue(setup.Processor.IsAccepting);
    }

    [Test]
    public void Disable_ReleasesOutstandingReservation()
    {
        GarageSetup setup = CreateGarage();
        GameObject owner = Create("Owner");
        Assert.IsTrue(setup.Storage.TryReserve(owner, out GarageSlotReservation reservation));

        setup.Storage.enabled = false;
        InvokePrivate(setup.Storage, "OnDisable");

        Assert.IsFalse(setup.Storage.IsReservationValid(reservation));
    }

    private GarageSetup CreateGarage()
    {
        GameObject root = Create("Garage");
        root.SetActive(false);
        Transform slotsRoot = Create("Slots").transform;
        slotsRoot.SetParent(root.transform, false);
        Transform[] slots = new Transform[GarageCubeStorage.Capacity];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = Create($"Slot_{i:00}").transform;
            slots[i].SetParent(slotsRoot, false);
        }

        GameObject panel = Create("Panel");
        panel.transform.SetParent(root.transform, false);
        panel.AddComponent<MeshRenderer>();
        panel.AddComponent<BoxCollider>();
        GarageDoorController door = root.AddComponent<GarageDoorController>();
        GarageCubeStorage storage = root.AddComponent<GarageCubeStorage>();
        storage.ConfigureSlots(slots);
        GarageCubeProcessor processor = root.AddComponent<GarageCubeProcessor>();
        processor.Configure(storage, door, 0.5f);
        root.SetActive(true);
        InvokePrivate(storage, "OnEnable");
        InvokePrivate(processor, "OnEnable");
        door.SetOpenImmediate();
        return new GarageSetup(storage, processor, door);
    }

    private WhiteCubeCargo[] Fill(GarageCubeStorage storage, int count)
    {
        WhiteCubeCargo[] cubes = new WhiteCubeCargo[count];
        for (int i = 0; i < count; i++)
        {
            WhiteCubeCargo cargo = CreateCargo("Cube_" + i);
            GameObject owner = Create("Owner_" + i);
            Assert.IsTrue(cargo.TryClaim(owner, out WhiteCubeClaim claim));
            Assert.IsTrue(storage.TryReserve(owner, out GarageSlotReservation reservation));
            Assert.IsTrue(storage.TryAccept(cargo, claim, reservation));
            cubes[i] = cargo;
        }
        return cubes;
    }

    private WhiteCubeCargo CreateCargo(string name)
    {
        GameObject go = Create(name);
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<TargetJoint2D>();
        go.AddComponent<BoxCollider2D>();
        CubePickup pickup = go.AddComponent<CubePickup>();
        InvokePrivate(pickup, "Awake");
        return go.AddComponent<WhiteCubeCargo>();
    }

    private GameObject Create(string name)
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private static void InvokePrivate(object owner, string methodName)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Missing {methodName} on {owner.GetType().Name}.");
        method.Invoke(owner, null);
    }

    private readonly struct GarageSetup
    {
        public GarageSetup(GarageCubeStorage storage, GarageCubeProcessor processor, GarageDoorController door)
        {
            Storage = storage;
            Processor = processor;
            Door = door;
        }
        public GarageCubeStorage Storage { get; }
        public GarageCubeProcessor Processor { get; }
        public GarageDoorController Door { get; }
    }
}

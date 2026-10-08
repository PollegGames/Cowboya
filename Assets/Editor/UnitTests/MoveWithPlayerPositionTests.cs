using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MoveWithPlayerPositionTests {
    private GameObject root;
    private Mesh originalMesh;
    private Mesh warpedMesh;

    [SetUp]
    public void SetUp() {
        root = new GameObject("MoveWithPlayerPositionTests");
    }

    [TearDown]
    public void TearDown() {
        Object.DestroyImmediate(root);
        if (warpedMesh != null)
            Object.DestroyImmediate(warpedMesh);
        if (originalMesh != null)
            Object.DestroyImmediate(originalMesh);
    }

    [Test]
    public void DefaultPlane_PreservesXYMovementAndLocalDepth() {
        Transform player = CreateTransform("Player", root.transform);
        MoveWithPlayerPosition mover = CreateMover("Marker", root.transform,
            new Vector3(2f, 3f, 9f), player);
        player.localPosition = new Vector3(-100f, 100f, 200f);

        InvokePrivate(mover, "LateUpdate");

        Assert.AreEqual(MoveWithPlayerPosition.MovementPlane.XY, mover.movementPlane);
        AssertVector(new Vector3(1f, 4f, 9f), mover.transform.localPosition);
    }

    [Test]
    public void XZPlane_UnderRotatedRoomMovesOnWorldXYAndPreservesDepth() {
        Transform room = CreateTransform("RotatedRoom", root.transform);
        room.position = new Vector3(10f, 20f, 5f);
        room.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Transform player = CreateTransform("Player", room);
        MoveWithPlayerPosition mover = CreateMover("Marker", room,
            new Vector3(1f, 2f, 3f), player);
        mover.movementPlane = MoveWithPlayerPosition.MovementPlane.XZ;
        mover.trackingCenter = room;
        player.localPosition = new Vector3(4f, 50f, 2f);
        Vector3 startingWorldPosition = mover.transform.position;

        InvokePrivate(mover, "LateUpdate");

        AssertVector(new Vector3(1.8f, 2f, 3.4f), mover.transform.localPosition);
        AssertVector(new Vector3(0.8f, 0.4f, 0f), mover.transform.position - startingWorldPosition);
    }

    [TestCase(-30f, -10f)]
    [TestCase(-4f, -2f)]
    [TestCase(4f, 2f)]
    [TestCase(30f, 10f)]
    public void SharedCenterAndGarageCalibration_KeepMarkersAlignedWithWarpedInterior(
        float playerX, float playerZ) {
        Transform room = CreateTransform("RotatedRoom", root.transform);
        room.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Transform garage = CreateTransform("GarageCubes", room);
        garage.localPosition = new Vector3(-4.99f, 0f, -4.3f);
        Transform slots = CreateTransform("Slots", garage);
        Transform background = CreateTransform("Background", garage);
        background.localPosition = new Vector3(0.99f, 0.01f, 3.55f);
        Transform player = CreateTransform("Player", root.transform);
        player.position = background.TransformPoint(new Vector3(playerX, 0f, playerZ));

        originalMesh = new Mesh();
        originalMesh.vertices = new[] {
            new Vector3(-2f, 0f, -2f), new Vector3(0f, 0f, -2f), new Vector3(2f, 0f, -2f),
            new Vector3(-2f, 0f, 0f), Vector3.zero, new Vector3(2f, 0f, 0f),
            new Vector3(-2f, 0f, 2f), new Vector3(0f, 0f, 2f), new Vector3(2f, 0f, 2f)
        };
        MeshFilter meshFilter = background.gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = originalMesh;
        WarpMeshXYSkew warp = background.gameObject.AddComponent<WarpMeshXYSkew>();
        warp.player = player;
        warp.warpFactorX = 0.35f;
        warp.maxWarpX = 7.5f;
        warp.warpFactorZ = 0.3f;
        warp.maxWarpZ = 1.5f;
        InvokePrivate(warp, "Start");
        warpedMesh = meshFilter.sharedMesh;

        MoveWithPlayerPosition[] markers = {
            CreateMover("Slot_00", slots, new Vector3(-0.61f, -0.1f, 0.74f), player),
            CreateMover("Slot_08", slots, new Vector3(1.18f, -0.1f, 2.88f), player),
            CreateMover("DeliveryPoint", garage, new Vector3(0.49f, 0f, 1.82f), player)
        };
        InvokePrivate(warp, "Update");
        Vector3 visualDisplacement = background.TransformVector(warpedMesh.vertices[4]);

        foreach (MoveWithPlayerPosition marker in markers) {
            marker.movementPlane = MoveWithPlayerPosition.MovementPlane.XZ;
            marker.trackingCenter = background;
            marker.horizontalRange = 21.428571f;
            marker.verticalRange = 5f;
            marker.maxLeft = marker.maxRight = 7.5f;
            marker.maxDown = marker.maxUp = 1.5f;
            Vector3 startingPosition = marker.transform.position;

            InvokePrivate(marker, "LateUpdate");

            AssertVector(visualDisplacement, marker.transform.position - startingPosition);
        }
    }

    private MoveWithPlayerPosition CreateMover(string name, Transform parent,
        Vector3 localPosition, Transform player) {
        Transform marker = CreateTransform(name, parent);
        marker.localPosition = localPosition;
        MoveWithPlayerPosition mover = marker.gameObject.AddComponent<MoveWithPlayerPosition>();
        mover.player = player;
        mover.smoothTime = 0f;
        InvokePrivate(mover, "Awake");
        return mover;
    }

    private static Transform CreateTransform(string name, Transform parent) {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void AssertVector(Vector3 expected, Vector3 actual) {
        Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.0001f));
    }

    private static void InvokePrivate(object target, string methodName) {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(target, null);
    }
}

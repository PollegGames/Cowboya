using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class GrabHandAttractorTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private class DummyGrabbable : MonoBehaviour, IGrabbable
    {
        public bool CanBeGrabbed(Inventory inventory) => true;
        public void OnGrab(Transform grabParent) {}
        public void OnRelease(Vector2 throwForce) {}
        public void OnAttract(Vector2 attractPoint) {}
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    private GameObject CreateGameObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    [Test]
    public void DetectGrabbable_DetectsObjectOnLayer()
    {
        var handObj = CreateGameObject("hand");
        var attractor = handObj.AddComponent<GrabHandAttractor>();
        attractor.detectionRadius = 1f;
        int layer = 8;
        attractor.detectionLayer = 1 << layer;

        var obj = CreateGameObject("grabbable");
        obj.layer = layer;
        obj.transform.position = handObj.transform.position;
        obj.AddComponent<CircleCollider2D>();
        var grabbable = obj.AddComponent<DummyGrabbable>();

        bool eventCalled = false;
        attractor.OnObjectDetected += g => eventCalled = true;

        var detected = attractor.DetectGrabbable();

        Assert.AreEqual(grabbable, detected);
        Assert.IsTrue(eventCalled);
    }

    [Test]
    public void DetectGrabbable_IgnoresWrongLayer()
    {
        var handObj = CreateGameObject("hand");
        var attractor = handObj.AddComponent<GrabHandAttractor>();
        attractor.detectionRadius = 1f;
        attractor.detectionLayer = 1 << 8;

        var obj = CreateGameObject("grabbable");
        obj.layer = 9;
        obj.transform.position = handObj.transform.position;
        obj.AddComponent<CircleCollider2D>();
        obj.AddComponent<DummyGrabbable>();

        var detected = attractor.DetectGrabbable();

        Assert.IsNull(detected);
    }
}

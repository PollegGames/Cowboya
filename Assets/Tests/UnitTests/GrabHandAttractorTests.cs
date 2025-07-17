using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class GrabHandAttractorTests
{
    private class DummyGrabbable : MonoBehaviour, IGrabbable
    {
        public bool CanBeGrabbed() => true;
        public void OnGrab(Transform grabParent) {}
        public void OnRelease(Vector2 throwForce) {}
        public void OnAttract(Vector2 attractPoint) {}
    }

    [Test]
    public void DetectGrabbable_DetectsObjectOnLayer()
    {
        var handObj = new GameObject("hand");
        var attractor = handObj.AddComponent<GrabHandAttractor>();
        attractor.detectionRadius = 1f;
        int layer = 8;
        attractor.detectionLayer = 1 << layer;

        var obj = new GameObject("grabbable");
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
        var handObj = new GameObject("hand");
        var attractor = handObj.AddComponent<GrabHandAttractor>();
        attractor.detectionRadius = 1f;
        attractor.detectionLayer = 1 << 8;

        var obj = new GameObject("grabbable");
        obj.layer = 9;
        obj.transform.position = handObj.transform.position;
        obj.AddComponent<CircleCollider2D>();
        obj.AddComponent<DummyGrabbable>();

        var detected = attractor.DetectGrabbable();

        Assert.IsNull(detected);
    }
}

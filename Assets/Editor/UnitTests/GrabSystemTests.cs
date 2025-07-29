using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class GrabSystemTests
{
    private class DummyInput : MonoBehaviour, IPlayerInput
    {
        public Vector2 Movement => Vector2.zero;
        public bool JumpPressed => false;
        public bool PrimaryAttack => false;

        public bool LeftGrabDown  { get; private set; }
        public bool LeftGrabHeld  { get; private set; }
        public bool LeftGrabUp    { get; private set; }

        public bool RightGrabDown => LeftGrabDown;
        public bool RightGrabHeld => LeftGrabHeld;
        public bool RightGrabUp   => LeftGrabUp;

        public void PressGrab()
        {
            LeftGrabDown = true;
            LeftGrabHeld = true;
        }

        public void ReleaseGrab()
        {
            LeftGrabHeld = false;
            LeftGrabUp = true;
        }

        public void NextFrame()
        {
            LeftGrabDown = false;
            LeftGrabUp = false;
        }
    }

    private class DummyGrabbable : MonoBehaviour, IGrabbable
    {
        public bool grabbed;
        public bool released;
        public Vector2 releasedForce;
        public bool CanBeGrabbed() => true;
        public void OnGrab(Transform grabParent)
        {
            grabbed = true;
            transform.SetParent(grabParent);
        }
        public void OnRelease(Vector2 throwForce)
        {
            released = true;
            releasedForce = throwForce;
            transform.SetParent(null);
        }
        public void OnAttract(Vector2 attractPoint) {}
    }

    [Test]
    public void GrabSystem_GrabAndReleaseObject()
    {
        // setup hand
        var handObj = new GameObject("hand");
        var attractor = handObj.AddComponent<GrabHandAttractor>();
        attractor.detectionRadius = 1f;
        int layer = 8;
        attractor.detectionLayer = 1 << layer;

        // object to grab
        var obj = new GameObject("grab");
        obj.layer = layer;
        obj.AddComponent<CircleCollider2D>();
        var grab = obj.AddComponent<DummyGrabbable>();
        obj.transform.position = handObj.transform.position;

        // grab system
        var systemObj = new GameObject("system");
        var system = systemObj.AddComponent<GrabSystem>();
        system.leftHand = attractor;
        system.throwStrength = 2f;
        var inputObj = new GameObject("input");
        var input = inputObj.AddComponent<DummyInput>();
        typeof(GrabSystem).GetField("inputSource", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(system, input);
        typeof(GrabSystem).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(system, null);

        var leftHeldField = typeof(GrabSystem).GetField("leftHeld", BindingFlags.NonPublic | BindingFlags.Instance);

        // grab
        input.PressGrab();
        // system.Update();
        input.NextFrame();

        Assert.IsTrue(grab.grabbed);
        Assert.AreEqual(grab, leftHeldField.GetValue(system));

        // release
        input.ReleaseGrab();
        // system.Update();
        input.NextFrame();

        Assert.IsTrue(grab.released);
        Assert.AreEqual(Vector2.right * system.throwStrength, grab.releasedForce);
        Assert.IsNull(leftHeldField.GetValue(system));
    }
}

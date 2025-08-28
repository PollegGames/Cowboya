using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Reflection;

public class NewPlayerMovementControllerTests
{
    [Test]
    public void SetFacing_LegJointLimitsMirror()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var go = new GameObject();
            go.AddComponent<Rigidbody2D>();
            var limiter = go.AddComponent<LegJointLimiter>();

            var left = new GameObject("LeftLowLeg");
            left.transform.SetParent(go.transform);
            left.AddComponent<Rigidbody2D>();
            var leftJoint = left.AddComponent<HingeJoint2D>();

            var right = new GameObject("RightLowLeg");
            right.transform.SetParent(go.transform);
            right.AddComponent<Rigidbody2D>();
            var rightJoint = right.AddComponent<HingeJoint2D>();

            var motor = rightJoint.motor;
            motor.motorSpeed = 50f;
            rightJoint.motor = motor;
            var limits = rightJoint.limits;
            limits.min = 0f;
            limits.max = 180f;
            rightJoint.limits = limits;

            limiter.RefreshJoints();

            var controller = go.AddComponent<NewPlayerMovementController>();
            typeof(NewPlayerMovementController)
                .GetField("legJointLimiter", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, limiter);

            var method = typeof(NewPlayerMovementController).GetMethod("SetFacing", BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(controller, new object[] { true });
            AssertJoint(limiter.rightLegJoint, 0f, 180f, 50f);
            AssertJoint(limiter.leftLegJoint, -180f, 0f, -50f);

            method.Invoke(controller, new object[] { false });
            AssertJoint(limiter.rightLegJoint, -180f, 0f, -50f);
            AssertJoint(limiter.leftLegJoint, 0f, 180f, 50f);

            Object.DestroyImmediate(go);
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    private static void AssertJoint(HingeJoint2D joint, float expectedMin, float expectedMax, float expectedSpeed)
    {
        Assert.AreEqual(expectedMin, joint.limits.min);
        Assert.AreEqual(expectedMax, joint.limits.max);
        Assert.AreEqual(expectedSpeed, joint.motor.motorSpeed);
    }
}

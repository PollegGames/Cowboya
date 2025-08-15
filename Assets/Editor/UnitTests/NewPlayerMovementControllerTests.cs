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
            left.AddComponent<HingeJoint2D>();

            var right = new GameObject("RightLowLeg");
            right.transform.SetParent(go.transform);
            right.AddComponent<Rigidbody2D>();
            right.AddComponent<HingeJoint2D>();

            limiter.RefreshJoints();

            var controller = go.AddComponent<NewPlayerMovementController>();
            typeof(NewPlayerMovementController)
                .GetField("legJointLimiter", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, limiter);

            var method = typeof(NewPlayerMovementController).GetMethod("SetFacing", BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(controller, new object[] { true });
            AssertJointLimits(limiter, 0f, 180f);

            method.Invoke(controller, new object[] { false });
            AssertJointLimits(limiter, -180f, 0f);

            Object.DestroyImmediate(go);
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    private static void AssertJointLimits(LegJointLimiter limiter, float expectedMin, float expectedMax)
    {
        Assert.AreEqual(expectedMin, limiter.leftLegJoint.limits.min);
        Assert.AreEqual(expectedMax, limiter.leftLegJoint.limits.max);
        Assert.AreEqual(expectedMin, limiter.rightLegJoint.limits.min);
        Assert.AreEqual(expectedMax, limiter.rightLegJoint.limits.max);
    }
}

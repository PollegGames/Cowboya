using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class PlayerCrouchTests
{
    private class TestInput : MonoBehaviour, IPlayerInput
    {
        public Vector2 movement;
        public Vector2 Movement => movement;
        public Vector2 Look => Vector2.zero;
        public bool JumpPressed => false;
        public bool PrimaryAttack => false;
        public bool LeftGrabDown => false;
        public bool LeftGrabHeld => false;
        public bool LeftGrabUp => false;
        public bool RightGrabDown => false;
        public bool RightGrabHeld => false;
        public bool RightGrabUp => false;
    }

    private static FootStepper CreateFootStepper()
    {
        var go = new GameObject("FootStepper");
        var step = go.AddComponent<FootStepper>();
        step.footTarget = new GameObject("FootTarget").transform;
        step.startRight = new GameObject("StartRight").transform;
        step.startLeft = new GameObject("StartLeft").transform;
        step.crouchUpRight = new GameObject("CrouchUpRight").transform;
        step.crouchDownRight = new GameObject("CrouchDownRight").transform;
        step.crouchUpLeft = new GameObject("CrouchUpLeft").transform;
        step.crouchDownLeft = new GameObject("CrouchDownLeft").transform;
        return step;
    }

    [Test]
    public void HoldAndReleaseCrouch_TriggersStartAndEnd()
    {
        var go = new GameObject("Player");
        var input = go.AddComponent<TestInput>();
        var energy = go.AddComponent<EnergyBot>();
        var state = go.AddComponent<RobotStateController>();
        state.Stats = new RobotStats { MaxEnergy = 10f, CurrentEnergy = 10f, AttackEnergyCost = 1f };

        var locomotion = go.AddComponent<RobotLocomotionController>();
        locomotion.leftFoot = CreateFootStepper();
        locomotion.rightFoot = CreateFootStepper();

        var player = go.AddComponent<PlayerMovementController>();
        typeof(PlayerMovementController).GetField("locomotion", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, locomotion);
        typeof(PlayerMovementController).GetField("inputSource", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, input);
        typeof(PlayerMovementController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(player, null);

        int started = 0;
        int ended = 0;
        locomotion.OnCrouchStarted += () => started++;
        locomotion.OnCrouchEnded += () => ended++;

        var verticalField = typeof(PlayerMovementController).GetField("verticalInput", BindingFlags.NonPublic | BindingFlags.Instance);
        var handleCrouch = typeof(PlayerMovementController).GetMethod("HandleCrouch", BindingFlags.NonPublic | BindingFlags.Instance);
        var isCrouchingField = typeof(RobotLocomotionController).GetField("isCrouching", BindingFlags.NonPublic | BindingFlags.Instance);

        // Press and hold crouch
        verticalField.SetValue(player, -1f);
        handleCrouch.Invoke(player, null);
        Assert.IsTrue((bool)isCrouchingField.GetValue(locomotion));
        handleCrouch.Invoke(player, null);
        Assert.AreEqual(1, started);
        Assert.IsTrue((bool)isCrouchingField.GetValue(locomotion));
        Assert.AreEqual(0, ended);

        // Release crouch
        verticalField.SetValue(player, 0f);
        handleCrouch.Invoke(player, null);
        Assert.AreEqual(1, ended);
        Assert.IsFalse((bool)isCrouchingField.GetValue(locomotion));

        Object.DestroyImmediate(go);
    }
}

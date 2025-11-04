using NUnit.Framework;
using UnityEngine;

public class AttackRequestControllerTests
{
    [Test]
    public void HeldAttackConsumesEnergyOnce()
    {
        var robot = new GameObject("Robot");
        var energyBot = robot.AddComponent<EnergyBot>();
        var stateController = robot.AddComponent<RobotStateController>();
        stateController.Stats = new RobotStats
        {
            MaxEnergy = 10f,
            CurrentEnergy = 10f,
            AttackEnergyCost = 2f
        };

        robot.AddComponent<PlayerPunchAnimator>();
        var controller = robot.AddComponent<AttackRequestController>();

        var request = new AttackRequest(Vector2.zero, AttackSector.Right, 0f);

        bool firstAccepted = controller.TryHandleAttack(request);
        Assert.IsTrue(firstAccepted);
        Assert.AreEqual(8f, stateController.Stats.CurrentEnergy);

        bool secondAccepted = controller.TryHandleAttack(request);
        Assert.IsFalse(secondAccepted);
        Assert.AreEqual(8f, stateController.Stats.CurrentEnergy);

        controller.NotifyPunchCompleted();

        bool thirdAccepted = controller.TryHandleAttack(request);
        Assert.IsTrue(thirdAccepted);
        Assert.AreEqual(6f, stateController.Stats.CurrentEnergy);

        Object.DestroyImmediate(robot);
    }
}

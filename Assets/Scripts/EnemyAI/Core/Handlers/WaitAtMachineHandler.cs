using UnityEngine;

/// <summary>
/// Keeps the robot at a machine for a specified duration before completing the task.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/WaitAtMachine", fileName = "WaitAtMachineHandler")]
public class WaitAtMachineHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null)
            return;

        var waitPayload = payload as WaitAtMachinePayload;
        BaseMachine machine = waitPayload != null ? waitPayload.Machine : payload as BaseMachine;
        float waitSeconds = waitPayload != null ? waitPayload.WaitSeconds : 0f;

        if (waitSeconds <= 0f && brain.Config != null)
            waitSeconds = brain.Config.WaitAtMachineSeconds;

        brain.RunWaitAtMachineRoutine(machine, waitSeconds);
    }
}

public class WaitAtMachinePayload
{
    public BaseMachine Machine { get; }
    public float WaitSeconds { get; }

    public WaitAtMachinePayload(BaseMachine machine, float waitSeconds)
    {
        Machine = machine;
        WaitSeconds = waitSeconds;
    }
}

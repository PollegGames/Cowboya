using System;
using UnityEngine;

public readonly struct RobotMachineStateDispatchEvent
{
    public RobotMachineStateDispatchEvent(RobotBrainNew brain, object payload, bool isOn)
    {
        Brain = brain;
        Payload = payload;
        IsOn = isOn;
    }

    public RobotBrainNew Brain { get; }
    public object Payload { get; }
    public bool IsOn { get; }
}

public readonly struct RobotSecurityDispatchEvent
{
    public RobotSecurityDispatchEvent(RobotBrainNew brain, object payload)
    {
        Brain = brain;
        Payload = payload;
    }

    public RobotBrainNew Brain { get; }
    public object Payload { get; }
}

public readonly struct RobotPerceptionDispatchEvent
{
    public RobotPerceptionDispatchEvent(
        RobotBrainNew brain,
        bool playerInDetectZone,
        bool playerInAttackZone,
        Vector3 playerPosition,
        bool hasKnownPosition,
        RoomWaypoint playerWaypoint = null)
    {
        Brain = brain;
        PlayerInDetectZone = playerInDetectZone;
        PlayerInAttackZone = playerInAttackZone;
        PlayerPosition = playerPosition;
        HasKnownPosition = hasKnownPosition;
        PlayerWaypoint = playerWaypoint;
    }

    public RobotBrainNew Brain { get; }
    public bool PlayerInDetectZone { get; }
    public bool PlayerInAttackZone { get; }
    public Vector3 PlayerPosition { get; }
    public bool HasKnownPosition { get; }
    public RoomWaypoint PlayerWaypoint { get; }
}

public readonly struct RobotCompleteReactivateDispatchEvent
{
    public RobotCompleteReactivateDispatchEvent(RobotBrainNew brain, BaseMachine machine, bool reached)
    {
        Brain = brain;
        Machine = machine;
        Reached = reached;
    }

    public RobotBrainNew Brain { get; }
    public BaseMachine Machine { get; }
    public bool Reached { get; }
}

public static class RobotDomainEventBus
{
    public static event Action<RobotMachineStateDispatchEvent> OnMachineStateDispatchRequested;
    public static event Action<RobotSecurityDispatchEvent> OnSecurityDispatchRequested;
    public static event Action<RobotPerceptionDispatchEvent> OnPerceptionDispatchRequested;
    public static event Action<RobotCompleteReactivateDispatchEvent> OnCompleteReactivateDispatchRequested;

    public static void PublishMachineStateDispatch(RobotBrainNew brain, object payload, bool isOn)
    {
        if (brain == null)
            return;
        OnMachineStateDispatchRequested?.Invoke(new RobotMachineStateDispatchEvent(brain, payload, isOn));
    }

    public static void PublishSecurityDispatch(RobotBrainNew brain, object payload)
    {
        if (brain == null)
            return;
        OnSecurityDispatchRequested?.Invoke(new RobotSecurityDispatchEvent(brain, payload));
    }

    public static void PublishPerceptionDispatch(
        RobotBrainNew brain,
        bool playerInDetectZone,
        bool playerInAttackZone,
        Vector3 playerPosition,
        bool hasKnownPosition = true,
        RoomWaypoint playerWaypoint = null)
    {
        if (brain == null)
            return;

        OnPerceptionDispatchRequested?.Invoke(new RobotPerceptionDispatchEvent(
            brain,
            playerInDetectZone,
            playerInAttackZone,
            playerPosition,
            hasKnownPosition,
            playerWaypoint));
    }

    public static void PublishCompleteReactivateDispatch(RobotBrainNew brain, BaseMachine machine, bool reached)
    {
        if (brain == null || machine == null)
            return;

        OnCompleteReactivateDispatchRequested?.Invoke(
            new RobotCompleteReactivateDispatchEvent(brain, machine, reached));
    }
}

public sealed class RobotDomainEventAdapter : MonoBehaviour
{
    public static RobotDomainEventAdapter EnsureInScene()
    {
        var existing = FindAnyObjectByType<RobotDomainEventAdapter>();
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(RobotDomainEventAdapter));
        DontDestroyOnLoad(go);
        return go.AddComponent<RobotDomainEventAdapter>();
    }

    private void OnEnable()
    {
        RobotDomainEventBus.OnMachineStateDispatchRequested += HandleMachineStateDispatchRequested;
        RobotDomainEventBus.OnSecurityDispatchRequested += HandleSecurityDispatchRequested;
        RobotDomainEventBus.OnPerceptionDispatchRequested += HandlePerceptionDispatchRequested;
        RobotDomainEventBus.OnCompleteReactivateDispatchRequested += HandleCompleteReactivateDispatchRequested;
    }

    private void OnDisable()
    {
        RobotDomainEventBus.OnMachineStateDispatchRequested -= HandleMachineStateDispatchRequested;
        RobotDomainEventBus.OnSecurityDispatchRequested -= HandleSecurityDispatchRequested;
        RobotDomainEventBus.OnPerceptionDispatchRequested -= HandlePerceptionDispatchRequested;
        RobotDomainEventBus.OnCompleteReactivateDispatchRequested -= HandleCompleteReactivateDispatchRequested;
    }

    private static void HandleMachineStateDispatchRequested(RobotMachineStateDispatchEvent evt)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || evt.Brain == null)
            return;
        evt.Brain.OnMachineStateEvent(evt.Payload, evt.IsOn);
    }

    private static void HandleSecurityDispatchRequested(RobotSecurityDispatchEvent evt)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || evt.Brain == null)
            return;
        evt.Brain.OnSecurityDispatch(evt.Payload);
    }

    private static void HandlePerceptionDispatchRequested(RobotPerceptionDispatchEvent evt)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || evt.Brain == null)
            return;
        evt.Brain.OnPerceptionChanged(
            evt.PlayerInDetectZone,
            evt.PlayerInAttackZone,
            evt.PlayerPosition,
            evt.HasKnownPosition,
            playerWaypoint: evt.PlayerWaypoint);
    }

    private static void HandleCompleteReactivateDispatchRequested(RobotCompleteReactivateDispatchEvent evt)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || evt.Brain == null || evt.Machine == null)
            return;
        evt.Brain.CompleteReactivateTask(evt.Machine, evt.Reached);
    }
}

using UnityEngine;
using System.Collections.Generic;

public class LiftShaftController : MonoBehaviour
{
    [Header("Alarm System")]
    public RoomManager roomManager;

    [Header("Controlled Lifts")]
    public List<LiftController> controlledLifts = new List<LiftController>();

    // Options removed (debug-only)

    private void Start()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomAlarmChanged += OnAlarmChanged;
            LockLiftWhenWall();
        }
    }

    private void LockLiftWhenWall()
    {
        // Pull flags once (can be false in hand-authored scenes without Map assignment)
        bool hasUp = roomManager != null && roomManager.roomProperties != null && roomManager.roomProperties.HasLiftUp;
        bool hasDown = roomManager != null && roomManager.roomProperties != null && roomManager.roomProperties.HasLiftDown;

        // At room init, tell each LiftController whether it's a wall (i.e. no lift available)
        foreach (var lift in controlledLifts)
        {
            if (lift == null) continue;

            // Determine direction robustly: consider small floats/unnormalized inputs
            var dir = lift.moveDirection.sqrMagnitude > 0f ? lift.moveDirection.normalized : Vector2.zero;
            bool isUpLift = Vector2.Dot(dir, Vector2.up) > 0.5f;

            // If room doesn't have that lift, mark as wall
            bool hasLift = isUpLift ? hasUp : hasDown;

            lift.isWall = !hasLift;

            // Evaluate initial state (locks, flashing)
            lift.EvaluateLiftState();
        }
    }

    private void OnDestroy()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomAlarmChanged -= OnAlarmChanged;
        }
    }

    private void OnAlarmChanged(AlarmState state)
    {
        bool lockLifts = state == AlarmState.Lockdown;

        foreach (LiftController lift in controlledLifts)
        {
            if (lift != null)
            {
                lift.SetLocked(lockLifts);
            }
        }
    }
}

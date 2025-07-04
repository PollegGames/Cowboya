using UnityEngine;
using System.Collections.Generic;

public class LiftShaftController : MonoBehaviour
{
    [Header("Alarm System")]
    public RoomManager roomManager;

    [Header("Controlled Lifts")]
    public List<LiftController> controlledLifts = new List<LiftController>();

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
         // At room init, tell each LiftController whether it's a wall (i.e. no lift available)
        foreach (var lift in controlledLifts)
        {
            // Determine direction: up if y-positive dot with world up, else down
            bool isUpLift = Vector2.Dot(lift.moveDirection, Vector2.up) > 0;

            // If room doesn't have that lift, mark as wall
            bool hasLift = isUpLift ? roomManager.roomProperties.HasLiftUp
                                     : roomManager.roomProperties.HasLiftDown;
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
        bool lockLifts = (state == AlarmState.Wanted || state == AlarmState.Lockdown);

        foreach (LiftController lift in controlledLifts)
        {
            if (lift != null)
            {
                lift.SetLocked(lockLifts);
            }
        }
    }
}

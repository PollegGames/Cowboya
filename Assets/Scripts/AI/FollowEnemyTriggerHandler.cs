using System;
using UnityEngine;

public class FollowEnemyTriggerHandler : MonoBehaviour
{
    [Header("Zone Detection")]
    public PositionTriggerZone roomsZone;
    [SerializeField] private RobotStateController stateController;

    private void Start()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();

        if (roomsZone == null)
            roomsZone = GetComponent<PositionTriggerZone>();

        if (roomsZone != null)
        {
            roomsZone.onEnter.AddListener(OnEnemyEnterRoomZone);
        }
       
    }

    private void OnDisable()
    {
        if (roomsZone != null)
            roomsZone.onEnter.RemoveListener(OnEnemyEnterRoomZone);
    }

    private void OnEnemyEnterRoomZone(Collider2D collider)
    {
        if (stateController == null || collider == null)
            return;

        Debug.Log($"Enemy entered room zone via {collider.name}", this);

        var room = collider.GetComponentInParent<RoomManager>();
        var roomProps = collider.GetComponentInParent<RoomProperties>();
        bool enteredStart = room != null && room.roomProperties != null
            ? room.roomProperties.usageType == UsageType.Start
            : roomProps != null && roomProps.usageType == UsageType.Start;
        if (enteredStart && IsReturningHomeForRescue())
            stateController.MarkAsSaved();
    }

    private bool IsReturningHomeForRescue()
    {
        RobotHeartNew heart = stateController != null
            ? stateController.GetComponent<RobotHeartNew>()
            : null;
        return heart != null
            && FactoryManager.IsSaveEligibleRole(heart.Role)
            && heart.CurrentTask != null
            && heart.CurrentTask.Type == RobotTaskType.ReturnHome;
    }

}

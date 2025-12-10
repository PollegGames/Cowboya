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
        if (stateController == null)
        {
            return;
        }

        Debug.Log($"Enemy entered room zone via {collider.name}", this);

        var room = collider.GetComponentInParent<RoomManager>();
        Debug.Log($"Room detected: {room.roomProperties.usageType}", this);
        if (room != null && room.roomProperties != null && room.roomProperties.usageType == UsageType.Start)
        {
            stateController.MarkAsSaved();
            return;
        }

        var roomProps = collider.GetComponentInParent<RoomProperties>();
        if (roomProps != null && roomProps.usageType == UsageType.Start)
        {
            stateController.MarkAsSaved();
        }
    }

}

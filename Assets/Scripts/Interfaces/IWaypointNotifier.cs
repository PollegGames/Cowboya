using UnityEngine;

public interface IWaypointNotifier
{
    void Subscribe(IRobotNavigationListener robot);
    void Unsubscribe(IRobotNavigationListener robot);
    void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable);
}

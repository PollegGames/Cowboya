using System;
using UnityEngine;

public interface IFactoryManager
{
    event Action<AlarmState> OnFactoryAlarmChanged;

    void Initialize(MapManager mapManager, WaypointService waypointService, VictorySetup victorySetup);
    IWaypointService GetWayPointService();
    Vector3 GetStartCellWorldPosition();
    void SetPlayerInstanceHead(GameObject playerInstance, Transform head);
    void OnRobotSaved();
    void OnRobotKilled();
    GameObject playerInstance { get; }
    Transform playerHeadTransform { get; }
}

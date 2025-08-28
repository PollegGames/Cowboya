using System;
using UnityEngine;

public interface IFactoryManager
{
    event Action<AlarmState> OnFactoryAlarmChanged;

    void Initialize(MapManager mapManager, IWaypointService waypointService, VictorySetup victorySetup, IEnemiesSpawner enemiesSpawner);
    IWaypointService GetWayPointService();
    /// <summary>
    /// Gets the world position of the start room, including its horizontal offset.
    /// </summary>
    Vector3 GetStartCellWorldPosition();
    void SetPlayerInstanceHead(GameObject playerInstance, Transform head);
    void OnRobotSaved();
    void OnRobotKilled();
    GameObject playerInstance { get; }
    Transform playerHeadTransform { get; }
    MachineSecurityManager SecurityManager { get; }
}

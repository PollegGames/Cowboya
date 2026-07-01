using System;
using System.Collections.Generic;
using UnityEngine;

public interface IFactoryManager
{
    event Action<AlarmState> OnFactoryAlarmChanged;
    event Action OnFactoryAllMachinesOff;

    void Initialize(MapManager mapManager, IWaypointService waypointService, VictorySetup victorySetup, IEnemiesSpawner enemiesSpawner);
    void InitializeStatic(VictorySetup victorySetup);
    void RegisterStaticRooms(IEnumerable<RoomManager> rooms, Transform playerHead);
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

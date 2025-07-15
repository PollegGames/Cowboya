using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers <see cref="SpawningMachine"/> instances with the
/// <see cref="StationReservationService"/> so worker spawners can
/// reserve them.
/// </summary>
public class SpawningWorkerManager : MonoBehaviour
{
    [SerializeField] private StationReservationService reservationService;

    private readonly List<SpawningMachine> machines = new();

    public void RegisterMachine(SpawningMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.WorkerSpawner);
    }
}

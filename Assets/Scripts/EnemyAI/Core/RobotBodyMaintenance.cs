using UnityEngine;

/// <summary>
/// Handles physical maintenance actions such as recovering stuck robots and respawning.
/// Keeps this logic out of Memory to maintain the Heart/Brain/Body separation.
/// </summary>
[RequireComponent(typeof(RobotMemory))]
public class RobotBodyMaintenance : MonoBehaviour, IPooledObject
{
    [SerializeField] private RobotMemory memory;
    private IRobotRespawnService respawnService;

    private void Awake()
    {
        if (memory == null)
            memory = GetComponent<RobotMemory>();
    }

    public void SetRespawnService(IRobotRespawnService service)
    {
        respawnService = service;
        if (memory != null && memory.Snapshot != null)
            memory.Snapshot.SetRespawnService(service);
    }

    /// <summary>
    /// Generic stuck handler for new BodyController flow.
    /// </summary>
    /// <param name="controller">The stuck robot component.</param>
    /// <param name="isBoss">If true, respawns a boss.</param>
    public void OnStuck(MonoBehaviour controller, bool isBoss)
    {
        HandleRespawn(controller, isBoss);
    }

    private void HandleRespawn(MonoBehaviour controller, bool isBoss)
    {
        if (controller == null)
            return;

        var respawn = respawnService;
        if (respawn == null)
        {
            Debug.LogError("[RobotBodyMaintenance] Cannot respawn: service is null!");
        }
        else
        {
            if (isBoss)
                respawn.RespawnBoss();
            else
                respawn.RespawnWorker();
        }

        ObjectPool.Instance.Release(controller.gameObject);
    }

    public void OnReleaseToPool()
    {
    }

    public void OnAcquireFromPool()
    {
    }
}

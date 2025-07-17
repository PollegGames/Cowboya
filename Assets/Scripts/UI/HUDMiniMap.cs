using UnityEngine;

public class HUDMiniMap : MonoBehaviour
{
    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;

    /// <summary>
    /// Initialize the minimap with references to map and services.
    /// </summary>
    public void Setup(MapManager mapManager, IWaypointService waypointService, IRobotRespawnService respawnService)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.respawnService = respawnService;
        Debug.Log("HUDMiniMap setup complete.");
    }
}

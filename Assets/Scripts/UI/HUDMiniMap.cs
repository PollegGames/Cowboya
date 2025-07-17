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

    private MapManager mapManager;
    private IFactoryManager factoryManager;
    private IWaypointService waypointService;
    private VisualElement previewElement;

    public void Initialize(MapManager mapManager, IFactoryManager factoryManager, IWaypointService waypointService, VisualElement preview)
    {
        this.mapManager = mapManager;
        this.factoryManager = factoryManager;
        this.waypointService = waypointService;
        this.previewElement = preview;

        BuildCamera();
        SubscribeToEvents();
        RefreshMiniMap();
    }

    private void BuildCamera()
    {
        if (miniMapCameraInstance != null)
            Destroy(miniMapCameraInstance);

        var prefab = Resources.Load<GameObject>(miniMapPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"HUDMiniMap: Prefab at '{miniMapPrefabPath}' not found.");
            return;
        }

        miniMapCameraInstance = Instantiate(prefab);
        miniMapCamera = miniMapCameraInstance.GetComponent<Camera>();
        if (miniMapCamera == null)
        {
            Debug.LogError("HUDMiniMap: MiniMapCamera prefab has no Camera component.");
            return;
        }
        if (miniMapRT != null)
            miniMapCamera.targetTexture = miniMapRT;

        PositionCamera();

        if (previewElement != null && miniMapRT != null)
        {
            previewElement.style.backgroundImage = new StyleBackground(miniMapRT);
            previewElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
    }

    private void PositionCamera()
    {
        if (miniMapCamera == null || mapManager == null || RunProgressManager.Instance == null)
            return;

        var config = RunProgressManager.Instance.CurrentConfig;
        if (config == null)
            return;

        float worldWidth = config.gridWidth * mapManager.cellWidth;
        float worldHeight = config.gridHeight * mapManager.cellHeight;
        float aspectRatio = (float)miniMapRT.width / miniMapRT.height;
        float halfVertSize = worldHeight / 2f;
        float halfHorzSize = (worldWidth / 2f) / aspectRatio;
        float orthoSize = Mathf.Max(halfVertSize, halfHorzSize);
        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = orthoSize;
        miniMapCamera.transform.position = new Vector3(worldWidth / 2f, worldHeight / 2f, -10f);
    }

    private void SubscribeToEvents()
    {
        if (factoryManager != null)
            factoryManager.OnFactoryAlarmChanged += HandleAlarmChanged;

        if (waypointService != null)
        {
            var rooms = waypointService.GetAllWaypoints()
                .Where(wp => wp != null && wp.parentRoom != null)
                .Select(wp => wp.parentRoom)
                .Distinct();
            foreach (var room in rooms)
                room.OnRoomAlarmChanged += HandleAlarmChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (factoryManager != null)
            factoryManager.OnFactoryAlarmChanged -= HandleAlarmChanged;

        if (waypointService != null)
        {
            var rooms = waypointService.GetAllWaypoints()
                .Where(wp => wp != null && wp.parentRoom != null)
                .Select(wp => wp.parentRoom)
                .Distinct();
            foreach (var room in rooms)
                room.OnRoomAlarmChanged -= HandleAlarmChanged;
        }
    }

    private void HandleAlarmChanged(AlarmState state)
    {
        RefreshMiniMap();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void RefreshMiniMap()
    {
        PositionCamera();
        // Camera renders automatically to RenderTexture

    }
}

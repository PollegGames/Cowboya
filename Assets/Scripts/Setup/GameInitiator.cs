using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

public abstract class GameInitiator : MonoBehaviour
{
    [SerializeField] protected Camera _mainCameraPrefab;
    [SerializeField] protected EventSystem _mainEventSystemPrefab;
    [SerializeField] protected CinemachineCamera _cinemachinePrefab;

    protected Camera _mainCamera;
    protected EventSystem _mainEventSystem;
    protected CinemachineCamera _cinemachine;

    protected void InitializeSharedObjects()
    {
        // Instantiate or ensure the Main Camera exists
        if (_mainCamera == null)
        {
            if (_mainCameraPrefab != null)
            {
                var cameraInstance = Instantiate(_mainCameraPrefab);
                _mainCamera = cameraInstance.GetComponent<Camera>();
                if (_mainCamera != null && _mainCamera.GetComponent<CinemachineBrain>() == null)
                {
                    _mainCamera.gameObject.AddComponent<CinemachineBrain>();
                }
                Debug.Log("Main Camera initialized with Cinemachine Brain.");
            }
            else
            {
                Debug.LogWarning("GameInitiator: Main Camera prefab not assigned; skipping creation.");
            }
        }

        // Instantiate or ensure the Event System exists
        if (_mainEventSystem == null)
        {
            if (_mainEventSystemPrefab != null)
            {
                var eventSystemInstance = Instantiate(_mainEventSystemPrefab);
                _mainEventSystem = eventSystemInstance.GetComponent<EventSystem>();
                Debug.Log("Event System initialized.");
            }
            else
            {
                Debug.LogWarning("GameInitiator: Event System prefab not assigned; skipping creation.");
            }
        }

        // Instantiate the Cinemachine Virtual Camera
        if (_cinemachine == null)
        {
            if (_cinemachinePrefab != null)
            {
                var cinemachineInstance = Instantiate(_cinemachinePrefab);
                _cinemachine = cinemachineInstance.GetComponent<CinemachineCamera>();
                Debug.Log("Cinemachine Virtual Camera initialized.");
            }
            else
            {
                Debug.LogWarning("GameInitiator: Cinemachine prefab not assigned; skipping creation.");
            }
        }
    }

    [Header("Camera framing")]
    [Tooltip("Follow a room-aware framing point instead of the raw player head.")]
    [SerializeField] protected bool _useRoomFramingCamera = true;
    [Tooltip("Cinemachine follow damping used with the framing point, which is already smoothed.")]
    [SerializeField] protected float _roomFramingCinemachineDamping = 0.15f;

    private RoomFramingCameraTarget _roomFramingTarget;

    protected void SetCinemachineTarget(Transform target)
    {
        if (target != null)
        {
            PlayerViewPoint.Register(target);
        }

        if (_cinemachine != null && target != null)
        {
            Transform followed = target;
            if (_useRoomFramingCamera)
            {
                if (_roomFramingTarget == null)
                {
                    _roomFramingTarget = new GameObject("CameraFramingTarget").AddComponent<RoomFramingCameraTarget>();
                    _roomFramingTarget.transform.SetParent(transform, false);
                }

                _roomFramingTarget.Initialize(target);
                followed = _roomFramingTarget.transform;

                CinemachineFollow follow = _cinemachine.GetComponent<CinemachineFollow>();
                if (follow != null)
                {
                    Unity.Cinemachine.TargetTracking.TrackerSettings tracker = follow.TrackerSettings;
                    tracker.PositionDamping = Vector3.one * _roomFramingCinemachineDamping;
                    follow.TrackerSettings = tracker;
                }
            }

            _cinemachine.Follow = followed;
            _cinemachine.LookAt = followed;
            Debug.Log("Cinemachine target set to: " + followed.name);
        }
        else
        {
            Debug.LogWarning("Cinemachine Virtual Camera or target is not initialized.");
        }
    }

    protected abstract void InitializeSceneSpecificObjects();
}

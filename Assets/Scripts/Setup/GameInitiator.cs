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

    protected void SetCinemachineTarget(Transform target)
    {
        if (_cinemachine != null && target != null)
        {
            _cinemachine.Follow = target;
            _cinemachine.LookAt = target;
            Debug.Log("Cinemachine target set to: " + target.name);
        }
        else
        {
            Debug.LogWarning("Cinemachine Virtual Camera or target is not initialized.");
        }
    }

    protected abstract void InitializeSceneSpecificObjects();
}

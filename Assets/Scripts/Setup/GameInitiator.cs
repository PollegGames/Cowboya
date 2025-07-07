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
            var cameraInstance = Instantiate(_mainCameraPrefab);
            _mainCamera = cameraInstance.GetComponent<Camera>();
            if (_mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                _mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }
            Debug.Log("Main Camera initialized with Cinemachine Brain.");
        }

        // Instantiate or ensure the Event System exists
        if (_mainEventSystem == null)
        {
            var eventSystemInstance = Instantiate(_mainEventSystemPrefab);
            _mainEventSystem = eventSystemInstance.GetComponent<EventSystem>();
            Debug.Log("Event System initialized.");
        }

        // Instantiate the Cinemachine Virtual Camera
        if (_cinemachine == null)
        {
            var cinemachineInstance = Instantiate(_cinemachinePrefab);
            _cinemachine = cinemachineInstance.GetComponent<CinemachineCamera>();

            Debug.Log("Cinemachine Virtual Camera initialized.");
        }
    }

     protected void SetCinemachineTarget(Transform target)
    {
        if (_cinemachine != null)
        {
            _cinemachine.Follow = target;
            _cinemachine.LookAt = target;
            Debug.Log("Cinemachine target set to: " + target.name);
        }
        else
        {
            Debug.LogWarning("Cinemachine Virtual Camera is not initialized.");
        }
    }

    protected abstract void InitializeSceneSpecificObjects();
}

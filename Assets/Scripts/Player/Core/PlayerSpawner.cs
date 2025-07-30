using UnityEngine;

public class PlayerSpawner : MonoBehaviour, IPlayerSpawner
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _playerStartPosition;
    [SerializeField] private PlayerTemplate playerTemplate;

    [Header("Runtime References")]
    public GameObject playerInstance { get; private set; }
    public RobotStateController playerRobotBehaviour { get; private set; }
    public RobotStats playerRobotInfo { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head inside WholeBody

    public void SetPlayerStartPosition(Vector3 startPosition)
    {
        _playerStartPosition = startPosition;
        Debug.Log($"PlayerSpawner: Player start position set to {_playerStartPosition}");
    }

    /// <summary>
    /// Instantiates the player robot prefab, initializes its behavior and info,
    /// then finds the "Head" Transform nested under "WholeBody".
    /// </summary>
    public void InitializePlayer(ISaveService saveService)
    {
        // Instantiate the robot
        playerInstance = Instantiate(
            playerTemplate.RobotGameObjectPrefab,
            _playerStartPosition,
            Quaternion.identity
        );

        var locomotion = playerInstance.GetComponent<RobotLocomotionController>();
        if (locomotion != null)
            locomotion.isPlayerControlled = true;

        // Setup behaviour and save-data info
        playerRobotBehaviour = playerTemplate.InitializePlayerStateController(playerInstance);
        playerRobotInfo = playerTemplate.InitializePlayerStats(saveService.CurrentSaveData);

        // Locate "WholeBody" container
        Transform wholeBody = playerInstance.transform.Find("WholeBody");
        if (wholeBody == null)
        {
            Debug.LogError("Couldn't find 'WholeBody' on playerInstance. Check prefab hierarchy.");
            return;
        }

        // Locate "Head" under WholeBody
        Transform head = wholeBody.Find("Head");
        if (head == null)
        {
            Debug.LogError("Couldn't find 'Head' under 'WholeBody'. Check prefab hierarchy.");
            return;
        }

        // Store head transform
        playerHeadTransform = head;
    }
}

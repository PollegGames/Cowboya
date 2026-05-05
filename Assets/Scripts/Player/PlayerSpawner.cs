using UnityEngine;

public class PlayerSpawner : MonoBehaviour, IPlayerSpawner
{
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 _playerStartPosition;
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerRunStats runStats;

    [Header("Runtime References")]
    public GameObject playerInstance { get; private set; }
    public RobotStateController playerRobotBehaviour { get; private set; }
    public RobotStats playerRobotInfo { get; private set; }
    public Transform playerHeadTransform { get; private set; } // Head under Cowboy_Puppet/Sprites

    public void SetPlayerStartPosition(Vector3 startPosition)
    {
        _playerStartPosition = startPosition;
        Debug.Log($"PlayerSpawner: Player start position set to {_playerStartPosition}");
    }

    /// <summary>
    /// Instantiates the player robot prefab, initializes its behavior and info,
    /// then finds the "Head" Transform nested under "Cowboy_Puppet".
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
        ResolveRunStats();
        // Apply stats before gameplay so HUD and AI use updated values
        if (runStats != null && runStats.HasValues)
        {
            EnergyBot energyBot = playerInstance.GetComponent<EnergyBot>();
            Attack attack = playerRobotInfo.Attacks.Count > 0 ? playerRobotInfo.Attacks[0] : null;
            AttackHitbox[] attackHitboxes = playerInstance.GetComponentsInChildren<AttackHitbox>(true);
            runStats.Apply(playerRobotInfo, energyBot, attack, attackHitboxes);
        }
        else
        {
            Debug.Log("[PlayerSpawner] No captured run stats to apply.");
        }

        Transform resolvedHead = ResolveHeadFromMovementController();
        if (resolvedHead == null)
        {
            resolvedHead = FindHeadUsingDefaultPath();
        }

        if (resolvedHead == null)
        {
            Debug.LogError("PlayerSpawner: Couldn't resolve player head transform. Assign it in the inspector or verify the prefab hierarchy.");
            return;
        }

        playerHeadTransform = resolvedHead;
    }

    private void ResolveRunStats()
    {
        if (RunProgressManager.Instance == null || RunProgressManager.Instance.RunStats == null)
            return;

        if (runStats != null && runStats != RunProgressManager.Instance.RunStats)
        {
            Debug.LogWarning("[PlayerSpawner] Replacing serialized PlayerRunStats with RunProgressManager.RunStats for run continuity.");
        }

        runStats = RunProgressManager.Instance.RunStats;
    }

    private Transform ResolveHeadFromMovementController()
    {
        var movementController = playerInstance.GetComponent<PlayerMovementController>();
        if (movementController == null)
        {
            Debug.LogWarning("PlayerSpawner: PlayerMovementController not found on player instance.");
            return null;
        }

        if (movementController.HeadTransform == null)
        {
            Debug.LogWarning("PlayerSpawner: HeadTransform is not assigned on PlayerMovementController.");
            return null;
        }

        return movementController.HeadTransform;
    }

    private Transform FindHeadUsingDefaultPath()
    {
        Transform head = playerInstance.transform.Find("Cowboy_Puppet/Sprites/Head");
        if (head == null)
        {
            head = playerInstance.transform.Find("Cowboy_Puppet/Hips_Bone/Cowboy_Master/Sprites/Head");
        }
        return head;
    }
}

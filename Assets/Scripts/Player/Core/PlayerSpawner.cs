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
        if (runStats == null && RunProgressManager.Instance != null)
        {
            runStats = RunProgressManager.Instance.RunStats;
        }
        // Apply stats before gameplay so HUD and AI use updated values
        if (runStats != null && runStats.HasValues)
        {
            EnergyBot energyBot = playerInstance.GetComponent<EnergyBot>();
            Attack attack = playerRobotInfo.Attacks.Count > 0 ? playerRobotInfo.Attacks[0] : null;
            runStats.Apply(playerRobotInfo, energyBot, attack);
        }

        // Locate "Cowboy_Puppet" container from Cowboy_Player prefab
        Transform cowboyPuppet = playerInstance.transform.Find("Cowboy_Puppet");
        if (cowboyPuppet == null)
        {
            Debug.LogError("PlayerSpawner: Couldn't find 'Cowboy_Puppet' on playerInstance. Check Cowboy_Player prefab hierarchy.");
            return;
        }

        // Locate "Head" under the prefab's sprite hierarchy
        Transform head = playerInstance.transform.Find("Cowboy_Puppet/Sprites/Head");
        if (head == null)
        {
            head = playerInstance.transform.Find("Cowboy_Puppet/Hips_Bone/Cowboy_Master/Sprites/Head");
        }
        if (head == null)
        {
            Debug.LogError("PlayerSpawner: Couldn't find player head under Cowboy_Puppet/Sprites in Cowboy_Player prefab.");
            return;
        }
        // Store head transform
        playerHeadTransform = head;
    }
}

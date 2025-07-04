using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveSystem : MonoBehaviour
{
    [SerializeField] private PlayerTemplate runtimePlayerData; // Assign in the Inspector

    private static string saveFilePath => Path.Combine(Application.persistentDataPath, "savefileCowBoya.json");
    public SaveData CurrentSaveData { get; private set; }

    // Singleton pattern to access SaveSystem easily
    public static SaveSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        LoadGame(); // Load game data when the game starts
    }

    // Save the current save data to a file
    public void SaveGame()
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData(); // Ensure CurrentSaveData is not null before saving
        }
        RobotBehaviour robotBehaviour = runtimePlayerData.RobotGameObjectPrefab.GetComponent<RobotBehaviour>();
        CurrentSaveData.MaxHealth = robotBehaviour.RobotInfo.MaxHealth;
        CurrentSaveData.MaxEnergy = robotBehaviour.RobotInfo.MaxEnergy;
        CurrentSaveData.AttackEnergyCost = robotBehaviour.RobotInfo.AttackEnergyCost;
        // CurrentSaveData.experience = runtimePlayerData.experience;
        // Map other fields as needed

        var json = JsonUtility.ToJson(CurrentSaveData);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Game saved at " + saveFilePath);
    }

    // Load the game data from a file
    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("Game loaded from " + saveFilePath);
        }
        else
        {
            // Initialize a new save data if no file exists
            CurrentSaveData = new SaveData();
            SaveGame();
            Debug.Log("New save data created and saved.");
        }
    }

    // Reset Save Data (optional)
    public void ResetSaveData()
    {
        CurrentSaveData = new SaveData();
        SaveGame();
        Debug.Log("Save data reset.");
    }

    // Method to handle loading the base camp scene with save data
    public void LoadFactorySetup()
    {
        SceneManager.LoadScene("RunSetupScene");
    }
}

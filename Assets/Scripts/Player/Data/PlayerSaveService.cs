using System.IO;
using UnityEngine;

// When running in WebGL builds, file writes occur in memory and are synced to
// IndexedDB. The accompanying index.html enables
// config.autoSyncPersistentDataPath for automatic persistence. If saving fails
// on WebGL, consider falling back to PlayerPrefs or another storage solution.
public class PlayerSaveService : MonoBehaviour, ISaveService
{
    [SerializeField] private PlayerTemplate runtimePlayerData; // Assign in the Inspector

    private static string saveFilePath => Path.Combine(Application.persistentDataPath, "savefileCowBoya.json");
    public SaveData CurrentSaveData { get; private set; }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        LoadGame();
    }

    // Save the current save data to a file
    public void SaveGame()
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData(); // Ensure CurrentSaveData is not null before saving
        }
        RobotStateController robotBehaviour = runtimePlayerData.RobotGameObjectPrefab.GetComponent<RobotStateController>();
        // CurrentSaveData.MaxHealth = robotBehaviour.Stats.MaxHealth;
        // CurrentSaveData.MaxEnergy = robotBehaviour.Stats.MaxEnergy;
        // CurrentSaveData.AttackEnergyCost = robotBehaviour.Stats.AttackEnergyCost;
        // CurrentSaveData.experience = runtimePlayerData.experience;
        // Map other fields as needed

        var json = JsonUtility.ToJson(CurrentSaveData);
        File.WriteAllText(saveFilePath, json);
        // In WebGL builds the write happens in memory and is synced to IndexedDB.
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
            // Initialize a new save data if no file exists. On WebGL this may
            // happen when the IndexedDB storage is empty. PlayerPrefs can be
            // used as a fallback if file operations fail.
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

}

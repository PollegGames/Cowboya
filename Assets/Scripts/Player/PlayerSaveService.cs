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

    /// <summary>
    /// Save the current player stats to a file.
    /// </summary>
    /// <param name="controller">The active robot controller whose stats will be saved.</param>
    public void SaveGame(RobotStateController controller)
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData();
        }

        CurrentSaveData.MaxHealth = controller.Stats.MaxHealth;
        CurrentSaveData.MaxEnergy = controller.Stats.MaxEnergy;
        CurrentSaveData.AttackEnergyCost = controller.Stats.AttackEnergyCost;
        // CurrentSaveData.experience = controller.Stats.Experience;
        // Map other fields as needed

        WriteCurrentSaveData("Game saved");
    }

    /// <summary>
    /// Save permanent stats while keeping temporary run cube bonuses out of the save file.
    /// </summary>
    /// <param name="controller">The active robot controller whose non-run stats will be saved.</param>
    /// <param name="runStats">Captured run stats containing the permanent baseline and temporary bonuses.</param>
    public void SaveGame(RobotStateController controller, PlayerRunStats runStats)
    {
        if (runStats == null || !runStats.HasValues)
        {
            SaveGame(controller);
            return;
        }

        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData();
        }

        CurrentSaveData.MaxHealth = runStats.MaxHealth;
        CurrentSaveData.MaxEnergy = runStats.MaxEnergy;
        CurrentSaveData.AttackEnergyCost = controller.Stats.AttackEnergyCost;

        WriteCurrentSaveData($"Game saved with run baseline. Bonuses kept temporary: {runStats.DescribeBonuses()}");
    }

    // Load the game data from a file
    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            CurrentSaveData = new SaveData();
            JsonUtility.FromJsonOverwrite(json, CurrentSaveData);
            Debug.Log("Game loaded from " + saveFilePath);
        }
        else
        {
            // Initialize a new save data if no file exists. On WebGL this may
            // happen when the IndexedDB storage is empty. PlayerPrefs can be
            // used as a fallback if file operations fail.
            CurrentSaveData = new SaveData();
            var json = JsonUtility.ToJson(CurrentSaveData);
            File.WriteAllText(saveFilePath, json);
            Debug.Log("New save data created and saved.");
        }
    }

    // Reset Save Data (optional)
    public void ResetSaveData()
    {
        CurrentSaveData = new SaveData();
        var json = JsonUtility.ToJson(CurrentSaveData);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Save data reset.");
    }

    private void WriteCurrentSaveData(string message)
    {
        var json = JsonUtility.ToJson(CurrentSaveData);
        File.WriteAllText(saveFilePath, json);
        // In WebGL builds the write happens in memory and is synced to IndexedDB.
        Debug.Log(message + " at " + saveFilePath);
    }
}

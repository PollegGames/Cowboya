using System.IO;
using UnityEngine;

// When running in WebGL builds, file writes occur in memory and are synced to
// IndexedDB. The accompanying index.html enables
// config.autoSyncPersistentDataPath for automatic persistence. If saving fails
// on WebGL, consider falling back to PlayerPrefs or another storage solution.
public class PlayerSaveService : MonoBehaviour, ISaveService
{
    private const string SaveFileName = "savefileCowBoya.json";
    private const string BackupExtension = ".bak";
    private const string TemporaryExtension = ".tmp";

    [SerializeField] private PlayerTemplate runtimePlayerData; // Assign in the Inspector

    private static string saveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);
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
        if (controller == null || controller.Stats == null)
        {
            Debug.LogError("Cannot save the game because the player stats are missing.");
            return;
        }

        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData();
        }

        CurrentSaveData.MaxHealth = controller.Stats.MaxHealth;
        CurrentSaveData.CurrentHealth = controller.Stats.CurrentHealth;
        CurrentSaveData.MaxEnergy = controller.Stats.MaxEnergy;
        CurrentSaveData.CurrentEnergy = controller.Stats.CurrentEnergy;
        CurrentSaveData.EnergyRechargeRate = controller.Stats.EnergyRechargeRate;
        CurrentSaveData.AttackEnergyCost = controller.Stats.AttackEnergyCost;
        CurrentSaveData.Morality = controller.Stats.Morality;

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
        CurrentSaveData.CurrentHealth = Mathf.Clamp(runStats.CurrentHealth, 0f, runStats.MaxHealth);
        CurrentSaveData.MaxEnergy = runStats.MaxEnergy;
        CurrentSaveData.CurrentEnergy = Mathf.Clamp(runStats.CurrentEnergy, 0f, runStats.MaxEnergy);
        CurrentSaveData.EnergyRechargeRate = runStats.EnergyRechargeRate;
        CurrentSaveData.AttackEnergyCost = controller.Stats.AttackEnergyCost;
        CurrentSaveData.Morality = runStats.Morality;

        WriteCurrentSaveData($"Game saved with run baseline. Bonuses kept temporary: {runStats.DescribeBonuses()}");
    }

    /// <summary>
    /// Loads the primary save, restores its backup when needed, or creates a new save.
    /// </summary>
    public void LoadGame()
    {
        if (TryLoadFromPath(saveFilePath, out SaveData loadedData))
        {
            CurrentSaveData = loadedData;
            Debug.Log("Game loaded from " + saveFilePath);
            return;
        }

        string backupPath = saveFilePath + BackupExtension;
        if (TryLoadFromPath(backupPath, out loadedData))
        {
            CurrentSaveData = loadedData;
            WriteSaveToPath(saveFilePath, CurrentSaveData);
            Debug.LogWarning("The primary save was invalid. The backup was restored.");
            return;
        }

        CurrentSaveData = new SaveData();
        WriteSaveToPath(saveFilePath, CurrentSaveData);
        Debug.Log("New save data created and saved.");
    }

    /// <summary>
    /// Explicitly replaces all progression with a new save.
    /// </summary>
    public void ResetSaveData()
    {
        CurrentSaveData = new SaveData();
        WriteSaveToPath(saveFilePath, CurrentSaveData);
        Debug.Log("Save data reset.");
    }

    private void WriteCurrentSaveData(string message)
    {
        WriteSaveToPath(saveFilePath, CurrentSaveData);
        // In WebGL builds the write happens in memory and is synced to IndexedDB.
        Debug.Log(message + " at " + saveFilePath);
    }

    /// <summary>
    /// Writes and validates a save before replacing the current file, while retaining a backup.
    /// </summary>
    public static void WriteSaveToPath(string path, SaveData saveData)
    {
        if (saveData == null)
        {
            throw new System.ArgumentNullException(nameof(saveData));
        }

        saveData.Normalize();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + TemporaryExtension;
        string backupPath = path + BackupExtension;
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(temporaryPath, json);

        if (!TryLoadFromPath(temporaryPath, out _))
        {
            File.Delete(temporaryPath);
            throw new InvalidDataException("The serialized save could not be validated.");
        }

        if (!File.Exists(path))
        {
            File.Move(temporaryPath, path);
            return;
        }

        try
        {
            File.Replace(temporaryPath, path, backupPath);
        }
        catch (System.PlatformNotSupportedException)
        {
            ReplaceWithPortableFallback(temporaryPath, path, backupPath);
        }
        catch (IOException)
        {
            ReplaceWithPortableFallback(temporaryPath, path, backupPath);
        }
    }

    /// <summary>
    /// Tries to read and normalize a save without throwing for missing or corrupt files.
    /// </summary>
    public static bool TryLoadFromPath(string path, out SaveData saveData)
    {
        saveData = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null)
            {
                return false;
            }

            saveData.Normalize();
            return true;
        }
        catch (System.Exception exception) when (exception is IOException
            || exception is System.UnauthorizedAccessException
            || exception is System.ArgumentException)
        {
            Debug.LogWarning($"Could not read save file '{path}': {exception.Message}");
            saveData = null;
            return false;
        }
    }

    private static void ReplaceWithPortableFallback(string temporaryPath, string path, string backupPath)
    {
        File.Copy(path, backupPath, true);
        File.Copy(temporaryPath, path, true);
        File.Delete(temporaryPath);
    }
}

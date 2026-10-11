using System.IO;
using UnityEngine;

public static class RunSaveStorage
{
    private const string SaveFileName = "runSaveCowBoya.json";
    private const string BackupExtension = ".bak";
    private const string TemporaryExtension = ".tmp";

    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>
    /// Records the furthest run step the player has reached.
    /// </summary>
    public static void SaveReachedStep(int stepIndex, int levelIndex)
    {
        WriteToPath(SaveFilePath, new RunSaveData
        {
            ReachedStepIndex = stepIndex,
            ReachedLevelIndex = levelIndex
        });
    }

    /// <summary>
    /// Loads the active run checkpoint when one exists and is valid.
    /// </summary>
    public static bool TryLoad(out RunSaveData data)
    {
        return TryLoadFromPath(SaveFilePath, out data);
    }

    /// <summary>
    /// Removes the active run and its recovery files.
    /// </summary>
    public static void DeleteRun()
    {
        DeleteIfPresent(SaveFilePath);
        DeleteIfPresent(SaveFilePath + BackupExtension);
        DeleteIfPresent(SaveFilePath + TemporaryExtension);
    }

    /// <summary>
    /// Writes a run checkpoint to an explicit path for persistence and tests.
    /// </summary>
    public static void WriteToPath(string path, RunSaveData data)
    {
        if (data == null || !data.IsValid())
        {
            throw new System.ArgumentException("Run save data must contain a valid reached step.", nameof(data));
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + TemporaryExtension;
        string backupPath = path + BackupExtension;
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));

        if (!TryLoadFromPath(temporaryPath, out _))
        {
            DeleteIfPresent(temporaryPath);
            throw new InvalidDataException("The serialized run save could not be validated.");
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
    /// Loads and validates a run checkpoint from an explicit path.
    /// </summary>
    public static bool TryLoadFromPath(string path, out RunSaveData data)
    {
        data = null;
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

            data = JsonUtility.FromJson<RunSaveData>(json);
            if (data == null || !data.IsValid())
            {
                data = null;
                return false;
            }

            return true;
        }
        catch (System.Exception exception) when (exception is IOException
            || exception is System.UnauthorizedAccessException
            || exception is System.ArgumentException)
        {
            Debug.LogWarning($"Could not read run save file '{path}': {exception.Message}");
            data = null;
            return false;
        }
    }

    private static void ReplaceWithPortableFallback(string temporaryPath, string path, string backupPath)
    {
        File.Copy(path, backupPath, true);
        File.Copy(temporaryPath, path, true);
        File.Delete(temporaryPath);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

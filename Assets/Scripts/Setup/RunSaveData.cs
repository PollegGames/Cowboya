using System;

[Serializable]
public class RunSaveData
{
    public const int CurrentVersion = 1;

    public int SaveVersion = CurrentVersion;
    public int ReachedStepIndex;
    public int ReachedLevelIndex = 1;

    /// <summary>
    /// Returns whether this checkpoint contains a usable reached run step.
    /// </summary>
    public bool IsValid()
    {
        return SaveVersion == CurrentVersion
            && ReachedStepIndex >= 0
            && ReachedLevelIndex >= 1;
    }
}

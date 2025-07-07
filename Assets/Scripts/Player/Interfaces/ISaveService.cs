using UnityEngine;

public interface ISaveService
{
    SaveData CurrentSaveData { get; }
    void SaveGame();
    void LoadGame();
    void ResetSaveData();
}

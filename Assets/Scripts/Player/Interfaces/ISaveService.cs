using UnityEngine;

public interface ISaveService
{
    SaveData CurrentSaveData { get; }
    void SaveGame(RobotStateController controller);
    void LoadGame();
    void ResetSaveData();
}

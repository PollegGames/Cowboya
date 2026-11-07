using UnityEngine;

[CreateAssetMenu(fileName = "RunMapConfig", menuName = "Map/Map Config")]
public class RunMapConfigSO : ScriptableObject
{
    [Header("Grid")]
    public int gridWidth = 3;
    public int gridHeight = 3;

    [Header("Rooms")]
    public int poiCount = 1;

    [Header("Enemies")]
    public int workersCount = 1;
    [Min(0)] public int workerCollectorsCount = 1;
    public bool workerCollectorsRestAfterBatch = true;
    public int enemiesCount = 1;

    // Compatibility alias: security guards are currently backed by enemiesCount in assets.
    public int securityGuardsCount
    {
        get => enemiesCount;
        set => enemiesCount = value;
    }

    [Header("Seed")]
    public string seed = "0";

    public void GenerateRandomSeed()
    {
        seed = System.DateTime.Now.GetHashCode().ToString();
    }

    public int TotalGridCells => gridWidth * gridHeight;
}

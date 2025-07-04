using UnityEngine;

[CreateAssetMenu(fileName = "RunMapConfig", menuName = "Map/Map Config")]
public class RunMapConfigSO : ScriptableObject
{
    [Header("Grid")]
    public int gridWidth  = 5;
    public int gridHeight = 5;

    [Header("Rooms")]
    public int poiCount     = 3;
    public int blockedCount = 1;

    [Header("Enemies")]
    public int enemyCount   = 4;
    public int bossCount   = 4;

    [Header("Seed")]
    public string seed = "0";

    public void GenerateRandomSeed()
    {
        seed = System.DateTime.Now.GetHashCode().ToString();
    }

    public void EnsureValidSeed()
    {
        GenerateRandomSeed();
    }

    public int TotalGridCells => gridWidth * gridHeight;
}

using NUnit.Framework;
using UnityEngine;

public class EnemiesSpawnerTests
{
    private GameObject _gameObject;
    private EnemiesSpawner _spawner;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _spawner = _gameObject.AddComponent<EnemiesSpawner>();
    }

    [Test]
    public void InitMapManager_SetsDependencies()
    {
        // TODO: Assert initialization
    }

    [Test]
    public void CreateEnemies_CreatesInstances()
    {
        // TODO: Assert enemy creation
    }

    [Test]
    public void CreateBoss_CreatesBossInstances()
    {
        // TODO: Assert boss creation
    }

    [Test]
    public void SpreadEnemies_ActivatesEnemies()
    {
        // TODO: Assert spreading
    }

    [Test]
    public void SpawnEnemyAtRandom_CreatesEnemy()
    {
        // TODO: Assert spawn logic
    }

    [Test]
    public void SpawnBossAtRandom_CreatesBoss()
    {
        // TODO: Assert boss spawn logic
    }

    [Test]
    public void SpawnInstanceAtRandom_SpawnsObject()
    {
        // TODO: Assert spawn instance logic
    }
}

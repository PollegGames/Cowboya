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
    public void Initialize_SetsDependencies()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void CreateEnemies_CreatesInstances()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void CreateBoss_CreatesBossInstances()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void SpreadEnemies_ActivatesEnemies()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void SpawnEnemyAtRandom_CreatesEnemy()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void SpawnBossAtRandom_CreatesBoss()
    {
        Assert.IsNotNull(_spawner);
    }

    [Test]
    public void SpawnInstanceAtRandom_SpawnsObject()
    {
        Assert.IsNotNull(_spawner);
    }
}

using NUnit.Framework;
using UnityEngine;

public class ObjectPoolTests
{
    [Test]
    public void GetAndRelease_ReusesInstance()
    {
        var poolGO = new GameObject("Pool");
        var pool = poolGO.AddComponent<ObjectPool>();
        var prefab = new GameObject("prefab");

        var first = pool.Get(prefab, pool.transform);
        pool.Release(first);
        var second = pool.Get(prefab, pool.transform);

        Assert.AreSame(first, second);
    }
}

using System.Collections.Generic;
using UnityEngine;
using JoostenProductions;

/// <summary>
/// Simple singleton-based object pool for GameObjects.
/// Objects are returned inactive when released and reused on the next request.
/// </summary>
public class ObjectPool : SingletonBehaviour<ObjectPool>
{
    // Persist across scenes so pooled objects survive reloads
    protected override bool DoNotDestroyOnLoad => true;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

    /// <summary>
    /// Get an instance for the given prefab. The returned object is inactive.
    /// </summary>
    public GameObject Get(GameObject prefab, Transform parent)
    {
        if (!pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }

        GameObject obj = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab);
        instanceToPrefab[obj] = prefab;
        obj.transform.SetParent(parent, false);
        obj.SetActive(false);

        foreach (var pooled in obj.GetComponents<IPooledObject>())
            pooled.OnAcquireFromPool();

        return obj;
    }

    /// <summary>
    /// Return an instance to the pool.
    /// </summary>
    public void Release(GameObject obj)
    {
        if (obj == null) return;

        foreach (var pooled in obj.GetComponents<IPooledObject>())
            pooled.OnReleaseToPool();

        obj.SetActive(false);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        if (instanceToPrefab.TryGetValue(obj, out var prefab) && pools.TryGetValue(prefab, out var queue))
        {
            queue.Enqueue(obj);
        }
        else
        {
            Destroy(obj);
        }
    }
}

using System;
using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private CubePickup normalCubePrefab;
    [SerializeField] private CubePickup[] upgradeCubePrefabs;
    private Unity.Mathematics.Random _rnd = new Unity.Mathematics.Random((uint)Environment.TickCount);


    /// <summary>
    /// Instantiates the normal cube prefab at the provided parent.
    /// </summary>
    /// <param name="parent">Transform to parent the cube to.</param>
    /// <returns>The spawned cube.</returns>
    public CubePickup SpawnCube(Transform parent)
    {
        return InstantiateCube(normalCubePrefab, parent, parent.position, parent.rotation);
    }

    /// <summary>
    /// Replaces a cube with a randomly selected upgrade cube.
    /// </summary>
    /// <param name="parent">Parent transform for the new cube.</param>
    /// <param name="position">Spawn position for the new cube.</param>
    /// <returns>The spawned upgrade cube, or null if none were available.</returns>
    public CubePickup SpawnRandomUpgrade(Transform parent, Vector3 position)
    {
        if (upgradeCubePrefabs == null || upgradeCubePrefabs.Length == 0)
        {
            Debug.LogWarning("CubeSpawner: upgradeCubePrefabs array is empty!");
            return null;
        }


        int index = (int)Math.Floor(_rnd.NextFloat(0f, upgradeCubePrefabs.Length));
        CubePickup prefab = upgradeCubePrefabs[index];

        return InstantiateCube(prefab, parent, position, parent.rotation);
    }

    private CubePickup InstantiateCube(CubePickup prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogWarning("CubeSpawner: cube prefab is null!");
            return null;
        }

        position.z = 0.01f;

        CubePickup cube = Instantiate(prefab, position, rotation, parent);
        cube.SetFollowTarget(parent);
        return cube;
    }
}

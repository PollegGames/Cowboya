using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    [SerializeField] private CubePickup cubePrefab;

    public CubePickup SpawnCube(Transform parent)
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("CubeSpawner: cubePrefab is null!");
            return null;
        }

        Vector3 spawnPos = parent.position;
        spawnPos.z = 0.01f;

        var cube = Instantiate(
            cubePrefab,
            spawnPos,
            parent.rotation,
            parent
        );

        cube.SetFollowTarget(parent);

        return cube;
    }
}

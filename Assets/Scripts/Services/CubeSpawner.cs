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

        var cube = Instantiate(
            cubePrefab,
            parent.position,
            parent.rotation,
            parent
        );

        cube.SetFollowTarget(parent);

        return cube;
    }
}

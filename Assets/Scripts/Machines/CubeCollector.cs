using UnityEngine;

/// <summary>
/// Collects conveyor cubes and stores their upgrade.
/// Requires a trigger Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CubeCollector : MonoBehaviour
{
    [SerializeField] private CubeUpgradeSO upgradeStore;

    private void Awake()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        CubePickup pickup = other.GetComponent<CubePickup>();
        ConveyorCube cube = other.GetComponent<ConveyorCube>();

        if (pickup != null && cube != null && upgradeStore != null)
        {
            upgradeStore.Store(cube.SelectedUpgrade);
        }
    }
}


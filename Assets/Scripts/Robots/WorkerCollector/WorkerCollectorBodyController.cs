using UnityEngine;

/// <summary>
/// Marks the physical actuator boundary for a Worker Collector.
/// Collection commands are intentionally added in a later implementation stage.
/// </summary>
[DisallowMultipleComponent]
public class WorkerCollectorBodyController : MonoBehaviour
{
    [SerializeField] private Transform carryAnchor;

    public Transform CarryAnchor => carryAnchor;

    private void Reset()
    {
        carryAnchor = transform.Find("CubeCarryAnchor");
    }
}

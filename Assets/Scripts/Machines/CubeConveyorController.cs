using System;
using UnityEngine;

public class CubeConveyorController : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform midpointTrigger;
    [SerializeField] private float speed = 1f;
    [SerializeField] private CubeSpawner cubeSpawner;

    private CubePickup currentCube;
    private bool midpointActivated = false;

    public event Action OnCubeProcessed;

    /// <summary>
    /// Spawns a cube and begins moving it along the conveyor.
    /// </summary>
    public void BeginConveyor()
    {
        if (currentCube != null)
            return;

        if (cubeSpawner == null || spawnPoint == null)
        {
            Debug.LogWarning("CubeConveyorController: Missing references.");
            return;
        }

        currentCube = cubeSpawner.SpawnCube(spawnPoint);
        if (currentCube == null)
            return;

        currentCube.OnGrabbed += HandleCubeGrabbed;

        var rb = currentCube.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Kinematic;

        midpointActivated = false;
    }

    private void Update()
    {
        if (currentCube == null)
            return;

        var cubeTransform = currentCube.transform;

        // Move cube towards the exit.
        cubeTransform.position = Vector2.MoveTowards(
            cubeTransform.position,
            exitPoint.position,
            speed * Time.deltaTime);

        // Check for midpoint activation.
        if (!midpointActivated && midpointTrigger != null && cubeTransform.position.x >= midpointTrigger.position.x)
        {
            currentCube.SendMessage("Activate", SendMessageOptions.DontRequireReceiver);
            midpointActivated = true;
        }

        // Check if cube reached the exit.
        if (Vector2.Distance(cubeTransform.position, exitPoint.position) < 0.01f)
        {
            Destroy(currentCube.gameObject);
            ClearCurrentCube();
        }
    }

    /// <summary>
    /// Detaches the current cube from the conveyor, enabling physics and
    /// stopping any guided movement.
    /// </summary>
    public void DetachCube()
    {
        if (currentCube == null)
            return;

        currentCube.OnRelease(Vector2.zero);
        var rb = currentCube.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Dynamic;

        currentCube.transform.SetParent(null);
        ClearCurrentCube();
    }

    private void HandleCubeGrabbed(CubePickup cube)
    {
        if (cube != currentCube)
            return;

        var rb = cube.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Dynamic;

        ClearCurrentCube();
    }

    private void ClearCurrentCube()
    {
        if (currentCube != null)
            currentCube.OnGrabbed -= HandleCubeGrabbed;

        currentCube = null;
        midpointActivated = false;
        OnCubeProcessed?.Invoke();
    }
}

using UnityEngine;

public sealed class GarageCubeConveyorController : MonoBehaviour {
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [Min(0f)]
    [SerializeField] private float speed = 2f;
    [SerializeField] private CubePickup cubePrefab;
    [SerializeField] private Vector3 cubeLocalEulerAngles = new Vector3(90f, 0f, 0f);
    [Min(0f)]
    [SerializeField] private float spawnDelay = 1f;

    private CubePickup currentCube;

    private void OnEnable() {
        ScheduleNextCube();
    }

    private void OnDisable() {
        CancelInvoke(nameof(SpawnCube));

        if (currentCube != null) {
            currentCube.OnGrabbed -= HandleCubeGrabbed;
            Destroy(currentCube.gameObject);
            currentCube = null;
        }
    }

    private void Update() {
        if (currentCube == null || exitPoint == null)
            return;

        Vector3 cubePosition = currentCube.transform.position;
        Vector2 nextPosition = Vector2.MoveTowards(
            cubePosition,
            exitPoint.position,
            speed * Time.deltaTime);

        cubePosition.x = nextPosition.x;
        cubePosition.y = nextPosition.y;
        currentCube.transform.position = cubePosition;

        if (Vector2.Distance(currentCube.transform.position, exitPoint.position) < 0.01f)
            FinishCurrentCube(true);
    }

    private void SpawnCube() {
        if (currentCube != null)
            return;

        if (cubePrefab == null || spawnPoint == null || exitPoint == null) {
            Debug.LogWarning("GarageCubeConveyorController: Missing references.", this);
            return;
        }

        Vector3 spawnPosition = spawnPoint.position;
        spawnPosition.z = -0.01f;
        currentCube = Instantiate(cubePrefab, spawnPosition, spawnPoint.rotation, transform);
        currentCube.transform.localRotation = Quaternion.Euler(cubeLocalEulerAngles);

        currentCube.OnGrabbed += HandleCubeGrabbed;

        Rigidbody2D rigidbody = currentCube.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    private void HandleCubeGrabbed(CubePickup cube) {
        if (cube != currentCube)
            return;

        Rigidbody2D rigidbody = cube.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
            rigidbody.bodyType = RigidbodyType2D.Dynamic;

        FinishCurrentCube(false);
    }

    private void FinishCurrentCube(bool destroyCube) {
        CubePickup finishedCube = currentCube;
        currentCube = null;

        if (finishedCube != null) {
            finishedCube.OnGrabbed -= HandleCubeGrabbed;
            if (destroyCube)
                Destroy(finishedCube.gameObject);
        }

        ScheduleNextCube();
    }

    private void ScheduleNextCube() {
        if (!isActiveAndEnabled)
            return;

        CancelInvoke(nameof(SpawnCube));
        Invoke(nameof(SpawnCube), spawnDelay);
    }
}

using UnityEngine;

/// <summary>
/// Warps a subdivided plane in both horizontal (X) and vertical (Z) directions.
/// The edges remain pinned, and only the interior vertices move.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class WarpMeshXYSkew : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVerts; // Original local-space positions
    private Vector3[] workingVerts;  // Buffer for per-frame modifications
    private bool[] isEdgeVert;       // Which vertices are "edge" vs. interior

    [Header("References")]
    public Transform player;         // Assign your player transform in the Inspector

    [Header("Warp Settings")]
    public float warpFactorX = 0.1f;  // How strongly the plane skews when the player moves in X
    public float maxWarpX = 2f;       // Clamp on how far we push vertices in X

    public float warpFactorZ = 0.1f;  // How strongly the plane skews when the player moves in Z
    public float maxWarpZ = 2f;       // Clamp on how far we push vertices in Z

    private float minX, maxX, minZ, maxZ;

    [Header("Room & Player References")]
    public RoomManager roomManager;            // Assign in Inspector

    private void Awake()
    {
        // Get the precise head Transform
        var head = roomManager.PlayerHead;
        if (head != null)
        {
            player = head;
        }

    }
    private void Start()
    {
        // Retrieve the mesh from this object's MeshFilter
        mesh = GetComponent<MeshFilter>().mesh;

        // Store the original vertex positions
        originalVerts = mesh.vertices;

        // Pre-allocate our workingVerts array
        workingVerts = new Vector3[originalVerts.Length];

        // Identify the bounding box & which vertices are edges
        MarkEdgeVertices();

        DebugHighlightEdges();

        // Subscribe to room trigger events
        if (roomManager != null && roomManager.triggerZone != null)
        {
            roomManager.triggerZone.onEnter.AddListener(OnEnterRoom);
            roomManager.triggerZone.onExit.AddListener(OnExitRoom);
        }
    }

    private void OnEnterRoom(Collider2D playerCollider)
    {
        if (player == null)
        {
            var head = roomManager.FactoryManager.playerHeadTransform;
            if (head != null)
            {
                player = head;
            }
        }
    }

    private void OnExitRoom()
    {
        // // Stop warping when the head exits
        // if (player == roomManager.FactoryManager.playerHeadTransform)
        //     player = null;
    }
    void DebugHighlightEdges()
    {
        Color[] colors = new Color[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            colors[i] = isEdgeVert[i] ? Color.red : Color.white;
        }
        mesh.colors = colors;
    }

    private void Update()
    {
        if (player == null) return;

        // Get player's local position relative to the plane
        Vector3 localPlayerPos = transform.InverseTransformPoint(player.position);
        float distanceX = localPlayerPos.x;
        float distanceZ = localPlayerPos.z;

        // Calculate and clamp the offsets
        float offsetX = Mathf.Clamp(-distanceX * warpFactorX, -maxWarpX, maxWarpX);
        float offsetZ = Mathf.Clamp(-distanceZ * warpFactorZ, -maxWarpZ, maxWarpZ);

        // Apply warping to interior vertices
        for (int i = 0; i < originalVerts.Length; i++)
        {
            if (isEdgeVert[i])
            {
                // Keep edge verts pinned to their original positions
                workingVerts[i] = originalVerts[i];
            }
            else
            {
                Vector3 v = originalVerts[i];
                v.x -= offsetX;  // Apply horizontal skew (X)
                v.z -= offsetZ;  // Apply vertical skew (Z)

                workingVerts[i] = v;
            }
        }

        // Assign the updated vertices back to the mesh
        mesh.vertices = workingVerts;
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Identify edge vertices by bounding box in x, z. Also store minX, maxX, minZ, maxZ.
    /// </summary>
    private void MarkEdgeVertices()
    {
        Vector3 min = originalVerts[0];
        Vector3 max = originalVerts[0];

        for (int i = 1; i < originalVerts.Length; i++)
        {
            Vector3 v = originalVerts[i];
            if (v.x < min.x) min.x = v.x;
            if (v.x > max.x) max.x = v.x;
            if (v.z < min.z) min.z = v.z;
            if (v.z > max.z) max.z = v.z;
        }

        minX = min.x;
        maxX = max.x;
        minZ = min.z;
        maxZ = max.z;

        // Mark which vertices are "edge" if they are at min/max X or min/max Z
        isEdgeVert = new bool[originalVerts.Length];
        for (int i = 0; i < originalVerts.Length; i++)
        {
            Vector3 v = originalVerts[i];
            bool onEdge =
                Mathf.Approximately(v.x, min.x) || Mathf.Approximately(v.x, max.x) ||
                Mathf.Approximately(v.z, min.z) || Mathf.Approximately(v.z, max.z);

            isEdgeVert[i] = onEdge;
        }
    }
}

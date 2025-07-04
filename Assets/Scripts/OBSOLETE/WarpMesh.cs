using UnityEngine;

/// <summary>
/// Warp a subdivided plane in the X-Z plane (y=0).
/// The outer edges remain pinned, the interior vertices move based on the player's X position.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class WarpMesh : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVerts;  // Stores the initial positions of all vertices
    private bool[] isEdgeVert;        // Mark which vertices are on the boundary

    [Header("References")]
    public Transform player;          // Assign your player transform in the Inspector

    [Header("Warp Settings")]
    public float warpFactor = 0.1f;   // How much to move vertices per unit of player distance
    public float maxWarp = 1f;        // If you want to clamp the offset

    void Start()
    {
        // Grab the mesh from this object's MeshFilter
        // (Make sure you've done "ProBuilder → To Object" so it's a standard mesh)
        mesh = GetComponent<MeshFilter>().mesh;

        // Store the original (local) vertex positions
        originalVerts = mesh.vertices;

        // We'll track which vertices are on the boundary (edge) so we can skip moving them
        isEdgeVert = new bool[originalVerts.Length];
        MarkEdgeVertices();
    }

    void Update()
    {
        // Create a working copy each frame
        Vector3[] workingVerts = new Vector3[originalVerts.Length];

        // Calculate how far the player is in X from this plane's center
        float distanceX = player.position.x - transform.position.x;

        // Optional: clamp or scale that distance
        float offsetValue = Mathf.Clamp(distanceX * warpFactor, -maxWarp, maxWarp);

        // Apply vertex-by-vertex adjustments
        for (int i = 0; i < originalVerts.Length; i++)
        {
            // Start at the original local position
            workingVerts[i] = originalVerts[i];

            // If it's an edge vertex, skip moving it
            if (isEdgeVert[i])
                continue;

            // Example: shift them in Y by offsetValue
            workingVerts[i].y += offsetValue;
        }

        // Apply updated vertex positions
        mesh.vertices = workingVerts;
        mesh.RecalculateBounds();
        // If you're using lighting, you might want RecalculateNormals() as well,
        // but that can be more expensive.
    }

    /// <summary>
    /// Identify edge vertices by checking min/max in X and Z.
    /// (We ignore Y because in this approach Y=0 for the entire plane.)
    /// </summary>
    void MarkEdgeVertices()
    {
        // Find min and max of x,z among originalVerts
        Vector3 min = originalVerts[0];
        Vector3 max = originalVerts[0];

        // Compute overall bounding box in local space
        for (int i = 1; i < originalVerts.Length; i++)
        {
            Vector3 v = originalVerts[i];
            if (v.x < min.x) min.x = v.x;
            if (v.x > max.x) max.x = v.x;
            if (v.z < min.z) min.z = v.z;
            if (v.z > max.z) max.z = v.z;
        }

        // Mark a vertex as "edge" if x or z matches the bounding box min or max
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

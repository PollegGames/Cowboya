using UnityEngine;

/// <summary>
/// Subdivided plane in the X-Z plane with y=0.
/// The edges remain pinned, the interior vertices warp along Z 
/// so the side closest to the player moves "farther" (increasing z).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class WarpMeshZSkew : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVerts; // Original local-space positions
    private Vector3[] workingVerts;  // Buffer for per-frame modifications
    private bool[] isEdgeVert;       // Which vertices are "edge" vs. interior

    [Header("References")]
    public Transform player;         // Assign your player transform in the Inspector

    [Header("Warp Settings")]
    public float warpFactor = 0.1f;  // How strongly the plane skews when the player is left/right
    public float maxWarp = 2f;       // Clamp on how far we push vertices in X

    // We'll store min.x, max.x for the bounding box
    private float minX, maxX;

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
    }

    private void Update()
    {
        if (player == null) return;

        // 1) Determine the player's position in local space (optional).
        //    If your plane isn't rotated/moved in tricky ways,
        //    you could just do global positions:
        //    float distanceX = player.position.x - transform.position.x;
        Vector3 localPlayerPos = transform.InverseTransformPoint(player.position);
        float distanceX = localPlayerPos.x;

        // 2) Calculate and clamp the offset
        float rawOffset = -distanceX * warpFactor;
        float clampedOffset = Mathf.Clamp(rawOffset, -maxWarp, maxWarp);

        // 3) Copy vertices, but warp interior vertices only
        for (int i = 0; i < originalVerts.Length; i++)
        {
            if (isEdgeVert[i])
            {
                // Keep edge verts pinned to their original positions
                workingVerts[i] = originalVerts[i];
            }
            else
            {
                // Interior vertices get warped in X by clampedOffset
                // NOTE: Subtracting offset to invert, can also do += as desired
                Vector3 v = originalVerts[i];
                v.x -= clampedOffset;

                // (Optional) If you want a gradient warp (e.g. less warp near edges),
                // you can multiply clampedOffset by a factor based on normalized X:
                //
                // float normalizedX = Mathf.InverseLerp(minX, maxX, v.x);
                // float gradient = someCurve.Evaluate(normalizedX); // or a simple formula
                // v.x = originalVerts[i].x - (clampedOffset * gradient);

                workingVerts[i] = v;
            }
        }

        // 4) Assign the updated vertices back to the mesh
        mesh.vertices = workingVerts;
        mesh.RecalculateBounds();

        // (Optional) If you rely on lighting or shading that depends on normals:
        // mesh.RecalculateNormals();
    }

    /// <summary>
    /// Identify edge vertices by bounding box in x, z. Also store minX, maxX.
    /// We ignore Y because plane is at y=0 (assuming standard orientation).
    /// </summary>
    private void MarkEdgeVertices()
    {
        // Find min.x and max.x (and also min.z, max.z if needed)
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

        // Mark which vertices are "edge" if x == min.x or x == max.x or z == min.z or z == max.z
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
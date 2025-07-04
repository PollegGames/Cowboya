using UnityEngine;

/// <summary>
/// Example of warping a plane only when the player "touches" it.
/// We use OnTriggerEnter/Exit to toggle whether we are warping or not.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class WarpOnPlayerTouch : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVerts; 
    private Vector3[] workingVerts;  
    private bool[] isEdgeVert;       
    private bool isWarping = false;  // Whether the warp is currently active

    [Header("Warp Settings")]
    public float warpAmount = 0.2f;  // How strongly we offset interior vertices
    public float maxWarp = 2f;       // If you still want to clamp warp in some logic

    private void Start()
    {
        // Ensure our collider is a trigger, so OnTriggerEnter/Exit will fire.
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        mesh = GetComponent<MeshFilter>().mesh;
        originalVerts = mesh.vertices;
        workingVerts = new Vector3[originalVerts.Length];

        MarkEdgeVertices();
    }

    private void Update()
    {
        // If we are not warping, ensure the mesh is reset (or stays unwarped).
        if (!isWarping)
        {
            // Option A: do nothing, keep it unwarped if you never changed it
            // Option B: reset mesh to original if you want it to return to normal:
            mesh.vertices = originalVerts;
            mesh.RecalculateBounds();
            // mesh.RecalculateNormals(); // if needed
            return;
        }

        // If we ARE warping, apply your warp logic.
        WarpInteriorVertices();
    }

    private void WarpInteriorVertices()
    {
        // Example: simply push interior vertices "down" in X by warpAmount
        // or do any offset you want
        for (int i = 0; i < originalVerts.Length; i++)
        {
            if (isEdgeVert[i])
            {
                workingVerts[i] = originalVerts[i];
            }
            else
            {
                Vector3 v = originalVerts[i];
                // Sample offset
                v.x -= warpAmount;  

                // If you want to clamp so it doesn't keep pushing infinitely:
                float diff = v.x - originalVerts[i].x;
                diff = Mathf.Clamp(diff, -maxWarp, maxWarp);
                v.x = originalVerts[i].x + diff;

                workingVerts[i] = v;
            }
        }
        mesh.vertices = workingVerts;
        mesh.RecalculateBounds();
        // mesh.RecalculateNormals(); // if lighting requires updated normals
    }

    /// <summary>
    /// Detect if an object with layer "Player" enters our trigger,
    /// then we enable warp.
    /// </summary>
    /// <param name="other">Collider that entered the trigger.</param>
    private void OnTriggerEnter(Collider other)
    {
        // If you prefer "CompareTag", do if(other.CompareTag("Player")) ...
        // Or if you want a specific layer, check layer:
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            isWarping = true;
        }
    }

    /// <summary>
    /// When the player leaves the plane's trigger, stop warping.
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            isWarping = false;
        }
    }

    /// <summary>
    /// Identify edge vertices by checking bounding box in x, z.
    /// </summary>
    private void MarkEdgeVertices()
    {
        Vector3[] verts = originalVerts;
        Vector3 min = verts[0];
        Vector3 max = verts[0];

        // Find bounding box
        for (int i = 1; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            if (v.x < min.x) min.x = v.x;
            if (v.x > max.x) max.x = v.x;
            if (v.z < min.z) min.z = v.z;
            if (v.z > max.z) max.z = v.z;
        }

        // Mark edges
        isEdgeVert = new bool[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i];
            bool onEdge =
                Mathf.Approximately(v.x, min.x) || Mathf.Approximately(v.x, max.x) ||
                Mathf.Approximately(v.z, min.z) || Mathf.Approximately(v.z, max.z);

            isEdgeVert[i] = onEdge;
        }
    }
}

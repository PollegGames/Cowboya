using UnityEngine;
/// <summary>
/// Warps a mesh based on player's position in X and Z, 
/// but only moves vertices tagged with a specific vertex color (e.g., green).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class WarpMeshByVertexColor : MonoBehaviour
{private Mesh mesh;
    private Vector3[] originalVerts;
    private Vector3[] workingVerts;
    private Color[] vertexColors;

    [Header("References")]
    public Transform player;

    [Header("Warp Settings")]
    public float warpFactorX = 0.1f;
    public float maxWarpX = 2f;
    public float warpFactorZ = 0.1f;
    public float maxWarpZ = 2f;

    [Header("Vertex Color Control")]
    [ColorUsage(false)]
    public Color movableColor = new Color(1f, 0.254f, 0.212f); // FF4136

    private void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        originalVerts = mesh.vertices;
        workingVerts = new Vector3[originalVerts.Length];

        vertexColors = mesh.colors;

        if (vertexColors == null || vertexColors.Length != originalVerts.Length)
        {
            Debug.LogWarning("[WarpMeshByColor] No vertex colors found or mismatch count. Warping will be skipped.");
        }
    }

    private void Update()
    {
        if (player == null || vertexColors == null) return;

        Vector3 localPlayerPos = transform.InverseTransformPoint(player.position);
        float offsetX = Mathf.Clamp(-localPlayerPos.x * warpFactorX, -maxWarpX, maxWarpX);
        float offsetZ = Mathf.Clamp(-localPlayerPos.z * warpFactorZ, -maxWarpZ, maxWarpZ);

        for (int i = 0; i < originalVerts.Length; i++)
        {
            if (Approximately(vertexColors[i], movableColor, 0.01f))
            {
                Vector3 v = originalVerts[i];
                v.x -= offsetX;
                v.z -= offsetZ;
                workingVerts[i] = v;
            }
            else
            {
                workingVerts[i] = originalVerts[i]; // Static
            }
        }

        mesh.vertices = workingVerts;
        mesh.RecalculateBounds();
    }

    private bool Approximately(Color a, Color b, float tolerance)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}
#if UNITY_EDITOR
using UnityEngine;

[ExecuteAlways]
public class MiniMapDebugGizmos : MonoBehaviour
{
    public Bounds gridBounds;
    public Camera cam;
    public float aspect;

    void OnDrawGizmos()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;

        aspect = (cam.targetTexture != null)
            ? (float)cam.targetTexture.width / cam.targetTexture.height
            : (float)Screen.width / Screen.height;

        float halfH = cam.orthographicSize;
        float halfW = halfH * aspect;

        Gizmos.color = Color.yellow;
        var c = cam.transform.position;
        var min = new Vector3(c.x - halfW, c.y - halfH, 0);
        var max = new Vector3(c.x + halfW, c.y + halfH, 0);
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(gridBounds.center, gridBounds.size);
    }
}
#endif

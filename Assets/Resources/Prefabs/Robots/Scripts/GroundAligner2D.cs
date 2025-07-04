using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GroundAligner2D : MonoBehaviour
{
     public float rayLength = 1f;
    public LayerMask groundMask = -1;
    public Vector2 rayOffset = Vector2.zero;
    public float alignSpeed = 20f; // pour un lissage, sinon mets 999

    void Update()
    {
        Vector2 origin = (Vector2)transform.position + rayOffset;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayLength, groundMask);
        if (hit)
        {
            Vector2 groundNormal = hit.normal;
            float angle = Mathf.Atan2(groundNormal.y, groundNormal.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRot = Quaternion.Euler(0, 0, angle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * alignSpeed);
        }
    }
}

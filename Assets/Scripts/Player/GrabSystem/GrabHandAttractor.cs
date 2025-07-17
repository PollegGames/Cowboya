using UnityEngine;

public class GrabHandAttractor : MonoBehaviour
{
    public float detectionRadius = 0.5f;
    public LayerMask detectionLayer;
    public System.Action<IGrabbable> OnObjectDetected;

    public IGrabbable DetectGrabbable()
    {
        Collider2D col = Physics2D.OverlapCircle(transform.position, detectionRadius, detectionLayer);
        if (col != null)
        {
            IGrabbable grabbable = col.GetComponentInParent<IGrabbable>();
            if (grabbable != null)
            {
                OnObjectDetected?.Invoke(grabbable);
                return grabbable;
            }
        }
        return null;
    }
}

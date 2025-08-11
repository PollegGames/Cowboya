using UnityEngine;

public class GrabHandAttractor : MonoBehaviour
{
    public float detectionRadius = 0.5f;
    public LayerMask detectionLayer;
    public System.Action<IGrabbable> OnObjectDetected;

    /// <summary>
    /// Detects the closest grabbable object within range.
    /// </summary>
    public IGrabbable DetectGrabbable()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, detectionRadius, detectionLayer);
        IGrabbable closestGrabbable = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D col in cols)
        {
            IGrabbable grabbable = col.GetComponent<IGrabbable>();
            if (grabbable == null)
            {
                grabbable = col.GetComponentInParent<IGrabbable>();
            }

            if (grabbable != null)
            {
                MonoBehaviour grabbableMono = grabbable as MonoBehaviour;
                if (grabbableMono == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(transform.position, grabbableMono.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGrabbable = grabbable;
                }
            }
        }

        if (closestGrabbable != null)
        {
            OnObjectDetected?.Invoke(closestGrabbable);
            return closestGrabbable;
        }

        return null;
    }
}

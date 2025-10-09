using UnityEngine;

public class GrabHandAttractor : MonoBehaviour
{
    public float detectionRadius = 0.5f;
    public LayerMask detectionLayer;
    public System.Action<IGrabbable> OnObjectDetected;

    [SerializeField] ToggleBox toggleBox;

    /// <summary>
    /// Detects the closest grabbable object within range.
    /// </summary>
    public IGrabbable DetectGrabbable()
    {
        if (toggleBox != null && toggleBox.IsActive == false)
            return null;
        int mask = detectionLayer.value;
        if (mask == 0)
            mask = ~0;

        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
        IGrabbable closestGrabbable = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D col in cols)
        {
            if ((mask & (1 << col.gameObject.layer)) == 0)
            {
                continue;
            }

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

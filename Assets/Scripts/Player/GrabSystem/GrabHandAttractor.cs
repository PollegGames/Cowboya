using UnityEngine;

public class GrabHandAttractor : MonoBehaviour
{
    public float detectionRadius = 0.5f;
    public LayerMask detectionLayer;
    public System.Action<IGrabbable> OnObjectDetected;

    [SerializeField] private ToggleBox toggleBox;
    [SerializeField] private SpriteRenderer grabIndicator;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0f);

    private bool isActive;
    private void Awake()
    {
        if (grabIndicator == null)
            grabIndicator = GetComponent<SpriteRenderer>();
        if (grabIndicator == null)
            grabIndicator = GetComponentInChildren<SpriteRenderer>(true);

        UpdateIndicator(false);
    }

    /// <summary>
    /// Detects the closest grabbable object within range.
    /// </summary>
    public IGrabbable DetectGrabbable()
    {
        if (!IsDetectionActive())
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

    public void Activate()
    {
        isActive = true;
        UpdateIndicator(true);
    }

    public void Deactivate()
    {
        isActive = false;
        UpdateIndicator(false);
    }

    public ToggleBox GetToggleBox()
    {
        if (toggleBox == null)
        {
            toggleBox = GetComponent<ToggleBox>();
            if (toggleBox == null)
            {
                toggleBox = GetComponentInChildren<ToggleBox>(true);
            }
        }

        return toggleBox;
    }

    private bool IsDetectionActive()
    {
        return toggleBox == null || toggleBox.IsActive;
    }

    private void UpdateIndicator(bool isActive)
    {
        Color targetColor = isActive ? activeColor : inactiveColor;
        if (grabIndicator != null)
        {
            grabIndicator.color = targetColor;
        }
    }
}

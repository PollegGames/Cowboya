using UnityEngine;

/// <summary>
/// Flips all child SpriteRenderers on the X axis based on an
/// ILookDirectionProvider. Defaults to auto-collecting sprites
/// in children and auto-resolving the provider from parents.
/// </summary>
public sealed class SpriteFlip2D : MonoBehaviour
{
    [Header("Direction Source")]
    [SerializeField] private MonoBehaviour directionProviderComponent; // Optional explicit provider
    private ILookDirectionProvider directionProvider;

    [Header("Targets")]
    [SerializeField] private SpriteRenderer[] sprites; // Optional explicit targets
    [SerializeField] private bool includeInactive = true;

    [Header("Behaviour")]
    [SerializeField] private bool startFacingRight = true;
    [SerializeField, Min(0f)] private float flipThreshold = 0.1f;

    private bool flipped;

    private void Awake()
    {
        // Resolve provider
        directionProvider = directionProviderComponent as ILookDirectionProvider;
        if (directionProvider == null)
        {
            directionProvider = GetComponentInParent<ILookDirectionProvider>();
        }

        // Collect sprites if not set
        if (sprites == null || sprites.Length == 0)
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(includeInactive);
        }

        // Apply initial state
        flipped = !startFacingRight;
        ApplyFlip();
    }

    private void Update()
    {
        if (directionProvider == null) return;

        float x = directionProvider.LookDirection.x;
        if (Mathf.Abs(x) > flipThreshold)
        {
            bool shouldFlip = x < 0f;
            if (shouldFlip != flipped)
            {
                flipped = shouldFlip;
                ApplyFlip();
            }
        }
    }

    /// <summary>
    /// Sets facing direction explicitly.
    /// </summary>
    /// <param name="isRight">True to face right; false to face left.</param>
    public void SetFacing(bool isRight)
    {
        flipped = !isRight;
        ApplyFlip();
    }

    private void ApplyFlip()
    {
        if (sprites == null) return;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i]) sprites[i].flipX = flipped;
        }
    }
}

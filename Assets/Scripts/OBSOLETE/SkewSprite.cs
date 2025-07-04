using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SkewSprite : MonoBehaviour
{
    public float skewFactor = 0.1f; // Control the intensity of the skew
    public Transform player; // Drag your player object here in the Inspector
    private Material material;

    void Start()
    {
        // Get the material from the Sprite Renderer
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        material = spriteRenderer.material;

        // Ensure the material is not null
        if (material == null)
        {
            Debug.LogError("Material not found on the Sprite Renderer!");
        }
    }

    void Update()
    {
        if (player != null && material != null)
        {
            float playerOffset = player.position.x * skewFactor;
            material.SetFloat("_SkewAmount", playerOffset);

            // Debug the value being set
            Debug.Log($"SkewAmount set to: {playerOffset}");
        }
    }
}

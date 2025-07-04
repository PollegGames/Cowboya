using System.Collections.Generic;
using UnityEngine;

public class DamageFeedback : MonoBehaviour
{
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.2f;


    private List<SpriteRenderer> renderers = new();
    private Dictionary<SpriteRenderer, Color> originalColors = new();

    private void Awake()
    {
        renderers.AddRange(GetComponentsInChildren<SpriteRenderer>());
        foreach (var r in renderers)
        {
            originalColors[r] = r.color;
        }
    }

    public void Flash()
    {
        foreach (var r in renderers)
        {
            r.color = flashColor;
        }
        CancelInvoke(nameof(ResetColors));
        Invoke(nameof(ResetColors), flashDuration);
    }

    private void ResetColors()
    {
        foreach (var r in renderers)
        {
            if (r != null && originalColors.ContainsKey(r))
                r.color = originalColors[r];
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(IKController))]
public class IKArcTargetHandler : MonoBehaviour
{
     [Range(0f, 1f)]
    public float t = 0f;

    [Header("Arc points (must have 3 children)")]
    public Transform arcRoot; // Parent de 3 points
    private Transform p0, p1, p2;

    [Header("Override settings")]
    public bool applyOverride = true;
    public float force = 75f;

    private IKController ik;

    void Awake()
    {
        ik = GetComponent<IKController>();

        if (arcRoot && arcRoot.childCount >= 3)
        {
            p0 = arcRoot.GetChild(0);
            p1 = arcRoot.GetChild(1);
            p2 = arcRoot.GetChild(2);
        }
        else
        {
            Debug.LogError("arcRoot must have at least 3 children: Start, Middle, End.");
        }
    }

    void Update()
    {
        if (!applyOverride || ik == null || p0 == null) return;

        // Bezier Quadratique : B(t) = (1 - t)^2 * P0 + 2(1 - t)t * P1 + t^2 * P2
        Vector3 pos = Mathf.Pow(1 - t, 2) * p0.position +
                      2 * (1 - t) * t * p1.position +
                      Mathf.Pow(t, 2) * p2.position;

        ik.overrideAnim(pos, force);
    }
}

using UnityEngine;

[RequireComponent(typeof(IKController))]
public class IKLegStepHandler : MonoBehaviour
{
    [Range(0f, 1f)]
    public float stepProgress = 0f;

    [Header("Points de pas (Start, Peak, End)")]
    public Transform stepPointsRoot;
    private Transform p0, p1, p2;

    [Header("Référence corps")]
    public Transform bodyReference;

    [Header("Force d'attraction")]
    public float force = 75f;

    private IKController ik;

    void Awake()
    {
        ik = GetComponent<IKController>();
        if (stepPointsRoot && stepPointsRoot.childCount >= 3)
        {
            p0 = stepPointsRoot.GetChild(0);
            p1 = stepPointsRoot.GetChild(1);
            p2 = stepPointsRoot.GetChild(2);
        }
        else
        {
            Debug.LogError("stepPointsRoot needs 3 children.");
        }
    }

    void Update()
    {
        if (ik == null || p0 == null || bodyReference == null) return;

        Vector3 pos =
            Mathf.Pow(1 - stepProgress, 2) * p0.position +
            2 * (1 - stepProgress) * stepProgress * p1.position +
            Mathf.Pow(stepProgress, 2) * p2.position;

        ik.overrideAnim(pos, force);
    }
}

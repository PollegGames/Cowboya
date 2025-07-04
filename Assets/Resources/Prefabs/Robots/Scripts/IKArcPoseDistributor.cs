using UnityEngine;


[ExecuteAlways]
public class IKArcPoseDistributor : MonoBehaviour
{
    [Tooltip("0=Start, 1=Middle, 2=End — ne pas toucher en Inspector")]
    public Transform[] arcPoints = new Transform[3];

    // t privé, uniquement modifiable via SetT()
    private float t = 0f;

    /// <summary>
    /// Appelé par RobotPoseController pour piloter la cible.
    /// </summary>
    public void SetT(float value)
    {
        t = Mathf.Clamp01(value);
    }

    private void Update()
    {
        if (arcPoints == null || arcPoints.Length != 3) return;

        // Calcule la position suivant la courbe Bézier
        Vector3 p0 = arcPoints[0].position;
        Vector3 p1 = arcPoints[1].position;
        Vector3 p2 = arcPoints[2].position;
        float tt = t;
        Vector3 ab = Vector3.Lerp(p0, p1, tt);
        Vector3 bc = Vector3.Lerp(p1, p2, tt);
        transform.position = Vector3.Lerp(ab, bc, tt);
    }
}

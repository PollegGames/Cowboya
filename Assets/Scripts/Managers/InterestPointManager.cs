using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gère les points d'intérêt dans une salle selon l'état de l'ennemi (travail, repos, fuite).
/// </summary>
public class InterestPointManager : MonoBehaviour
{
    [Header("Interest Points")]
    public List<Transform> workPoints;
    public List<Transform> restPoints;
    public List<Transform> fleePoints;

    private readonly HashSet<Transform> occupiedPoints = new HashSet<Transform>();

    /// <summary>
    /// Retourne un point d'intérêt libre selon le type demandé.
    /// </summary>
    public Transform GetAvailablePoint(InterestPointType type)
    {
        List<Transform> points = GetPointsList(type);
        var availablePoints = points.Where(p => !occupiedPoints.Contains(p)).ToList();

        if (availablePoints.Count == 0)
            return null;

        Transform chosenPoint = availablePoints[Random.Range(0, availablePoints.Count)];
        occupiedPoints.Add(chosenPoint);

        return chosenPoint;
    }

    /// <summary>
    /// Libère un point d'intérêt une fois quitté.
    /// </summary>
    public void ReleasePoint(Transform point)
    {
        if (occupiedPoints.Contains(point))
            occupiedPoints.Remove(point);
    }

    private List<Transform> GetPointsList(InterestPointType type)
    {
        switch (type)
        {
            case InterestPointType.Work:
                return workPoints;
            case InterestPointType.Rest:
                return restPoints;
            case InterestPointType.Flee:
                return fleePoints;
            default:
                return new List<Transform>();
        }
    }
}

public enum InterestPointType
{
    Work,
    Rest,
    Flee
}

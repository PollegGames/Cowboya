using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages interest points in a room depending on the enemy state (work, rest, flee).
/// </summary>
public class InterestPointManager : MonoBehaviour
{
    [Header("Interest Points")]
    [SerializeField] private List<Transform> workPoints;
    [SerializeField] private List<Transform> restPoints;
    [SerializeField] private List<Transform> fleePoints;

    public IReadOnlyList<Transform> WorkPoints => workPoints;
    public IReadOnlyList<Transform> RestPoints => restPoints;
    public IReadOnlyList<Transform> FleePoints => fleePoints;

    private readonly HashSet<Transform> occupiedPoints = new HashSet<Transform>();

    /// <summary>
    /// Returns a free interest point of the requested type.
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
    /// Releases an interest point when it is left.
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

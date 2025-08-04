using System;
using UnityEngine;

/// <summary>
/// Detects players within a radius and notifies when their morality is low.
/// </summary>
public class LowMoralityPlayerTriggerHandler : MonoBehaviour
{
    [Header("Zone Detection")]
    public PositionTriggerZone detectZone;
    public float radius = 10f;

    public event Action<Transform> OnLowMoralityPlayerDetected;

    private void Start()
    {
        if (detectZone != null)
        {
            detectZone.zoneSize = Vector2.one * radius * 2f;
            detectZone.onEnter.AddListener(OnPlayerEnterDetectZone);
        }
    }

    private void OnPlayerEnterDetectZone(Collider2D collider)
    {
        if (!collider.CompareTag("Player"))
            return;

        var playerController = collider.transform.root.GetComponent<RobotStateController>();
        if (playerController != null && playerController.Stats.Morality <= -5f)
        {
            OnLowMoralityPlayerDetected?.Invoke(playerController.transform);
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ColliderEvent : UnityEvent<Collider2D> { }

public class PositionTriggerZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public Vector2 zoneSize = new Vector2(2, 2);  // Width and height of the detection zone
    public Vector2 offset = Vector2.zero;         // Optional offset from the zone’s position
    public LayerMask detectionLayer;              // Layers to detect

    [Header("Events")]
    public ColliderEvent onEnter; // Event to pass the detected collider
    public UnityEvent onExit;

    private bool hasEntered = false;

    private void Update()
    {
        Vector2 zoneCenter = (Vector2)transform.position + offset;

        // Check if any collider on the target layers is inside the zone
        Collider2D hit = Physics2D.OverlapBox(zoneCenter, zoneSize, 0f, detectionLayer);

        bool isInside = hit != null;

        if (isInside && !hasEntered)
        {
            hasEntered = true;
            OnEnterZone(hit);
        }
        else if (!isInside && hasEntered)
        {
            hasEntered = false;
            OnExitZone();
        }
    }

    protected virtual void OnEnterZone(Collider2D collider)
    {
        onEnter.Invoke(collider); // Pass the detected collider
    }

    protected virtual void OnExitZone()
    {
        onExit.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 1, 0, 0.3f); // Light yellow
        Vector3 size = new Vector3(zoneSize.x, zoneSize.y, 1f);
        Gizmos.DrawCube(transform.position + (Vector3)offset, size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.9f, 0.2f, 0.6f); // Brighter yellow
        Vector3 size = new Vector3(zoneSize.x, zoneSize.y, 1f);
        Gizmos.DrawCube(transform.position + (Vector3)offset, size);
    }
}
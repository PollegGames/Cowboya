using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CameraController that follows the player and clamps its position
/// within dynamically updated room bounds without relying on Cinemachine.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player Transform to follow.")]
    [SerializeField] private Transform player;

    // Active room bounds
    private readonly List<Bounds> activeRoomBounds = new List<Bounds>();
    private Bounds combinedBounds;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Initializes the controller with the player and starting room bounds.
    /// Call once at game start.
    /// </summary>
    public void Initialize(Transform playerTransformHead)
    {
        player = playerTransformHead;

        activeRoomBounds.Clear();
        RecalculateBounds();
    }

    /// <summary>
    /// Called when the player enters a room; expands the camera bounds.
    /// </summary>
    public void AddRoomBounds(Bounds roomBounds)
    {
        if (activeRoomBounds.Exists(b => b.min == roomBounds.min && b.max == roomBounds.max))
        {
            return;
        }

        activeRoomBounds.Add(roomBounds);
        RecalculateBounds();
    }

    /// <summary>
    /// Called when the player exits a room; shrinks the camera bounds.
    /// </summary>
    public void RemoveRoomBounds(Bounds roomBounds)
    {
        int removed = activeRoomBounds.RemoveAll(b => b.min == roomBounds.min && b.max == roomBounds.max);
        if (removed == 0)
        {
            return;
        }

        if (activeRoomBounds.Count > 0)
            RecalculateBounds();
    }

    /// <summary>
    /// Recalculates the union of all active room bounds into combinedBounds.
    /// </summary>
    private void RecalculateBounds()
    {
        if (activeRoomBounds.Count == 0)
        {
            combinedBounds = new Bounds(player.position, Vector3.zero);
            return;
        }

        Vector3 min = activeRoomBounds[0].min;
        Vector3 max = activeRoomBounds[0].max;

        for (int i = 1; i < activeRoomBounds.Count; i++)
        {
            Bounds b = activeRoomBounds[i];
            min = Vector3.Min(min, b.min);
            max = Vector3.Max(max, b.max);
        }

        Vector3 size = max - min;
        Vector3 center = (min + max) * 0.5f;
        // Apply vertical offset to the bounds' center
        center.y += verticalOffset;

        combinedBounds = new Bounds(center, size);
    }

    /// <summary>
    /// After all movement, clamp the camera position to combinedBounds.
    /// </summary>
    private Vector3 velocity = Vector3.zero; // Used for SmoothDamp
    [SerializeField] private float smoothTime = 0.2f; // Adjust smoothing duration
    [SerializeField] private float verticalOffset = -1f; // Adjust this value to move the camera downward


    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 targetPosition = player.position;
        // targetPosition.z = transform.position.z; // Preserve camera's z position

        // Basic clamp
        targetPosition.x = Mathf.Clamp(targetPosition.x, combinedBounds.min.x, combinedBounds.max.x);
        targetPosition.y = Mathf.Clamp(targetPosition.y, combinedBounds.min.y, combinedBounds.max.y);

        // Optional: account for orthographic extents
        if (cam.orthographic)
        {
            float vert = cam.orthographicSize;
            float horiz = vert * cam.aspect;
            targetPosition.x = Mathf.Clamp(targetPosition.x,
                combinedBounds.min.x + horiz,
                combinedBounds.max.x - horiz);
            targetPosition.y = Mathf.Clamp(targetPosition.y,
                combinedBounds.min.y + vert,
                combinedBounds.max.y - vert);
        }

        // Smoothly move the camera toward the target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}

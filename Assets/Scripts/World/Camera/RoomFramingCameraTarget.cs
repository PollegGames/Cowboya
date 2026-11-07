using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Invisible point followed by the gameplay camera instead of the player's head.
/// It keeps the room the player is in framed: the camera leans towards the centre of the
/// current room, glides to the next room when the player crosses a door, and ignores the
/// small vertical jolts of fights by anchoring the height on the room.
/// The player is always kept inside a safe part of the screen, whatever the room detection says.
/// </summary>
public sealed class RoomFramingCameraTarget : MonoBehaviour {
    [Tooltip("Player point to frame (usually the head).")]
    [SerializeField] private Transform player;

    [Header("Horizontal")]
    [Tooltip("0 = follow the player exactly, 1 = stay on the room centre.")]
    [SerializeField, Range(0f, 1f)] private float horizontalRoomBias = 0.4f;
    [SerializeField, Min(0f)] private float horizontalSmoothTime = 0.35f;
    [Tooltip("The player never gets further than this fraction of the half screen width from the centre.")]
    [SerializeField, Range(0.1f, 0.9f)] private float maxHorizontalPlayerOffset = 0.5f;

    [Header("Vertical")]
    [Tooltip("0 = follow the player height, 1 = stay on the room height. Outside any room the player height is used.")]
    [SerializeField, Range(0f, 1f)] private float verticalRoomBias = 0.85f;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField, Min(0f)] private float verticalSmoothTime = 0.5f;
    [Tooltip("Player height changes smaller than this are ignored (fight jolts, landing bumps).")]
    [SerializeField, Min(0f)] private float verticalDeadZone = 0.6f;
    [Tooltip("The player never gets further than this fraction of the half screen height from the centre.")]
    [SerializeField, Range(0.1f, 0.9f)] private float maxVerticalPlayerOffset = 0.5f;

    [Header("Rooms")]
    [Tooltip("Seconds between refreshes of the room list (rooms can appear after load).")]
    [SerializeField, Min(0.1f)] private float roomRefreshInterval = 1f;

    private readonly Dictionary<RoomManager, Bounds> roomBounds = new Dictionary<RoomManager, Bounds>();
    private RoomManager[] rooms = new RoomManager[0];
    private RoomManager currentRoom;
    private float nextRoomRefreshTime;
    private float trackedPlayerY;
    private float velocityX;
    private float velocityY;
    private bool initialized;

    public RoomManager CurrentRoom => currentRoom;

    /// <summary>Sets the player point to frame and snaps the target onto it.</summary>
    public void Initialize(Transform playerPoint) {
        player = playerPoint;
        initialized = false;
    }

    private void LateUpdate() {
        if (player == null) {
            return;
        }

        Vector3 playerPosition = player.position;
        if (!initialized) {
            trackedPlayerY = playerPosition.y;
            transform.position = playerPosition;
            velocityX = velocityY = 0f;
            initialized = true;
        }

        // Height dead zone: only follow the player height once it moved far enough.
        float deltaY = playerPosition.y - trackedPlayerY;
        if (Mathf.Abs(deltaY) > verticalDeadZone) {
            trackedPlayerY = playerPosition.y - Mathf.Sign(deltaY) * verticalDeadZone;
        }

        UpdateCurrentRoom(playerPosition);

        float desiredX = playerPosition.x;
        float desiredY = trackedPlayerY;
        if (currentRoom != null && roomBounds.TryGetValue(currentRoom, out Bounds bounds)) {
            desiredX = Mathf.Lerp(playerPosition.x, bounds.center.x, horizontalRoomBias);
            desiredY = Mathf.Lerp(trackedPlayerY, bounds.center.y + verticalOffset, verticalRoomBias);
        }

        // Safety: keep the player on screen even if the room is wrong or still changing.
        GetHalfViewSize(out float halfWidth, out float halfHeight);
        float maxDx = halfWidth * maxHorizontalPlayerOffset;
        float maxDy = halfHeight * maxVerticalPlayerOffset;
        desiredX = Mathf.Clamp(desiredX, playerPosition.x - maxDx, playerPosition.x + maxDx);
        desiredY = Mathf.Clamp(desiredY, playerPosition.y - maxDy, playerPosition.y + maxDy);

        Vector3 position = transform.position;
        position.x = Mathf.SmoothDamp(position.x, desiredX, ref velocityX, horizontalSmoothTime);
        position.y = Mathf.SmoothDamp(position.y, desiredY, ref velocityY, verticalSmoothTime);
        // The smoothing may lag behind a fast player: never let the player leave the screen.
        position.x = Mathf.Clamp(position.x, playerPosition.x - halfWidth * 0.9f, playerPosition.x + halfWidth * 0.9f);
        position.y = Mathf.Clamp(position.y, playerPosition.y - halfHeight * 0.9f, playerPosition.y + halfHeight * 0.9f);
        position.z = playerPosition.z;
        transform.position = position;
    }

    private static void GetHalfViewSize(out float halfWidth, out float halfHeight) {
        Camera cam = Camera.main;
        float size = cam != null && cam.orthographic ? cam.orthographicSize : 6f;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        halfHeight = size;
        halfWidth = size * aspect;
    }

    private void UpdateCurrentRoom(Vector3 playerPosition) {
        if (Time.unscaledTime >= nextRoomRefreshTime) {
            rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
            nextRoomRefreshTime = Time.unscaledTime + roomRefreshInterval;
        }

        // The room containing the player whose centre is horizontally the closest wins:
        // neighbouring rooms may overlap slightly at their doors.
        RoomManager best = null;
        float bestDistance = float.MaxValue;
        foreach (RoomManager room in rooms) {
            if (room == null || !room.isActiveAndEnabled || !TryGetRoomBounds(room, out Bounds bounds)) {
                continue;
            }

            if (playerPosition.x < bounds.min.x || playerPosition.x > bounds.max.x
                || playerPosition.y < bounds.min.y || playerPosition.y > bounds.max.y) {
                continue;
            }

            float distance = Mathf.Abs(playerPosition.x - bounds.center.x);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = room;
            }
        }

        if (best != null) {
            currentRoom = best;
        }
        // Between rooms (lift shaft): keep the last room; the on-screen clamp keeps the player visible.
    }

    /// <summary>
    /// World bounds of a room from its renderers, cached. Rooms are rotated prefabs, so their
    /// trigger zone size is not reliable in world space; renderers are.
    /// </summary>
    private bool TryGetRoomBounds(RoomManager room, out Bounds bounds) {
        if (roomBounds.TryGetValue(room, out bounds)) {
            return bounds.size.x > 0f;
        }

        bool hasBounds = false;
        foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>()) {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) {
                continue;
            }

            if (!hasBounds) {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds) {
            bounds = new Bounds(room.transform.position, Vector3.zero);
        }

        roomBounds[room] = bounds;
        return bounds.size.x > 0f;
    }
}

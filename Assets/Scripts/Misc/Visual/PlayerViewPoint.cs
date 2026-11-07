using UnityEngine;

/// <summary>
/// One smoothed player position shared by every effect that reacts to where the player is
/// (room perspective warp, objects moving with the player). Using the same smoothed point keeps
/// warped rooms and the objects drawn on them in sync, and makes fast moves (lifts) glide.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayerViewPoint : MonoBehaviour {
    [Tooltip("Smoothing time of the shared player point. 0 = raw player position.")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    private static PlayerViewPoint instance;

    private Transform player;
    private Vector3 smoothed;
    private Vector3 velocity;
    private bool hasValue;

    /// <summary>
    /// Registers the player point (usually the head). Creates the shared instance on first use.
    /// </summary>
    public static void Register(Transform playerPoint) {
        if (instance == null) {
            instance = new GameObject(nameof(PlayerViewPoint)).AddComponent<PlayerViewPoint>();
        }

        instance.player = playerPoint;
        instance.hasValue = false;
    }

    /// <summary>True when <paramref name="playerPoint"/> is the registered player and a smoothed value exists.</summary>
    public static bool IsTracking(Transform playerPoint) {
        return instance != null && instance.hasValue && playerPoint != null && instance.player == playerPoint;
    }

    /// <summary>
    /// Returns the smoothed position when <paramref name="playerPoint"/> is the registered player,
    /// otherwise the raw position of <paramref name="playerPoint"/>.
    /// </summary>
    public static Vector3 GetPosition(Transform playerPoint) {
        if (instance != null && instance.hasValue && instance.player == playerPoint && playerPoint != null) {
            return instance.smoothed;
        }

        return playerPoint != null ? playerPoint.position : Vector3.zero;
    }

    private void Update() {
        if (player == null) {
            hasValue = false;
            return;
        }

        if (!hasValue || smoothTime <= 0f) {
            smoothed = player.position;
            velocity = Vector3.zero;
            hasValue = true;
            return;
        }

        smoothed = Vector3.SmoothDamp(smoothed, player.position, ref velocity, smoothTime);
    }

    private void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes a Rigidbody2D "ride" moving platforms without transform parenting.
/// Detects when grounded on an IMovingPlatform2D and applies the platform's delta movement.
/// Can collect contacts from itself or from child sensors (PlatformRiderSensor2D).
/// </summary>
[DisallowMultipleComponent]
public class PlatformRider2D : MonoBehaviour
{
    [Header("Rigidbody Target")]
    [Tooltip("Rigidbody2D to move with platforms. Usually the Hips/root body.")]
    [SerializeField] private Rigidbody2D targetRb;

    [Header("Ground Detection")]
    [Tooltip("Minimum upward normal to consider a contact as support (0.5 ~ 60° slope).")]
    [Range(0f, 1f)]
    [SerializeField] private float minSupportNormalY = 0.5f;

    [Tooltip("Layers that can act as moving platforms. Empty = any layer.")]
    [SerializeField] private LayerMask platformLayers = ~0;

    // Sensors optionally forward their collision events to the rider.
    [SerializeField] private PlatformRiderSensor2D[] sensors;

    // Track best support score per collider currently in contact.
    private readonly Dictionary<Collider2D, float> supportScores = new Dictionary<Collider2D, float>(8);
    private IMovingPlatform2D currentPlatform;

    private void Awake()
    {
        if (targetRb == null)
            targetRb = GetComponent<Rigidbody2D>();

        if (sensors == null || sensors.Length == 0)
            sensors = GetComponentsInChildren<PlatformRiderSensor2D>(true);

        for (int i = 0; i < sensors.Length; i++)
        {
            if (sensors[i] != null)
                sensors[i].Bind(this);
        }

        // If we have a collider on this object, it can act as an implicit sensor.
        if (GetComponent<Collider2D>() == null && (sensors == null || sensors.Length == 0))
        {
            Debug.LogWarning($"{nameof(PlatformRider2D)} on {name} has no Collider2D and no sensors. It will not detect platforms.");
        }
    }

    private void FixedUpdate()
    {
        // Refresh which platform (if any) we're currently riding based on contacts.
        UpdateCurrentPlatformFromContacts();

        if (currentPlatform == null || targetRb == null)
            return;

        Vector2 delta = currentPlatform.DeltaPosition;
        if (delta.sqrMagnitude > 0f)
        {
            // Move the rigidbody by the platform's delta to prevent sliding.
            targetRb.MovePosition(targetRb.position + delta);
        }
    }

    private void UpdateCurrentPlatformFromContacts()
    {
        IMovingPlatform2D bestPlatform = null;
        float bestScore = 0f;

        // Iterate contacts and select the most supportive platform beneath us.
        var toRemove = new List<Collider2D>();
        foreach (var kvp in supportScores)
        {
            Collider2D col = kvp.Key;
            if (!col) { toRemove.Add(col); continue; }
            if (((1 << col.gameObject.layer) & platformLayers) == 0) continue;

            IMovingPlatform2D platform = col.GetComponentInParent<IMovingPlatform2D>();
            if (platform == null) continue;

            float score = kvp.Value;
            if (score >= minSupportNormalY && score > bestScore)
            {
                bestScore = score;
                bestPlatform = platform;
            }
        }
        for (int i = 0; i < toRemove.Count; i++) supportScores.Remove(toRemove[i]);

        currentPlatform = bestPlatform;
    }

    // These methods are called by sensors and by local collision callbacks.
    internal void SensorCollisionEnter(Collision2D collision)
    {
        if (collision.collider.isTrigger) return;
        float best = BestUpNormal(collision);
        supportScores[collision.collider] = Mathf.Max(best, supportScores.TryGetValue(collision.collider, out float v) ? v : 0f);
    }

    internal void SensorCollisionStay(Collision2D collision)
    {
        if (collision.collider.isTrigger) return;
        float best = BestUpNormal(collision);
        supportScores[collision.collider] = best;
    }

    internal void SensorCollisionExit(Collision2D collision)
    {
        supportScores.Remove(collision.collider);
        if (currentPlatform != null)
        {
            IMovingPlatform2D exited = collision.collider ? collision.collider.GetComponentInParent<IMovingPlatform2D>() : null;
            if (exited == currentPlatform)
                currentPlatform = null;
        }
    }

    private float BestUpNormal(Collision2D collision)
    {
        float best = 0f;
        var contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            float ny = contacts[i].normal.y;
            if (ny > best) best = ny;
        }
        return best;
    }

    // Local collider can also act as a sensor if present.
    private void OnCollisionEnter2D(Collision2D collision) => SensorCollisionEnter(collision);
    private void OnCollisionStay2D(Collision2D collision) => SensorCollisionStay(collision);
    private void OnCollisionExit2D(Collision2D collision) => SensorCollisionExit(collision);
}

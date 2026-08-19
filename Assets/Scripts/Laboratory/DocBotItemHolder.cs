using System;
using UnityEngine;

/// <summary>
/// Owns the physical Junk and white-cube representations carried by DocBot.
/// </summary>
public sealed class DocBotItemHolder : MonoBehaviour {
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private Transform rightHandAnchor;

    private JunkPickup heldJunk;
    private DocBotHand? heldJunkHand;
    private CubePickup presentedWhiteCube;
    private DocBotHand? whiteCubeHand;

    public event Action<CubePickup> OnWhiteCubeTaken;

    public Transform LeftHandAnchor => leftHandAnchor;
    public Transform RightHandAnchor => rightHandAnchor;
    public JunkPickup HeldJunk => heldJunk;
    public CubePickup PresentedWhiteCube => presentedWhiteCube;
    public bool HasHeldJunk => heldJunk != null;
    public bool HasPresentedWhiteCube => presentedWhiteCube != null;

    /// <summary>
    /// Assigns the two physical hand anchors authored on the puppet rig.
    /// </summary>
    public void Configure(Transform leftAnchor, Transform rightAnchor) {
        leftHandAnchor = leftAnchor;
        rightHandAnchor = rightAnchor;
    }

    /// <summary>
    /// Returns whether the requested hand currently owns no laboratory item.
    /// </summary>
    public bool IsHandFree(DocBotHand hand) {
        return heldJunkHand != hand && whiteCubeHand != hand;
    }

    /// <summary>
    /// Finds the free hand closest to a world-space source position.
    /// </summary>
    public bool TryGetClosestFreeHand(Vector3 sourcePosition, out DocBotHand hand) {
        bool leftAvailable = leftHandAnchor != null && IsHandFree(DocBotHand.Left);
        bool rightAvailable = rightHandAnchor != null && IsHandFree(DocBotHand.Right);

        if (!leftAvailable && !rightAvailable) {
            hand = default;
            return false;
        }

        if (!leftAvailable) {
            hand = DocBotHand.Right;
            return true;
        }

        if (!rightAvailable) {
            hand = DocBotHand.Left;
            return true;
        }

        float leftDistance = (leftHandAnchor.position - sourcePosition).sqrMagnitude;
        float rightDistance = (rightHandAnchor.position - sourcePosition).sqrMagnitude;
        hand = leftDistance <= rightDistance ? DocBotHand.Left : DocBotHand.Right;
        return true;
    }

    /// <summary>
    /// Attaches Junk already locked by this holder after the player relinquishes it.
    /// </summary>
    public bool TryAttachLockedJunk(JunkPickup junk, DocBotHand hand) {
        if (junk == null
            || junk.GrabLockOwner != this
            || heldJunk != null
            || !IsHandFree(hand)) {
            return false;
        }

        Transform anchor = GetAnchor(hand);
        if (anchor == null) {
            return false;
        }

        EnsureCollectorCargoExclusion(junk.gameObject);
        junk.OnGrab(anchor);
        if (junk.transform.parent != anchor) {
            return false;
        }

        heldJunk = junk;
        heldJunkHand = hand;
        return true;
    }

    /// <summary>
    /// Creates one white reward cube in an available hand.
    /// </summary>
    public bool TryPresentWhiteCube(CubePickup whiteCubePrefab) {
        if (whiteCubePrefab == null || presentedWhiteCube != null) {
            return false;
        }

        Vector3 preferredPosition = rightHandAnchor != null
            ? rightHandAnchor.position
            : transform.position;
        if (!TryGetClosestFreeHand(preferredPosition, out DocBotHand hand)) {
            return false;
        }

        Transform anchor = GetAnchor(hand);
        CubePickup instance = Instantiate(
            whiteCubePrefab,
            anchor.position,
            Quaternion.identity);
        instance.name = "DocBot_WhiteCube";
        EnsureCollectorCargoExclusion(instance.gameObject);

        // Subscribe only after DocBot's own attachment so it is not mistaken for
        // the player's first grab.
        instance.OnGrab(anchor);
        instance.OnGrabbed += HandleWhiteCubeGrabbed;
        presentedWhiteCube = instance;
        whiteCubeHand = hand;
        return true;
    }

    /// <summary>
    /// Drops every carried representation before corpse collection is evaluated.
    /// </summary>
    public void ReleaseItemsForDeath() {
        ReserveHeldJunkForStorage();

        if (presentedWhiteCube != null && whiteCubeHand.HasValue) {
            presentedWhiteCube.OnRelease(Vector2.zero);
            whiteCubeHand = null;
        }
    }

    /// <summary>
    /// Unlocks and drops only the accepted Junk representation.
    /// </summary>
    public void ReleaseHeldJunk() {
        if (heldJunk != null) {
            JunkPickup junk = heldJunk;
            heldJunk = null;
            heldJunkHand = null;
            junk.UnlockGrab(this);
            junk.OnRelease(Vector2.zero);
        }
    }

    private void ReserveHeldJunkForStorage() {
        if (heldJunk == null) {
            return;
        }

        JunkPickup junk = heldJunk;
        heldJunk = null;
        heldJunkHand = null;

        LaboratoryStoredJunkRepresentation storage =
            junk.GetComponent<LaboratoryStoredJunkRepresentation>();
        if (storage == null) {
            storage = junk.gameObject.AddComponent<LaboratoryStoredJunkRepresentation>();
        }

        if (!storage.TryReserve(junk, this)) {
            Debug.LogError("DocBot could not transfer accepted Junk to laboratory storage.", junk);
            junk.UnlockGrab(this);
        }

        junk.OnRelease(Vector2.zero);
    }

    private Transform GetAnchor(DocBotHand hand) {
        return hand == DocBotHand.Left ? leftHandAnchor : rightHandAnchor;
    }

    private void HandleWhiteCubeGrabbed(CubePickup cube) {
        if (cube == null || cube != presentedWhiteCube) {
            return;
        }

        cube.OnGrabbed -= HandleWhiteCubeGrabbed;
        presentedWhiteCube = null;
        whiteCubeHand = null;
        OnWhiteCubeTaken?.Invoke(cube);
    }

    private void OnDestroy() {
        if (presentedWhiteCube != null) {
            presentedWhiteCube.OnGrabbed -= HandleWhiteCubeGrabbed;
        }
    }

    private static void EnsureCollectorCargoExclusion(GameObject item) {
        if (item == null) {
            return;
        }

        CollectorCargoExclusion exclusion = item.GetComponent<CollectorCargoExclusion>();
        if (exclusion == null) {
            exclusion = item.AddComponent<CollectorCargoExclusion>();
        }
        exclusion.Configure(true);
    }
}

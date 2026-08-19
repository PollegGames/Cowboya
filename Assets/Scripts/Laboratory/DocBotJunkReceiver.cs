using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects player-held Junk near DocBot, maintains a continuous acceptance delay,
/// and performs a targeted atomic hand-off to the closest free scientist hand.
/// </summary>
[DisallowMultipleComponent]
public sealed class DocBotJunkReceiver : MonoBehaviour {
    [SerializeField] private DocBotController docBot;
    [SerializeField] private DocBotItemHolder itemHolder;
    [SerializeField] private DocBotHandReachController handReach;
    [SerializeField, Min(0.01f)] private float acceptanceDelay = 1f;

    private readonly List<JunkPickup> nearbyJunk = new List<JunkPickup>();
    private readonly Dictionary<Collider2D, JunkPickup> nearbyColliders =
        new Dictionary<Collider2D, JunkPickup>();
    private readonly List<Collider2D> staleColliders = new List<Collider2D>();
    private Collider2D receiverCollider;
    private JunkPickup activeCandidate;
    private CowboyGrabController activePlayerGrab;
    private DocBotHand activeHand;
    private float candidateElapsed;

    public float AcceptanceDelay => acceptanceDelay;
    public float CandidateElapsed => candidateElapsed;
    public JunkPickup ActiveCandidate => activeCandidate;
    public DocBotHand? ActiveCandidateHand => activeCandidate != null
        ? activeHand
        : null;

    /// <summary>
    /// Assigns the exchange components authored on the final prefab.
    /// </summary>
    public void Configure(
        DocBotController controller,
        DocBotItemHolder holder,
        DocBotHandReachController reach) {
        docBot = controller;
        itemHolder = holder;
        handReach = reach;
    }

    private void Awake() {
        if (docBot == null) {
            docBot = GetComponentInParent<DocBotController>();
        }

        if (itemHolder == null && docBot != null) {
            itemHolder = docBot.ItemHolder;
        }

        if (handReach == null && docBot != null) {
            handReach = docBot.GetComponent<DocBotHandReachController>();
        }

        if (receiverCollider == null) {
            receiverCollider = GetComponent<Collider2D>();
        }
    }

    private void OnDisable() {
        ResetCandidate();
        nearbyJunk.Clear();
        nearbyColliders.Clear();
        staleColliders.Clear();
    }

    private void Update() {
        AdvanceAcceptance(Time.deltaTime);
    }

    /// <summary>
    /// Advances the deterministic acceptance timer. Runtime calls this once per
    /// frame; tests and future orchestration may supply an explicit delta.
    /// </summary>
    public void AdvanceAcceptance(float deltaTime) {
        PruneColliderTracking();

        if (docBot == null || itemHolder == null || !docBot.CanAcceptJunk) {
            ResetCandidate();
            return;
        }

        if (activeCandidate != null) {
            if (!IsActiveCandidateValid()) {
                ResetCandidate();
            }
            else {
                candidateElapsed += Mathf.Max(0f, deltaTime);
                if (candidateElapsed >= acceptanceDelay) {
                    TryCompleteTransfer(activeCandidate, activePlayerGrab, activeHand);
                }

                return;
            }
        }

        if (!TrySelectCandidate(
                out JunkPickup candidate,
                out CowboyGrabController playerGrab,
                out DocBotHand hand)) {
            return;
        }

        activeCandidate = candidate;
        activePlayerGrab = playerGrab;
        activeHand = hand;
        candidateElapsed = 0f;
        handReach?.BeginReach(hand, candidate.transform);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        Track(other);
    }

    private void OnTriggerStay2D(Collider2D other) {
        Track(other);
    }

    private void OnTriggerExit2D(Collider2D other) {
        if (other == null) {
            return;
        }

        JunkPickup junk;
        if (!nearbyColliders.TryGetValue(other, out junk)) {
            junk = other.GetComponentInParent<JunkPickup>();
        }

        nearbyColliders.Remove(other);
        if (junk == null) {
            return;
        }

        if (HasTrackedCollider(junk)) {
            return;
        }

        nearbyJunk.Remove(junk);
        if (junk == activeCandidate) {
            ResetCandidate();
        }
    }

    private void PruneColliderTracking() {
        staleColliders.Clear();
        foreach (KeyValuePair<Collider2D, JunkPickup> tracked in nearbyColliders) {
            Collider2D trackedCollider = tracked.Key;
            if (trackedCollider == null
                || !trackedCollider.enabled
                || !trackedCollider.gameObject.activeInHierarchy
                || tracked.Value == null
                || !IsOverlappingReceiver(trackedCollider)) {
                staleColliders.Add(trackedCollider);
            }
        }

        for (int i = 0; i < staleColliders.Count; i++) {
            nearbyColliders.Remove(staleColliders[i]);
        }

        for (int i = nearbyJunk.Count - 1; i >= 0; i--) {
            JunkPickup junk = nearbyJunk[i];
            if (junk == null || !HasTrackedCollider(junk)) {
                nearbyJunk.RemoveAt(i);
            }
        }
    }

    private bool IsOverlappingReceiver(Collider2D trackedCollider) {
        if (receiverCollider == null) {
            receiverCollider = GetComponent<Collider2D>();
        }

        if (receiverCollider == null
            || !receiverCollider.enabled
            || !receiverCollider.gameObject.activeInHierarchy) {
            return false;
        }

        return Physics2D.Distance(receiverCollider, trackedCollider).isOverlapped;
    }

    private bool HasTrackedCollider(JunkPickup junk) {
        foreach (KeyValuePair<Collider2D, JunkPickup> tracked in nearbyColliders) {
            if (tracked.Key != null && tracked.Value == junk) {
                return true;
            }
        }

        return false;
    }

    private void Track(Collider2D other) {
        JunkPickup junk = other != null ? other.GetComponentInParent<JunkPickup>() : null;
        if (junk == null) {
            return;
        }

        nearbyColliders[other] = junk;
        if (!nearbyJunk.Contains(junk)) {
            nearbyJunk.Add(junk);
        }
    }

    private bool IsActiveCandidateValid() {
        if (activeCandidate == null
            || activePlayerGrab == null
            || !nearbyJunk.Contains(activeCandidate)
            || !itemHolder.IsHandFree(activeHand)) {
            return false;
        }

        if (!TryGetHoldingPlayer(
                activeCandidate,
                out CowboyGrabController currentPlayer)
            || currentPlayer != activePlayerGrab) {
            return false;
        }

        Transform anchor = activeHand == DocBotHand.Left
            ? itemHolder.LeftHandAnchor
            : itemHolder.RightHandAnchor;
        return anchor != null;
    }

    private bool TrySelectCandidate(
        out JunkPickup selectedJunk,
        out CowboyGrabController selectedPlayer,
        out DocBotHand selectedHand) {
        selectedJunk = null;
        selectedPlayer = null;
        selectedHand = default;
        float bestDistance = float.MaxValue;

        for (int i = nearbyJunk.Count - 1; i >= 0; i--) {
            JunkPickup junk = nearbyJunk[i];
            if (junk == null) {
                nearbyJunk.RemoveAt(i);
                continue;
            }

            if (!TryGetHoldingPlayer(junk, out CowboyGrabController playerGrab)
                || !itemHolder.TryGetClosestFreeHand(junk.transform.position, out DocBotHand hand)) {
                continue;
            }

            Transform anchor = hand == DocBotHand.Left
                ? itemHolder.LeftHandAnchor
                : itemHolder.RightHandAnchor;
            if (anchor == null) {
                continue;
            }

            float distance = (anchor.position - junk.transform.position).sqrMagnitude;
            if (distance >= bestDistance) {
                continue;
            }

            bestDistance = distance;
            selectedJunk = junk;
            selectedPlayer = playerGrab;
            selectedHand = hand;
        }

        return selectedJunk != null && selectedPlayer != null;
    }

    private void TryCompleteTransfer(
        JunkPickup junk,
        CowboyGrabController playerGrab,
        DocBotHand hand) {
        if (junk == null
            || playerGrab == null
            || !docBot.CanAcceptJunk
            || junk != activeCandidate
            || playerGrab != activePlayerGrab
            || hand != activeHand
            || !IsActiveCandidateValid()) {
            ResetCandidate();
            return;
        }

        if (!junk.TryLockGrab(itemHolder)) {
            ResetCandidate();
            return;
        }

        if (!playerGrab.TryDetachHeldObject(junk)) {
            junk.UnlockGrab(itemHolder);
            ResetCandidate();
            return;
        }

        if (!itemHolder.TryAttachLockedJunk(junk, hand)) {
            junk.UnlockGrab(itemHolder);
            junk.OnRelease(Vector2.zero);
            ResetCandidate();
            return;
        }

        if (!docBot.TryCommitAcceptedJunk(junk.Variant)) {
            itemHolder.ReleaseHeldJunk();
            ResetCandidate();
            return;
        }

        nearbyJunk.Remove(junk);
        ResetCandidate();
        enabled = false;
    }

    private static bool TryGetHoldingPlayer(
        JunkPickup junk,
        out CowboyGrabController playerGrab) {
        playerGrab = junk.CurrentHolder != null
            ? junk.CurrentHolder.GetComponentInParent<CowboyGrabController>()
            : null;
        if (playerGrab == null) {
            return false;
        }

        return ReferenceEquals(playerGrab.GetHeldObject(CowboyArmSide.Left), junk)
            || ReferenceEquals(playerGrab.GetHeldObject(CowboyArmSide.Right), junk);
    }

    private void ResetCandidate() {
        activeCandidate = null;
        activePlayerGrab = null;
        candidateElapsed = 0f;
        handReach?.CancelReach();
    }
}

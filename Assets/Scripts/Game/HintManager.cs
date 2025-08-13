using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays contextual gameplay hints based on player actions and world events.
/// Ensures only one hint is visible at a time and avoids repeating messages
/// unless explicitly allowed (e.g., lingering near a locked door).
/// </summary>
public class HintManager : MonoBehaviour
{
    private enum HintType
    {
        MovementEnergy,
        TargetAttack,
        InteractGrab,
        Health,
        MachinesSaving,
        Security,
        Alarm,
        ObjectiveExit,
        FinalRoomReminder
    }

    private readonly Dictionary<HintType, GameMessage> hintLookup = new()
    {
        { HintType.MovementEnergy, GameMessages.Hints.MovementEnergy },
        { HintType.TargetAttack, GameMessages.Hints.TargetAttack },
        { HintType.InteractGrab, GameMessages.Hints.InteractGrab },
        { HintType.Health, GameMessages.Hints.Health },
        { HintType.MachinesSaving, GameMessages.Hints.MachinesSaving },
        { HintType.Security, GameMessages.Hints.Security },
        { HintType.Alarm, GameMessages.Hints.Alarm },
        { HintType.ObjectiveExit, GameMessages.Hints.ObjectiveExit },
        { HintType.FinalRoomReminder, GameMessages.Hints.FinalRoomReminder },
    };

    private readonly HashSet<HintType> shownHints = new();
    private readonly Dictionary<HintType, float> lastShownTimes = new();

    private HintType? activeHint = null;
    private Coroutine clearHintCoroutine;
    private readonly Queue<GameMessage> queuedHints = new();
    private bool processingQueue = false;

    [Header("References")]
    private HealthBot health;
    private Inventory inventory;

    private IPlayerInput inputSource;

    // Door tracking for re-showing security hints
    private bool nearLockedDoor = false;
    private float doorTimer = 0f;

    /// <summary>
    /// Initializes the HintManager with required references.
    /// </summary>
    public void Setup(IPlayerInput input, HealthBot health, Inventory inventory)
    {
        this.inputSource = input;
        this.health = health;
        this.inventory = inventory;


        if (this.health != null)
            this.health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;
    }

    /// <summary>
    /// Queues a hint to be displayed in order without overlapping others.
    /// </summary>
    /// <param name="hint">The hint message to display.</param>
    public void QueueHint(GameMessage hint)
    {
        if (hint.Text == null || hint.Text.Length == 0)
            return;

        queuedHints.Enqueue(hint);

        if (!processingQueue)
            StartCoroutine(ProcessQueuedHints());
    }

    private IEnumerator ProcessQueuedHints()
    {
        processingQueue = true;

        while (queuedHints.Count > 0)
        {
            var nextHint = queuedHints.Dequeue();
            MessageService.Instance?.ShowHint(nextHint, 7f);
            yield return new WaitForSeconds(7f);
        }

        processingQueue = false;
    }

    private void Update()
    {
        if (inputSource != null)
        {
            if (inputSource.Movement.sqrMagnitude > 0.01f)
                TryShowHint(HintType.MovementEnergy);

            if (inputSource.PrimaryAttack)
                TryShowHint(HintType.TargetAttack);

            if (inputSource.LeftGrabDown || inputSource.RightGrabDown)
                TryShowHint(HintType.InteractGrab);
        }

        if (nearLockedDoor)
        {
            doorTimer += Time.deltaTime;
            if (doorTimer >= 5f)
            {
                TryShowHint(HintType.Security, allowRepeat: true, repeatDelay: 5f);
                doorTimer = 0f;
            }
        }
    }

    private void HandleHealthChanged(float change)
    {
        if (change < 0f)
            TryShowHint(HintType.Health);
    }

    private void TryShowHint(HintType hint, bool allowRepeat = false, float repeatDelay = 0f)
    {
        if (MessageService.Instance == null)
            return;

        if (!MessageService.Instance.IsNotDisplaying && activeHint != null)
            return; // A hint is already being shown

        if (shownHints.Contains(hint))
        {
            if (!allowRepeat)
                return;

            if (lastShownTimes.TryGetValue(hint, out var lastTime))
                if (Time.time - lastTime < repeatDelay)
                    return;
        }

        ShowHint(hint);
    }

    private void ShowHint(HintType hint)
    {
        activeHint = hint;
        shownHints.Add(hint);
        lastShownTimes[hint] = Time.time;

        MessageService.Instance.ShowMessage(hintLookup[hint], 7f);

        if (clearHintCoroutine != null)
            StopCoroutine(clearHintCoroutine);
        clearHintCoroutine = StartCoroutine(ClearActiveHint(7f));
    }

    private IEnumerator ClearActiveHint(float delay)
    {
        yield return new WaitForSeconds(delay);
        activeHint = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Machine"))
        {
            TryShowHint(HintType.MachinesSaving);
        }
        else if (other.CompareTag("Door"))
        {
            var door = other.GetComponent<DoorController>();
            if (door != null && door.normalRequiresBadge && !HasBadge())
            {
                nearLockedDoor = true;
                doorTimer = 0f;
                TryShowHint(HintType.Security, allowRepeat: true, repeatDelay: 5f);
            }
        }
        else if (other.CompareTag("Item"))
        {
            TryShowHint(HintType.InteractGrab);
        }
        else if (other.CompareTag("Enemy"))
        {
            TryShowHint(HintType.TargetAttack);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Door"))
        {
            nearLockedDoor = false;
            doorTimer = 0f;
            if (activeHint == HintType.Security)
            {
                MessageService.Instance.HideMessage();
                activeHint = null;
            }
        }
    }

    private bool HasBadge()
    {
        return inventory != null && inventory.HasItem(PickupType.SecurityBadge);
    }

    /// <summary>
    /// External systems can call when an alarm is triggered.
    /// </summary>
    public void OnAlarmTriggered()
    {
        TryShowHint(HintType.Alarm);
    }

    /// <summary>
    /// Call when objectives change to possibly display exit hints.
    /// </summary>
    public void OnObjectiveProgress(int completed, int required, bool finalRoom = false)
    {
        if (completed >= required)
            TryShowHint(HintType.ObjectiveExit);

        if (finalRoom)
            TryShowHint(HintType.FinalRoomReminder);
    }
}


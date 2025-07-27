using System.Collections;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door Parts")]
    public Transform leftPanel;
    public Transform rightPanel;
    public Collider2D solidCollider;
    public float openDistance = 1f;
    public float openSpeed = 2f;

    [Header("Door Settings")]
    public RoomManager roomManager;
    public Vector2 moveDirection = Vector2.right; // Direction of the door movement (left/right)

    [Header("Door Security Settings")]
    public bool isWall = false;
    public bool normalRequiresBadge = false;
    public bool alarmLocksDoor = false;

    [Header("Door Display Panel (optional)")]
    public Renderer statusPanelRenderer;
    public Material normalMaterial;
    public Material lockedMaterial;
    public Material blockedMaterial;

    private bool isAlarmActive = false;
    private bool isRevolt = false;
    private bool isOpen = false;
    private bool isAnimating = false;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private int entitiesInside = 0;
    private int securityEntitiesInside = 0;

    [Header("Door Settings")]
    public float safetyCheckInterval = 5f;

    [Header("Victory")]
    public bool isVictoryDoor = false;

    private void Start()
    {
        if (roomManager != null)
        {
            if (moveDirection == Vector2.left)
            {
                isWall = !roomManager.roomProperties.HasLeftDoor;
                normalRequiresBadge = roomManager.roomProperties.HasLeftDoorLocked;
                isVictoryDoor = roomManager.roomProperties.IsVictoryDoorLeft;
            }
            else if (moveDirection == Vector2.right)
            {
                isWall = !roomManager.roomProperties.HasRightDoor;
                normalRequiresBadge = roomManager.roomProperties.HasRightDoorLocked;
                isVictoryDoor = roomManager.roomProperties.IsVictoryDoorRight;
            }
        }
        CachePanelPositions();

        if (solidCollider != null)
            solidCollider.enabled = true;

        if (roomManager != null)
            roomManager.OnRoomAlarmChanged += OnRoomAlarmChanged;

        UpdateStatusPanel();
        // Start the safety check coroutine
        StartCoroutine(SafetyCheckRoutine());
    }

    private IEnumerator SafetyCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(safetyCheckInterval);

            if (isWall)
            {
                CloseDoor(forceClose: true);
                continue;
            }

            // Re-use your centralized logic
            EvaluateDoorState();
        }
    }
    private void OnDestroy()
    {
        if (roomManager != null)
            roomManager.OnRoomAlarmChanged -= OnRoomAlarmChanged;
    }

    private void CachePanelPositions()
    {
        leftClosedPos = leftPanel.localPosition;
        rightClosedPos = rightPanel.localPosition;
        leftOpenPos = leftClosedPos + Vector3.left * openDistance;
        rightOpenPos = rightClosedPos + Vector3.right * openDistance;
    }

    private void OnRoomAlarmChanged(AlarmState state)
    {
        isAlarmActive = (state == AlarmState.Wanted || state == AlarmState.Lockdown);
        isRevolt = state == AlarmState.Revolt;
        EvaluateDoorState();
    }

    // These two methods are called from UnityEvent setup in Inspector
    public void OnEntityEnterZone()
    {
        entitiesInside++;
        EvaluateDoorState();
    }

    public void OnEntityExitZone()
    {
        entitiesInside = Mathf.Max(0, entitiesInside - 1); // Ensure it never goes negative
        EvaluateDoorState();
    }

    public void OnSecurityEntityEnterZone()
    {
        securityEntitiesInside++;
        EvaluateDoorState();
    }

    public void OnSecurityEntityExitZone()
    {
        securityEntitiesInside = Mathf.Max(0, securityEntitiesInside - 1); // Ensure it never goes negative
        EvaluateDoorState();
    }
    public void EvaluateDoorState()
    {
        if (isRevolt)
        {
            OpenDoor();
            UpdateStatusPanel();
            return;
        }
        if (isWall)
        {
            CloseDoor(forceClose: true);
            UpdateStatusPanel();
            return;
        }

        if (securityEntitiesInside > 0)
        {
            OpenDoor();
            UpdateStatusPanel();
            return;
        }

        bool shouldLock = (isAlarmActive && alarmLocksDoor)
            || (!isAlarmActive && normalRequiresBadge);

        if (shouldLock)
        {
            CloseDoor();
        }
        else
        {
            if (entitiesInside > 0)
                OpenDoor();
            else
                CloseDoor();
        }

        UpdateStatusPanel();
    }

    private Coroutine slidingCoroutine;
    private void OpenDoor()
    {
        if (isOpen || isAnimating) return;

        isOpen = true;
        if (solidCollider != null)
            solidCollider.enabled = false;

        if (slidingCoroutine != null)
            StopCoroutine(slidingCoroutine);
        slidingCoroutine = StartCoroutine(SlidePanels(leftOpenPos, rightOpenPos));
    }

    private void CloseDoor(bool forceClose = false)
    {
        if ((!isOpen && !forceClose) || isAnimating) return;

        isOpen = false;
        if (solidCollider != null)
            solidCollider.enabled = true;

        if (slidingCoroutine != null)
            StopCoroutine(slidingCoroutine);
        slidingCoroutine = StartCoroutine(SlidePanels(leftClosedPos, rightClosedPos));
    }

    private IEnumerator SlidePanels(Vector3 leftTarget, Vector3 rightTarget)
    {
        isAnimating = true;

        bool opening = (leftTarget == leftOpenPos); // Detect if we're opening or closing

        while (Vector3.Distance(leftPanel.localPosition, leftTarget) > 0.01f ||
               Vector3.Distance(rightPanel.localPosition, rightTarget) > 0.01f)
        {
            leftPanel.localPosition = Vector3.MoveTowards(leftPanel.localPosition, leftTarget, openSpeed * Time.deltaTime);
            rightPanel.localPosition = Vector3.MoveTowards(rightPanel.localPosition, rightTarget, openSpeed * Time.deltaTime);
            yield return null;
        }

        leftPanel.localPosition = leftTarget;
        rightPanel.localPosition = rightTarget;

        isAnimating = false;

        // ✅ Only now set isOpen depending on final position
        isOpen = opening;
    }


    private void UpdateStatusPanel()
    {
        if (statusPanelRenderer == null) return;

        if (isWall)
        {
            statusPanelRenderer.material = blockedMaterial;
        }
        else if ((isAlarmActive && alarmLocksDoor) || (!isAlarmActive && normalRequiresBadge))
        {
            statusPanelRenderer.material = lockedMaterial;
        }
        else
        {
            statusPanelRenderer.material = normalMaterial;
        }
    }
}

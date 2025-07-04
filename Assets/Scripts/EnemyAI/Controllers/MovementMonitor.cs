using UnityEngine;

public class MovementMonitor
{
    private Vector3 lastCheckPosition;
    private float checkTimer;
    private int recoveryAttempts;

    private readonly float interval;
    private readonly float minMovement;
    private readonly int maxAttempts;

    public MovementMonitor(float interval = 10f, float minMovement = 1f, int maxAttempts = 6)
    {
        this.interval = interval;
        this.minMovement = minMovement;
        this.maxAttempts = maxAttempts;
        Reset(Vector3.zero);
    }

    public void Reset(Vector3 currentPosition)
    {
        lastCheckPosition = currentPosition;
        checkTimer = 0f;
        recoveryAttempts = 0;
    }

    public MovementStatus Update(float deltaTime, Vector3 currentPosition)
    {
        checkTimer += deltaTime;

        if (checkTimer < interval)
            return MovementStatus.MovingNormally;

        float moved = Vector3.Distance(currentPosition, lastCheckPosition);
        checkTimer = 0f;

        if (moved < minMovement)
        {
            recoveryAttempts++;
            if (recoveryAttempts >= maxAttempts)
                return MovementStatus.Stuck;

            return MovementStatus.ShouldAttemptRecovery;
        }

        lastCheckPosition = currentPosition;
        recoveryAttempts = 0;
        return MovementStatus.MovingNormally;
    }
}

public enum MovementStatus
{
    MovingNormally,
    ShouldAttemptRecovery,
    Stuck
}

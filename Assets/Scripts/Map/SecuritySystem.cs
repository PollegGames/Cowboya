using UnityEngine;
public class SecuritySystem : MonoBehaviour
{
    public AlarmFlasher alarmFlasher;

    void Start()
    {
        // Example: activate alarm after 2 seconds
        // Invoke(nameof(TriggerAlarm), 2f);
    }

    void TriggerAlarm()
    {
        alarmFlasher.ActivateAlarm();
    }

    void StopAlarm()
    {
        alarmFlasher.DeactivateAlarm();
    }
}

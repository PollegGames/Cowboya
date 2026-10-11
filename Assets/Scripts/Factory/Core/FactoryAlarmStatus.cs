using UnityEngine;
using System;

[CreateAssetMenu(fileName = "FactoryAlarmStatus", menuName = "Map/FactoryAlarmStatus")]
public class FactoryAlarmStatus : ScriptableObject
{
    [SerializeField]
    private AlarmState currentAlarmState = AlarmState.Normal;
    public AlarmState CurrentAlarmState
    {
        get => currentAlarmState;
        set
        {
            if (currentAlarmState != value)
            {
                currentAlarmState = value;
                OnAlarmStateChanged?.Invoke(currentAlarmState);
            }
        }
    }

    public event Action<AlarmState> OnAlarmStateChanged;

    public Vector3 LastPlayerPosition { get; set; }

    /// <summary>Restores level-local alarm state and notifies every active effect owner.</summary>
    public void ResetForLevel()
    {
        currentAlarmState = AlarmState.Normal;
        LastPlayerPosition = Vector3.zero;
        OnAlarmStateChanged?.Invoke(currentAlarmState);
    }
}

public enum AlarmState
{
    Normal,
    Wanted,
    Revolt,
    Lockdown
}

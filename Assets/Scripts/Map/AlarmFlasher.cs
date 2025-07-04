using UnityEngine;

public class AlarmFlasher : MonoBehaviour
{
    public Renderer targetRenderer;
    public float flashSpeed = 2f;
    public RoomManager roomManager; // <<< Connected to RoomManager

    private Color normalColor = Color.white;
    private Color alarmColor = Color.red;
    private Material sharedMat;

    private bool isFlashing = false;
    private float timer = 0f;

    private void Start()
    {
        if (targetRenderer != null)
        {
            sharedMat = targetRenderer.sharedMaterial;
            sharedMat.color = normalColor;
        }

        if (roomManager != null)
        {
            roomManager.OnRoomAlarmChanged += OnAlarmChanged;
        }
    }

    private void OnDestroy()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomAlarmChanged -= OnAlarmChanged;
        }
    }

    private void Update()
    {
        if (!isFlashing || sharedMat == null) return;

        timer += Time.deltaTime * flashSpeed;
        float t = Mathf.PingPong(timer, 1f);
        sharedMat.color = Color.Lerp(normalColor, alarmColor, t);
    }

    private void OnAlarmChanged(AlarmState state)
    {
        if (state == AlarmState.Wanted || state == AlarmState.Lockdown)
        {
            ActivateAlarm();
        }
        else
        {
            DeactivateAlarm();
        }
    }

    public void ActivateAlarm()
    {
        isFlashing = true;
        timer = 0f;
    }

    public void DeactivateAlarm()
    {
        isFlashing = false;
        if (sharedMat != null)
            sharedMat.color = normalColor;
    }
}

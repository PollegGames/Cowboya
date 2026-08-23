using UnityEngine;

public class AlarmFlasher : MonoBehaviour
{
    public Renderer targetRenderer;
    public float flashSpeed = 2f;
    public RoomManager roomManager; // <<< Connected to RoomManager

    private Color normalColor = Color.white;
    private Color alarmColor = Color.red;
    private Material runtimeMaterial;

    private bool isFlashing = false;
    private float timer = 0f;

    private void Start()
    {
        if (targetRenderer != null)
        {
            // Renderer.material creates an instance for this alarm. Changing the
            // shared material would modify the material asset and leave the
            // prefab tinted after exiting Play Mode.
            runtimeMaterial = targetRenderer.material;
            runtimeMaterial.color = normalColor;
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

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void OnDisable()
    {
        DeactivateAlarm();
    }

    private void Update()
    {
        if (!isFlashing || runtimeMaterial == null) return;

        timer += Time.deltaTime * flashSpeed;
        float t = Mathf.PingPong(timer, 1f);
        runtimeMaterial.color = Color.Lerp(normalColor, alarmColor, t);
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
        if (runtimeMaterial != null)
            runtimeMaterial.color = normalColor;
    }
}

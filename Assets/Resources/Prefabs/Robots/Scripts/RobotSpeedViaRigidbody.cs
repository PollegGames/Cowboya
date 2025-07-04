using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RobotSpeedViaRigidbody : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Smoothing and Scaling")]
    [SerializeField] private float smoothTime = 0.5f;
    [SerializeField] private float speedMultiplier = 2f;

    private float smoothedSpeed = 0f;
    public float CurrentSpeed => smoothedSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public float GetStepDuration(float minDuration, float maxDuration)
    {
        float t = Mathf.Clamp01(smoothedSpeed / 10f);
        return Mathf.Lerp(maxDuration, minDuration, t);
    }


    private void Update()
    {
        float rawSpeed = rb.linearVelocity.magnitude * speedMultiplier;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, Time.deltaTime / smoothTime);
    }
}

using System.Collections;
using UnityEngine;

public class AlternatingPunchAttack : MonoBehaviour
{
    [SerializeField] private ArcTargetFollower arcTargetFollower; // assign in inspector

    [Header("Punch Targets")]
    public Transform leftArmTarget;
    public Transform rightArmTarget;
    public Transform leftRestPosition;
    public Transform rightRestPosition;
    public Transform punchTarget; // position de la souris ou un ennemi

    [Header("Arc Control Points")]
    public Transform arcControlRight;
    public Transform arcControlLeft;

    [Header("Timing and Speed")]
    public float punchDuration = 0.2f;
    public float returnSpeed = 10f;

    [Header("Attack Hitboxes")]
    public AttackHitbox leftArmHitbox;
    public AttackHitbox rightArmHitbox;


    private bool isPunching = false;

    private RobotBehaviour robotBehaviour;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotBehaviour>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isPunching && robotBehaviour != null)
        {
            bool facingRight = arcTargetFollower.IsFacingRight;

            // Choix du bras selon la direction
            Transform armTarget = facingRight ? rightArmTarget : leftArmTarget;
            AttackHitbox hitbox = facingRight ? rightArmHitbox : leftArmHitbox;
            float damageCost = hitbox.DamageCost;

            // Consomme l’énergie
            robotBehaviour.PerformAttackbyEnergy(damageCost);

            // Lance la coroutine
            StartCoroutine(PunchSequence(armTarget, hitbox, facingRight));
        }
    }

    private IEnumerator PunchSequence(Transform armTarget, AttackHitbox hitbox, bool facingRight)
    {
      isPunching = true;

        Vector3 start = armTarget.position;
        Vector3 end = punchTarget.position;

        // Point de contrôle de l'arc
        Transform controlPoint = facingRight ? arcControlRight : arcControlLeft;
        Vector3 control = controlPoint != null ? controlPoint.position : (start + end) / 2 + Vector3.up * 0.5f;

        // Position de repos
        Transform restPosition = facingRight ? rightRestPosition : leftRestPosition;

        hitbox?.Activate();

        float timer = 0f;
        while (timer < punchDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / punchDuration);
            armTarget.position = GetQuadraticBezierPoint(start, control, end, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        hitbox?.Deactivate();

        while (Vector3.Distance(armTarget.position, restPosition.position) > 0.01f)
        {
            armTarget.position = Vector3.MoveTowards(armTarget.position, restPosition.position, returnSpeed * Time.deltaTime);
            yield return null;
        }

        isPunching = false;
    }

    private Vector3 GetQuadraticBezierPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return Mathf.Pow(1 - t, 2) * a +
               2 * (1 - t) * t * b +
               Mathf.Pow(t, 2) * c;
    }
}

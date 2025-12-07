using UnityEngine;

public class Hinge2DIkSolver : MonoBehaviour
{
    public static float reflectionForce = 0.5f;
    public Transform target;
    public Transform pole;
    public int chainLength = 2;
    public float SetForce = 75;
    private float forceValue;
    private float dforce;
    public float force
    {
        get
        {
            if (SetForce != forceValue)
            {
                force = SetForce;
            }
            return forceValue;
        }
        set
        {
            forceValue = value;
            SetForce = value;
            dforce = 1f / (value * value);
        }
    }

    public float delta = 0.25f;
    public int iterations = 10;
    public float poleMinDistance = 1;
    public float SnapStrength = 1f;
    public int updateEveryXFrames = 5;
    public bool drawGizmos = true;
    public float torqueStrength = 1f;

    private Vector2[] positions;
    private float[] bonesLength;
    private AnchoredJoint2D[] bones;
    private Rigidbody2D[] bonesR;
    private Transform[] bonesT;
    private Vector2[] startDir;
    private float[] angleOffset;
    private float completeLength = 0;
    private Transform root;
    private Rigidbody2D rootrb;
    private bool isReinitializing;
    public bool active = true;
    public delegate void OnSolve();
    public OnSolve CallBackOnSolve;

    /// <summary>
    /// Rebuilds cached joints and transforms and runs the solver once.
    /// </summary>
    public void Reinitialize()
    {
        if (isReinitializing)
        {
            return;
        }
        isReinitializing = true;

        positions = null;
        bones = null;
        bonesT = null;
        bonesR = null;
        startDir = null;
        bonesLength = null;
        avAngles = null;
        angleOffset = null;
        completeLength = 0f;
        root = null;
        rootrb = null;

        Init();
        Solve();

        isReinitializing = false;
    }

    private void OnEnable()
    {
        Reinitialize();
    }

    private void Init()
    {
        positions = new Vector2[chainLength + 1];
        bones = new AnchoredJoint2D[chainLength + 1];
        bonesT = new Transform[chainLength + 1];
        bonesR = new Rigidbody2D[chainLength + 1];
        startDir = new Vector2[chainLength + 1];
        bonesLength = new float[chainLength];
        avAngles = new float[chainLength];
        angleOffset = new float[chainLength];
        completeLength = 0;
        var current = GetComponent<AnchoredJoint2D>();
        if (target)
        {
            var controller = target.GetComponent<IKController>();
            if (!controller)
            {
                controller = target.gameObject.AddComponent<IKController>();
            }
            controller.Init(this);
        }

        for (int i = chainLength; i >= 0; i--)
        {
            bones[i] = current;
            bonesT[i] = current.transform;
            bonesR[i] = current.attachedRigidbody;
            if (i == chainLength)
            {
                startDir[i] = (Vector2)target.position - GetBonePos(i);
            }
            else
            {
                var dir = GetBonePos(i + 1) - GetBonePos(i);
                startDir[i] = dir;
                bonesLength[i] = dir.magnitude;
                completeLength += bonesLength[i];
            }
            current = current.connectedBody.GetComponent<AnchoredJoint2D>();
        }
        root = bones[0].connectedBody.transform;
        rootrb = root.GetComponent<Rigidbody2D>();
        if (!rootrb)
        {
            rootrb = root.GetComponentInParent<Rigidbody2D>();
        }
        if (!rootrb)
        {
            Debug.LogError("Hinge2DIkSolver: Root object is missing a Rigidbody2D. Solver disabled.", this);
            enabled = false;
        }

        for (int i = 1; i <= chainLength; i++)
        {
            avAngles[i - 1] = PlainMath.AngleBetween(GetBonePos(i), root.position);
            angleOffset[i - 1] = PlainMath.AngleFromDirection(GetBonePosVR2(i - 1) - GetBonePosVR2(i)) - root.eulerAngles.z;
        }
    }

    private bool IsValidBone(int index)
    {
        return bonesT != null && bones != null && index >= 0 &&
            index < bonesT.Length && bonesT[index] != null && bones[index] != null;
    }

    private Vector2 GetBonePos(int index)
    {
        if (!IsValidBone(index))
        {
            Reinitialize();
            if (!IsValidBone(index))
            {
                return Vector2.zero;
            }
        }
        return bonesT[index].rotation * bones[index].anchor + bonesT[index].position;
    }

    private Vector2 GetBonePosVR2(int index)
    {
        if (!IsValidBone(index))
        {
            Reinitialize();
            if (!IsValidBone(index))
            {
                return Vector2.zero;
            }
        }
        return bones[index].anchor + (Vector2)bonesT[index].position;
    }

    private void LateUpdate()
    {
        if (Time.frameCount % updateEveryXFrames == 0)
        {
            Solve();
        }
    }

    private void FixedUpdate()
    {
        if (active)
        {
            ApplyByTorque();
            ApplyPositions();
        }
    }

    private void Solve()
    {
        if (target == null)
            return;
        if (bonesLength.Length != chainLength)
            Init();
        CallBackOnSolve?.Invoke();

        Vector2 targetPos = target.position;
        for (int i = 0; i < chainLength + 1; i++)
        {
            if (!IsValidBone(i))
            {
                return;
            }
            positions[i] = GetBonePos(i);
        }

        var direction = positions[0] - targetPos;
        if (direction.sqrMagnitude > completeLength * completeLength)
        {
            for (int i = 1; i < chainLength + 1; i++)
            {
                positions[i] = positions[i - 1] - direction.normalized * bonesLength[i - 1];
            }
        }
        else
        {
            for (int i = 0; i < chainLength; i++)
            {
                positions[i + 1] = Vector3.Lerp(positions[i + 1],
                    positions[i] + startDir[i], SnapStrength);
            }

            for (int k = 0; k < iterations; k++)
            {
                positions[chainLength] = targetPos;
                for (int i = chainLength - 1; i > 0; i--)
                {
                    positions[i] = positions[i + 1] + (positions[i] - positions[i + 1]).normalized * bonesLength[i];
                }

                for (int i = 1; i < chainLength + 1; i++)
                {
                    var dir = positions[i] - positions[i - 1];
                    positions[i] = positions[i - 1] + dir.normalized * bonesLength[i - 1];
                }

                if ((targetPos - positions[chainLength]).sqrMagnitude < delta * delta)
                {
                    break;
                }
            }

            if (pole)
            {
                Vector2 polePos = (pole.position - root.position) * 100 + root.position;
                for (int i = 1; i < chainLength; i++)
                {
                    var dir = positions[i + 1] - positions[i - 1];
                    var closest = PlainMath.ClosestOnLine(positions[i + 1], dir, positions[i]);
                    var toCenter = closest - positions[i];
                    var poleToCenter = closest - polePos;
                    if (poleToCenter.sqrMagnitude > poleMinDistance && Vector2.Dot(toCenter, poleToCenter) < 0)
                    {
                        positions[i] = closest + toCenter;
                    }
                }
            }
        }
    }

    private float[] avAngles;
    private const float Tc = 0.05f;

    private void ApplyByTorque()
    {
        if (rootrb == null || rootrb.linearVelocity == Vector2.zero)
        {
            if (rootrb == null)
            {
                Reinitialize();
            }
            return;
        }
        float sqrVel = rootrb.linearVelocity.sqrMagnitude;
        float velocityMod = sqrVel <= Mathf.Epsilon ? 1f : 10f / sqrVel;
        velocityMod = Mathf.Clamp01(velocityMod);
        var fixedStep = 25 * Time.fixedDeltaTime;
        for (int i = 1; i <= chainLength; i++)
        {
            if (!IsValidBone(i - 1) || bonesR == null || bonesR[i - 1] == null)
            {
                continue;
            }
            var targetAngle = PlainMath.AngleFromDirection(positions[i - 1] - positions[i]);
            var angle = -Mathf.DeltaAngle(bonesT[i - 1].eulerAngles.z + angleOffset[i - 1], targetAngle);
            var direction = angle > 0 ? 1 : -1;
            var velocity = bonesR[i - 1].angularVelocity;
            angle = Mathf.Abs(angle) * 0.1f;
            if (angle < 1)
            {
                angle = angle * angle;
            }

            var reflection = velocity * fixedStep * reflectionForce * velocityMod;
            reflection = Mathf.Clamp(reflection, -force * 0.2f, force * 0.2f);
            rootrb.AddTorque(reflection);
            bonesR[i - 1].AddTorque(-reflection);

            var forceMultiplier = Mathf.Log10((chainLength - i) * 10 + 10);
            var torque = angle * direction * force * torqueStrength * forceMultiplier * fixedStep * 6;
            rootrb.AddTorque(torque);
            bonesR[i - 1].AddTorque(-torque);
        }
    }

    private float vn = 1;
    private const float RForce = 0.5f;

    private void ApplyPositions()
    {
        if (rootrb == null || rootrb.linearVelocity == Vector2.zero || bonesR == null)
        {
            if (rootrb == null || bonesR == null)
            {
                Reinitialize();
            }
            return;
        }

        float n = 10 / rootrb.linearVelocity.magnitude;
        n = Mathf.Clamp01(n);
        vn = n * 0.1f + vn * 0.9f;

        var fixedStep = 25 * Time.fixedDeltaTime;
        for (int i = 0; i <= chainLength; i++)
        {
            if (!IsValidBone(i) || bonesR == null || bonesR[i] == null)
            {
                continue;
            }

            var dir = positions[i] - GetBonePos(i);
            var velocity = bonesR[i].linearVelocity * vn * fixedStep;

            if (velocity.sqrMagnitude > force * force * RForce * RForce)
            {
                velocity = velocity.normalized * force * RForce;
            }

            rootrb.AddForce(velocity * reflectionForce, ForceMode2D.Impulse);
            bonesR[i].AddForce(-velocity * reflectionForce, ForceMode2D.Impulse);
            var addForce = Mathf.Log10((chainLength - i) * 10 + 10) * force * 6 * fixedStep;
            bonesR[i].AddForce(dir * addForce * vn);
            rootrb.AddForce(-dir * addForce * vn);
        }
    }

#if UNITY_EDITOR
    public float gsize = 0.1f;
    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;
        Gizmos.color = Color.white;
        try
        {
            if (bones != null)
            {
                Random.InitState(10);
                for (int i = 0; i < bones.Length; i++)
                {
                    PlainMath.NextGizmosColor();
                    Gizmos.DrawSphere(GetBonePos(i), gsize);
                }

                for (int i = 0; i < chainLength + 1; i++)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(positions[i], gsize);
                }
            }
        }
        catch
        {
        }
    }
#endif
}

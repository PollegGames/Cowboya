using System.Collections.Generic;
using System.Text;
using CowBoya.Robots;
using UnityEngine;

/// <summary>
/// TEMPORARY debug tool. Measures the player's walk separately for left and right and logs a
/// summary every few seconds with the prefix [WalkDiag]:
/// - how far each physical (puppet) bone is from the animated (master) pose,
/// - bone angular speeds,
/// - foot ground contacts, airborne time and "hops",
/// - body speed and vertical bumps.
/// Remove the component (and this file) once the walk issue is understood.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class WalkDiagnostics : MonoBehaviour {
    [SerializeField] private bool logEnabled = true;
    [SerializeField, Min(0.5f)] private float reportInterval = 2f;
    [Tooltip("Bones to measure (puppet names from the SimplePuppetBinder pairs).")]
    [SerializeField] private string[] bones = { "LUpLeg_Bone", "RUpLeg_Bone", "LFoot_Bone", "RFoot_Bone", "Hips_Bone", "BodyLow_Bone", "Body_Bone" };
    [Tooltip("Bones whose collider contacts count as feet on the ground.")]
    [SerializeField] private string[] feet = { "LFoot_Bone", "RFoot_Bone" };

    private sealed class BoneStats {
        public double ErrorSum;
        public float ErrorMax;
        public double SpeedSum;
        public float SpeedMax;
        public double JumpSum;
        public float JumpMax;
        public int BigJumps;
        public int Samples;
    }

    private readonly Dictionary<SimplePuppetBinder.BonePair, float> previousTarget = new Dictionary<SimplePuppetBinder.BonePair, float>();

    private sealed class DirectionStats {
        public readonly Dictionary<string, BoneStats> Bones = new Dictionary<string, BoneStats>();
        public int Frames;
        public int AirborneFrames;
        public int SingleFootFrames;
        public int Hops;
        public double SpeedSum;
        public float MaxUpVelocity;
        public float MaxDownVelocity;
        public readonly int[] FootContactFrames = new int[2];
        public int FootChanges;
    }

    private SimplePuppetBinder binder;
    private PlayerMovementController movement;
    private Rigidbody2D body;
    private Transform root;
    private readonly List<SimplePuppetBinder.BonePair> measuredPairs = new List<SimplePuppetBinder.BonePair>();
    private readonly List<Rigidbody2D> footBodies = new List<Rigidbody2D>();
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private readonly Dictionary<int, DirectionStats> stats = new Dictionary<int, DirectionStats>();
    private bool wasAirborne;
    private bool[] lastFootGrounded = new bool[2];
    private float nextReportTime;

    private void Start() {
        binder = GetComponent<SimplePuppetBinder>();
        movement = GetComponent<PlayerMovementController>();
        body = movement != null ? movement.BodyReference : null;
        root = transform.root;

        if (binder != null) {
            foreach (SimplePuppetBinder.BonePair pair in binder.Pairs) {
                if (pair?.Puppet == null) {
                    continue;
                }

                if (System.Array.IndexOf(bones, pair.Puppet.name) >= 0) {
                    measuredPairs.Add(pair);
                }
            }
        }

        foreach (string footName in feet) {
            Rigidbody2D foot = FindBody(footName);
            footBodies.Add(foot);
        }

        nextReportTime = Time.time + reportInterval;
        Debug.Log($"[WalkDiag] Started: {measuredPairs.Count} bones, feet found={footBodies.FindAll(f => f != null).Count}, body={(body != null ? body.name : "none")}.", this);
    }

    private Rigidbody2D FindBody(string boneName) {
        foreach (Rigidbody2D rb in GetComponentsInChildren<Rigidbody2D>(true)) {
            if (rb.name == boneName) {
                return rb;
            }
        }

        return null;
    }

    private void FixedUpdate() {
        if (!logEnabled || movement == null) {
            return;
        }

        float input = movement.Movement.x;
        if (Mathf.Abs(input) < 0.1f) {
            wasAirborne = false;
            previousTarget.Clear();
            return;
        }

        int direction = input > 0f ? 1 : -1;
        if (!stats.TryGetValue(direction, out DirectionStats s)) {
            s = new DirectionStats();
            stats[direction] = s;
        }

        s.Frames++;
        foreach (SimplePuppetBinder.BonePair pair in measuredPairs) {
            Rigidbody2D rb = pair.PuppetBody2D;
            if (rb == null || !pair.hasRotationTarget) {
                continue;
            }

            if (!s.Bones.TryGetValue(pair.Puppet.name, out BoneStats b)) {
                b = new BoneStats();
                s.Bones[pair.Puppet.name] = b;
            }

            // How fast the ANIMATED (master) pose of this bone changes: a big value per physics
            // step means the animation/IK snaps, which the physical bone cannot follow.
            float targetAngle = pair.targetRotation.eulerAngles.z;
            if (previousTarget.TryGetValue(pair, out float previous)) {
                float jump = Mathf.Abs(Mathf.DeltaAngle(previous, targetAngle));
                b.JumpSum += jump;
                b.JumpMax = Mathf.Max(b.JumpMax, jump);
                if (jump > 20f) {
                    b.BigJumps++;
                    LogSnap(pair, direction, previous, targetAngle);
                }
            }
            previousTarget[pair] = targetAngle;

            float error = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, targetAngle));
            float speed = Mathf.Abs(rb.angularVelocity);
            b.ErrorSum += error;
            b.ErrorMax = Mathf.Max(b.ErrorMax, error);
            b.SpeedSum += speed;
            b.SpeedMax = Mathf.Max(b.SpeedMax, speed);
            b.Samples++;
        }

        int grounded = 0;
        for (int i = 0; i < footBodies.Count && i < 2; i++) {
            bool onGround = IsTouchingWorld(footBodies[i]);
            if (onGround) {
                grounded++;
                s.FootContactFrames[i]++;
            }

            if (onGround != lastFootGrounded[i]) {
                s.FootChanges++;
            }

            lastFootGrounded[i] = onGround;
        }

        bool airborne = grounded == 0;
        if (airborne) {
            s.AirborneFrames++;
            if (!wasAirborne) {
                s.Hops++;
            }
        }
        else if (grounded == 1) {
            s.SingleFootFrames++;
        }

        wasAirborne = airborne;

        if (body != null) {
            s.SpeedSum += Mathf.Abs(body.linearVelocity.x);
            s.MaxUpVelocity = Mathf.Max(s.MaxUpVelocity, body.linearVelocity.y);
            s.MaxDownVelocity = Mathf.Min(s.MaxDownVelocity, body.linearVelocity.y);
        }
    }

    [Tooltip("Maximum detailed snap lines logged per direction.")]
    [SerializeField] private int maxSnapLogsPerDirection = 25;
    private readonly Dictionary<int, int> snapLogs = new Dictionary<int, int>();
    private readonly Dictionary<string, float> previousKnee = new Dictionary<string, float>();
    private readonly Dictionary<string, float> currentKnee = new Dictionary<string, float>();

    /// <summary>
    /// Logs one animated-pose snap of an upper leg: IK target position, knee angle before/after,
    /// to tell an IK knee-solution flip from a curve jump.
    /// </summary>
    private void LogSnap(SimplePuppetBinder.BonePair pair, int direction, float previousAngle, float newAngle) {
        string bone = pair.Puppet.name;
        if (!bone.Contains("UpLeg")) {
            return;
        }

        snapLogs.TryGetValue(direction, out int count);
        if (count >= maxSnapLogsPerDirection) {
            return;
        }
        snapLogs[direction] = count + 1;

        string side = bone.StartsWith("L") ? "L" : "R";
        Transform shin = FindMaster(side + "Foot_Bone");
        Transform ikTarget = FindDeep(transform, side + "LegSolver_Target");
        _ = shin;
        currentKnee.TryGetValue(side, out float knee);
        previousKnee.TryGetValue(side, out float kneeBefore);

        string targetText = ikTarget != null ? $"({ikTarget.localPosition.x:0.00},{ikTarget.localPosition.y:0.00})" : "?";
        Debug.Log(
            $"[WalkDiag] SNAP dir={(direction > 0 ? "RIGHT" : "LEFT ")} {side}UpLeg {previousAngle:0}->{newAngle:0} "
            + $"(jump {Mathf.DeltaAngle(previousAngle, newAngle):0}) knee(before?)={kneeBefore:0} knee={knee:0} ikTarget={targetText} t={Time.time:0.00}",
            this);
    }

    private Transform FindMaster(string puppetName) {
        if (binder == null) {
            return null;
        }

        foreach (SimplePuppetBinder.BonePair p in binder.Pairs) {
            if (p?.Puppet != null && p.Puppet.name == puppetName) {
                return p.Master;
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform rootTransform, string childName) {
        foreach (Transform t in rootTransform.GetComponentsInChildren<Transform>(true)) {
            if (t.name == childName) {
                return t;
            }
        }

        return null;
    }

    private void LateUpdate() {
        // Track the master knee angle every frame so a snap can be compared with the frame before.
        foreach (string side in new[] { "L", "R" }) {
            Transform thigh = FindMasterCached(side + "UpLeg_Bone");
            Transform shin = FindMasterCached(side + "Foot_Bone");
            if (thigh != null && shin != null) {
                if (currentKnee.TryGetValue(side, out float last)) {
                    previousKnee[side] = last;
                }
                currentKnee[side] = Mathf.DeltaAngle(thigh.eulerAngles.z, shin.eulerAngles.z);
            }
        }
    }

    private readonly Dictionary<string, Transform> masterCache = new Dictionary<string, Transform>();

    private Transform FindMasterCached(string puppetName) {
        if (!masterCache.TryGetValue(puppetName, out Transform t) || t == null) {
            t = FindMaster(puppetName);
            masterCache[puppetName] = t;
        }

        return t;
    }

    private bool IsTouchingWorld(Rigidbody2D foot) {
        if (foot == null) {
            return false;
        }

        int count = foot.GetContacts(contacts);
        for (int i = 0; i < count; i++) {
            // One side of the contact belongs to the foot; the other side is what it touches.
            Collider2D touched = contacts[i].collider != null && contacts[i].collider.attachedRigidbody == foot
                ? contacts[i].otherCollider
                : contacts[i].collider;
            if (touched != null && !touched.isTrigger && touched.transform.root != root) {
                return true;
            }
        }

        return false;
    }

    private void Update() {
        if (!logEnabled || Time.time < nextReportTime) {
            return;
        }

        nextReportTime = Time.time + reportInterval;
        foreach (KeyValuePair<int, DirectionStats> entry in stats) {
            DirectionStats s = entry.Value;
            if (s.Frames < 10) {
                continue;
            }

            var line = new StringBuilder();
            line.Append($"[WalkDiag] dir={(entry.Key > 0 ? "RIGHT" : "LEFT ")} frames={s.Frames} ");
            line.Append($"speed={s.SpeedSum / s.Frames:0.00} ");
            line.Append($"airborne={100f * s.AirborneFrames / s.Frames:0}% hops={s.Hops} singleFoot={100f * s.SingleFootFrames / s.Frames:0}% ");
            line.Append($"footGround(L/R)={100f * s.FootContactFrames[0] / s.Frames:0}%/{100f * s.FootContactFrames[1] / s.Frames:0}% footChanges={s.FootChanges} ");
            line.Append($"velY(max/min)={s.MaxUpVelocity:0.00}/{s.MaxDownVelocity:0.00} | ");
            foreach (KeyValuePair<string, BoneStats> b in s.Bones) {
                BoneStats bs = b.Value;
                if (bs.Samples == 0) {
                    continue;
                }

                line.Append($"{b.Key.Replace("_Bone", "")} err={bs.ErrorSum / bs.Samples:0}/{bs.ErrorMax:0} animStep={bs.JumpSum / bs.Samples:0.0}/{bs.JumpMax:0} snaps={bs.BigJumps}; ");
            }

            Debug.Log(line.ToString(), this);
        }

        stats.Clear();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CowBoya.Robots
{
    /// <summary>
    /// Links a set of puppet rigidbodies to their master transforms using force and torque based tracking.
    /// Includes balance modulation, contact awareness, and debug instrumentation.
    /// </summary>
    public class MasterPuppetLink : MonoBehaviour
    {
        public enum BoneRegion
        {
            Root,
            Torso,
            Hips,
            Legs,
            Arms,
            Head
        }

        public enum BalanceState
        {
            Normal,
            Unpinned,
            Recovery
        }

        [Serializable]
        public class BoneLink
        {
            public Transform Master;
            public Rigidbody2D Puppet;
            public float PositionStiffness = 120f;
            public float PositionDamping = 25f;
            public float RotationStiffness = 200f;
            public float RotationDamping = 20f;
            public float Strength = 1f;
            public bool EnablePosition = true;
            public bool EnableRotation = true;
            public bool UseLocalRotation = true;
            public BoneRegion Region = BoneRegion.Torso;
        }

        [Serializable]
        public class RegionStrength
        {
            public BoneRegion Region = BoneRegion.Torso;
            [Range(0f, 2f)]
            public float Multiplier = 1f;
        }

        [Serializable]
        public class StateRegionMultiplier
        {
            public BoneRegion Region = BoneRegion.Torso;
            [Range(0f, 2f)]
            public float Normal = 1f;
            [Range(0f, 2f)]
            public float Unpinned = 0.4f;
            [Range(0f, 2f)]
            public float Recovery = 1.3f;
        }

        [Serializable]
        public class ContactPoint
        {
            public string Id = "Foot";
            public Rigidbody2D Body;
            public Collider2D Sensor;
            [Range(0f, 2f)]
            public float StanceStrength = 1.2f;
            [Range(0f, 1f)]
            public float SlipStrength = 0.3f;
            [NonSerialized]
            public bool IsGrounded;
        }

        [Serializable]
        public class DebugSettings
        {
            public bool DrawTargets = true;
            public bool DrawCenterOfMass = true;
            public float GizmoScale = 0.08f;
            public Color TargetColor = new Color(0.18f, 0.65f, 1f, 0.6f);
            public Color PuppetColor = new Color(1f, 0.32f, 0.26f, 0.6f);
            public Color ComColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            public Color SupportColor = new Color(0.2f, 1f, 0.45f, 0.8f);
        }

        [Tooltip("Ordered list of master/puppet bone pairs that should stay aligned.")]
        public List<BoneLink> Links = new List<BoneLink>();

        [Header("Auto Population")]
        [Tooltip("Root transform used to search for master bones. Defaults to this transform.")]
        public Transform MasterRoot;
        [Tooltip("Root transform that contains all puppet rigidbodies to map against the master bones.")]
        public Transform PuppetRoot;
        [Tooltip("Automatically populate links during Awake/OnValidate when needed.")]
        public bool AutoPopulateOnStart = true;

        [Tooltip("Scalar applied to every force and torque. Lower to let the puppet fall, raise to tighten.")]
        [Range(0f, 4f)]
        public float GlobalStrength = 0.5f;

        [Header("Root Anchoring")]
        [Tooltip("Optional rigidbody used to keep the puppet centered. Leave empty to skip anchoring.")]
        public Rigidbody2D RootBody;

        [Tooltip("Target transform the root body should hover around. Defaults to this component transform.")]
        public Transform RootTarget;

        public float RootStiffness = 120f;
        public float RootDamping = 32f;
        [Range(0f, 4f)]
        public float RootStrength = 0.2f;
        public bool AnchorRoot = true;

        [Header("Force Limits")]
        [Tooltip("Multiplier applied to the positional force clamp relative to stiffness. Raise to allow stronger pulls.")]
        [Min(0f)]
        public float PositionForceClampScale = 10f;
        [Tooltip("Multiplier applied to the rotational torque clamp relative to stiffness. Raise to allow stronger twists.")]
        [Min(0f)]
        public float RotationTorqueClampScale = 45f;

        [Header("Regional Modulation")]
        public List<RegionStrength> RegionMultipliers = new List<RegionStrength>();
        public List<StateRegionMultiplier> StateRegionMultipliers = new List<StateRegionMultiplier>();

        [Header("Contacts")]
        public List<ContactPoint> Contacts = new List<ContactPoint>();
        public LayerMask GroundLayers = ~0;
        public float ContactStickTime = 0.08f;

        [Header("Balance Metrics")]
        public Rigidbody2D PelvisBody;
        public bool AutoAssignPelvisFromLinks = true;
        public Transform TorsoReference;
        public Transform CenterOfMassOverride;
        public float DesiredPelvisHeight = 1.2f;
        public float PelvisHeightTolerance = 0.3f;
        public float MaxTorsoTilt = 55f;
        public float ComDistanceThreshold = 0.55f;
        public float ComSmoothing = 0.12f;
        public float SupportSmoothing = 0.12f;
        public float TiltSmoothing = 0.2f;
        public float HeightSmoothing = 0.2f;
        public float StableDuration = 0.35f;
        public float RecoveryDuration = 1.1f;
        public float ImpactCooldown = 0.5f;
        public float ImpactVelocityThreshold = 4f;

        [Header("State Strength Scaling")]
        [Range(0f, 2f)]
        public float NormalStrengthScale = 1f;
        [Range(0f, 2f)]
        public float UnpinnedStrengthScale = 0.25f;
        [Range(0f, 2f)]
        public float RecoveryStrengthScale = 1.15f;

        [Range(0f, 2f)]
        public float NormalRootScale = 1f;
        [Range(0f, 2f)]
        public float UnpinnedRootScale = 0.3f;
        [Range(0f, 2f)]
        public float RecoveryRootScale = 1.4f;

        [Header("Controller Tuning")]
        [Tooltip("Positional error below this distance (in meters) is ignored to avoid micro jitter corrections.")]
        [Min(0f)]
        public float PositionDeadZone = 0.012f;

        [Tooltip("Velocity difference below this threshold (in meters/second) is ignored when inside the position dead zone.")]
        [Min(0f)]
        public float PositionVelocityDeadZone = 0.05f;

        [Tooltip("Angular error below this threshold (in degrees) is ignored to avoid micro jitter corrections.")]
        [Min(0f)]
        public float RotationDeadZone = 2f;

        [Tooltip("Angular velocity difference below this threshold (in degrees/second) is ignored when inside the rotation dead zone.")]
        [Min(0f)]
        public float RotationVelocityDeadZone = 25f;

        [Tooltip("When enabled, master transform linear velocity is subtracted from puppet velocity so forces are based on relative motion.")]
        public bool UseMasterVelocityFeedForward = true;

        [Tooltip("When enabled, master transform angular velocity is subtracted from puppet angular velocity so torques are based on relative motion.")]
        public bool UseMasterAngularVelocityFeedForward = true;

        [Header("Instrumentation")]
        public DebugSettings Debug = new DebugSettings();

        public BalanceState CurrentState => currentState;
        public Vector2 CurrentCenterOfMass => currentCenterOfMass;
        public Vector2 CurrentSupportCenter => currentSupportCenter;
        public float CurrentTorsoTilt => currentTorsoTilt;
        public float CurrentPelvisHeight => currentPelvisHeight;

        private readonly Dictionary<BoneRegion, float> regionStrengthCache = new Dictionary<BoneRegion, float>();
        private readonly Dictionary<BoneRegion, float> regionStateCache = new Dictionary<BoneRegion, float>();
        private readonly Dictionary<ContactPoint, float> contactTimers = new Dictionary<ContactPoint, float>();
        private readonly List<Rigidbody2D> puppetBodies = new List<Rigidbody2D>();
        private readonly Dictionary<Rigidbody2D, Vector2> previousVelocities = new Dictionary<Rigidbody2D, Vector2>();
        private readonly Dictionary<Transform, Vector2> previousMasterPositions = new Dictionary<Transform, Vector2>();
        private readonly Dictionary<Transform, Vector2> masterLinearVelocities = new Dictionary<Transform, Vector2>();
        private readonly Dictionary<Rigidbody2D, float> previousTargetAngles = new Dictionary<Rigidbody2D, float>();
        private readonly List<Transform> masterVelocityScratch = new List<Transform>();
        private readonly List<Transform> masterCleanupScratch = new List<Transform>();
        private readonly List<Rigidbody2D> targetCleanupScratch = new List<Rigidbody2D>();

        private BalanceState currentState = BalanceState.Normal;
        private float stateTimer;
        private float stableTimer;
        private float impactTimer;
        private bool bodiesDirty = true;
        private bool metricsInitialized;
        private bool autoPopulating;

        private Vector2 currentCenterOfMass;
        private Vector2 currentSupportCenter;
        private float currentPelvisHeight;
        private float currentTorsoTilt;

        private bool pelvisLow;
        private bool tiltExceeded;
        private bool comOutsideBase;
        private int groundedContacts;

        private void Awake()
        {
            if (MasterRoot == null)
            {
                MasterRoot = transform;
            }

            if (RootTarget == null)
            {
                RootTarget = transform;
            }

            if (PuppetRoot == null)
            {
                PuppetRoot = FindLikelyPuppetRoot();
            }

            if (AutoPopulateOnStart && ShouldAutoPopulate())
            {
                AutoPopulateLinksInternal(false);
            }

            RefreshBodiesCache();
            AutoAssignPelvis();
        }

        private void OnValidate()
        {
            if (autoPopulating)
            {
                return;
            }

            if (MasterRoot == null)
            {
                MasterRoot = transform;
            }

            if (RootTarget == null)
            {
                RootTarget = transform;
            }

            if (PuppetRoot == null)
            {
                PuppetRoot = FindLikelyPuppetRoot();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && AutoPopulateOnStart && ShouldAutoPopulate())
            {
                AutoPopulateLinksInternal(true);
            }
#endif

            bodiesDirty = true;
            AutoAssignPelvis();
        }

        [ContextMenu("Auto Populate Links")]
        public void AutoPopulateLinksContextMenu()
        {
            AutoPopulateLinksInternal(true);
        }

        private void FixedUpdate()
        {
            if (Links == null || Links.Count == 0)
            {
                return;
            }

            RefreshBodiesCache();
            float dt = Time.fixedDeltaTime;

            UpdateContacts(dt);
            UpdateImpacts(dt);
            UpdateMetrics(dt);
            UpdateBalanceState(dt);

            float stateStrength = ResolveStateStrengthScale();
            float clampedStrength = Mathf.Max(GlobalStrength, 0f) * stateStrength;

            UpdateMasterTargetVelocities(dt);
            ApplyBoneForces(clampedStrength);
            float rootScale = ResolveRootStrengthScale();
            ApplyRootAnchor(clampedStrength * rootScale);
        }

        private void LateUpdate()
        {
            regionStrengthCache.Clear();
            regionStateCache.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            float gizmoScale = Mathf.Max(Debug.GizmoScale, 0.01f);

            if (Debug.DrawTargets && Links != null)
            {
                foreach (BoneLink link in Links)
                {
                    if (link == null)
                    {
                        continue;
                    }

                    Vector3 puppetPosition = link.Puppet != null ? (Vector3)link.Puppet.position : Vector3.zero;
                    Vector3 masterPosition = link.Master != null ? link.Master.position : puppetPosition;

                    Gizmos.color = Debug.PuppetColor;
                    if (link.Puppet != null)
                    {
                        Gizmos.DrawSphere(puppetPosition, gizmoScale * 0.75f);
                    }

                    Gizmos.color = Debug.TargetColor;
                    if (link.Master != null)
                    {
                        Gizmos.DrawSphere(masterPosition, gizmoScale);
                    }

                    Gizmos.DrawLine(puppetPosition, masterPosition);
                }
            }

            if (Debug.DrawCenterOfMass)
            {
                Vector2 com = Application.isPlaying ? currentCenterOfMass : EstimateCenterOfMassEditor();
                Vector2 support = Application.isPlaying ? currentSupportCenter : EstimateSupportCenterEditor();

                Gizmos.color = Debug.ComColor;
                Gizmos.DrawSphere(new Vector3(com.x, com.y, 0f), gizmoScale * 1.4f);

                Gizmos.color = Debug.SupportColor;
                Gizmos.DrawSphere(new Vector3(support.x, support.y, 0f), gizmoScale);
                Gizmos.DrawLine(new Vector3(support.x, support.y, 0f), new Vector3(com.x, com.y, 0f));
            }
        }

        private void ApplyBoneForces(float globalStrength)
        {
            if (globalStrength <= 0f)
            {
                return;
            }

            float positionDeadZoneSq = Mathf.Max(PositionDeadZone * PositionDeadZone, 0f);
            float positionVelocityDeadZoneSq = Mathf.Max(PositionVelocityDeadZone * PositionVelocityDeadZone, 0f);
            float rotationDeadZoneAbs = Mathf.Max(RotationDeadZone, 0f);
            float rotationVelocityDeadZoneAbs = Mathf.Max(RotationVelocityDeadZone, 0f);

            HashSet<Rigidbody2D> activeBodies = null;

            foreach (BoneLink link in Links)
            {
                if (link == null || link.Master == null || link.Puppet == null)
                {
                    continue;
                }

                Rigidbody2D puppetBody = link.Puppet;
                float linkStrength = Mathf.Max(link.Strength, 0f) * globalStrength;
                if (linkStrength <= 0f)
                {
                    continue;
                }

                linkStrength *= ResolveDynamicMultiplier(link.Region);
                if (linkStrength <= 0f)
                {
                    continue;
                }

                activeBodies ??= new HashSet<Rigidbody2D>();
                activeBodies.Add(puppetBody);

                if (link.EnablePosition)
                {
                    Vector2 targetPosition = link.Master.position;
                    Vector2 currentPosition = puppetBody.position;
                    Vector2 currentVelocity = puppetBody.linearVelocity;
                    Vector2 masterVelocity = UseMasterVelocityFeedForward ? GetMasterLinearVelocity(link.Master) : Vector2.zero;
                    Vector2 relativeVelocity = currentVelocity - masterVelocity;
                    Vector2 positionError = targetPosition - currentPosition;

                    bool inPositionDeadZone = positionError.sqrMagnitude <= positionDeadZoneSq;
                    bool inVelocityDeadZone = relativeVelocity.sqrMagnitude <= positionVelocityDeadZoneSq;
                    if (!(inPositionDeadZone && inVelocityDeadZone))
                    {
                        float scaledStiffness = link.PositionStiffness * linkStrength;
                        float scaledDamping = ComputeScaledDamping(link.PositionStiffness, link.PositionDamping, linkStrength, puppetBody.mass);

                        Vector2 force = positionError * scaledStiffness - relativeVelocity * scaledDamping;
                        float maxForce = Mathf.Max(scaledStiffness, 0f) * Mathf.Max(PositionForceClampScale, 0f);
                        if (maxForce > 0f)
                        {
                            force = Vector2.ClampMagnitude(force, maxForce);
                        }
                        puppetBody.AddForce(force);
                    }
                }

                if (link.EnableRotation)
                {
                    float targetAngle = ComputeTargetAngle(link);
                    float currentAngle = puppetBody.rotation;
                    float angularError = Mathf.DeltaAngle(currentAngle, targetAngle);

                    float targetAngularVelocity = UseMasterAngularVelocityFeedForward ? GetTargetAngularVelocity(link, targetAngle) : 0f;
                    if (!UseMasterAngularVelocityFeedForward)
                    {
                        CacheTargetAngle(link, targetAngle);
                    }

                    float angularVelocity = puppetBody.angularVelocity;
                    float relativeAngularVelocity = angularVelocity - targetAngularVelocity;

                    bool inAngleDeadZone = Mathf.Abs(angularError) <= rotationDeadZoneAbs;
                    bool inAngularVelocityDeadZone = Mathf.Abs(relativeAngularVelocity) <= rotationVelocityDeadZoneAbs;
                    if (!(inAngleDeadZone && inAngularVelocityDeadZone))
                    {
                        float scaledStiffness = link.RotationStiffness * linkStrength;
                        float scaledDamping = ComputeScaledDamping(link.RotationStiffness, link.RotationDamping, linkStrength, puppetBody.inertia);

                        float torque = angularError * scaledStiffness - relativeAngularVelocity * scaledDamping;
                        float maxTorque = Mathf.Max(scaledStiffness, 0f) * Mathf.Max(RotationTorqueClampScale, 0f);
                        if (maxTorque > 0f)
                        {
                            torque = Mathf.Clamp(torque, -maxTorque, maxTorque);
                        }
                        puppetBody.AddTorque(torque);
                    }
                }
            }

            if (activeBodies != null)
            {
                CleanupTargetAngleCache(activeBodies);
            }
            else
            {
                previousTargetAngles.Clear();
            }
        }

        private void ApplyRootAnchor(float strengthScale)
        {
            if (!AnchorRoot || RootBody == null)
            {
                return;
            }

            float strength = Mathf.Max(RootStrength, 0f) * Mathf.Max(strengthScale, 0f);
            if (strength <= 0f)
            {
                return;
            }

            Transform targetTransform = RootTarget != null ? RootTarget : transform;

            Vector2 targetPosition = targetTransform.position;
            Vector2 currentPosition = RootBody.position;
            Vector2 masterVelocity = UseMasterVelocityFeedForward ? GetMasterLinearVelocity(targetTransform) : Vector2.zero;
            Vector2 currentVelocity = RootBody.linearVelocity;
            Vector2 relativeVelocity = currentVelocity - masterVelocity;
            Vector2 positionError = targetPosition - currentPosition;

            bool inPositionDeadZone = positionError.sqrMagnitude <= Mathf.Max(PositionDeadZone * PositionDeadZone, 0f);
            bool inVelocityDeadZone = relativeVelocity.sqrMagnitude <= Mathf.Max(PositionVelocityDeadZone * PositionVelocityDeadZone, 0f);
            if (inPositionDeadZone && inVelocityDeadZone)
            {
                return;
            }

            float scaledStiffness = RootStiffness * strength;
            float scaledDamping = ComputeScaledDamping(RootStiffness, RootDamping, strength, RootBody.mass);

            Vector2 force = positionError * scaledStiffness - relativeVelocity * scaledDamping;
            RootBody.AddForce(force);
        }

        private float ResolveDynamicMultiplier(BoneRegion region)
        {
            float multiplier = ResolveRegionMultiplier(region);
            multiplier *= ResolveStateRegionMultiplier(region);
            multiplier *= ResolveContactMultiplier(region);
            return multiplier;
        }

        private void UpdateMasterTargetVelocities(float dt)
        {
            masterVelocityScratch.Clear();

            if (Links != null)
            {
                for (int i = 0; i < Links.Count; i++)
                {
                    BoneLink link = Links[i];
                    if (link == null || link.Master == null)
                    {
                        continue;
                    }

                    if (!masterVelocityScratch.Contains(link.Master))
                    {
                        masterVelocityScratch.Add(link.Master);
                        UpdateMasterTransform(link.Master, dt);
                    }
                }
            }

            Transform rootTarget = RootTarget != null ? RootTarget : transform;
            if (rootTarget != null && !masterVelocityScratch.Contains(rootTarget))
            {
                masterVelocityScratch.Add(rootTarget);
                UpdateMasterTransform(rootTarget, dt);
            }

            CleanupUnusedMasterEntries();
        }

        private void UpdateMasterTransform(Transform target, float dt)
        {
            Vector2 currentPosition = target.position;
            if (dt > Mathf.Epsilon && previousMasterPositions.TryGetValue(target, out Vector2 previousPosition))
            {
                masterLinearVelocities[target] = (currentPosition - previousPosition) / dt;
            }
            else
            {
                masterLinearVelocities[target] = Vector2.zero;
            }

            previousMasterPositions[target] = currentPosition;
        }

        private void CleanupUnusedMasterEntries()
        {
            masterCleanupScratch.Clear();

            foreach (KeyValuePair<Transform, Vector2> entry in masterLinearVelocities)
            {
                if (!masterVelocityScratch.Contains(entry.Key))
                {
                    masterCleanupScratch.Add(entry.Key);
                }
            }

            for (int i = 0; i < masterCleanupScratch.Count; i++)
            {
                Transform key = masterCleanupScratch[i];
                masterLinearVelocities.Remove(key);
                previousMasterPositions.Remove(key);
            }

            masterCleanupScratch.Clear();
        }

        private Vector2 GetMasterLinearVelocity(Transform target)
        {
            if (target == null)
            {
                return Vector2.zero;
            }

            if (masterLinearVelocities.TryGetValue(target, out Vector2 velocity))
            {
                return velocity;
            }

            return Vector2.zero;
        }

        private static float ComputeScaledDamping(float baseStiffness, float dampingSetting, float strength, float massOrInertia)
        {
            float clampedStrength = Mathf.Max(strength, 0f);
            float clampedStiffness = Mathf.Max(baseStiffness, 0f);
            float clampedMass = Mathf.Max(massOrInertia, 0.0001f);

            if (clampedStrength <= 0f || clampedStiffness <= 0f)
            {
                return 0f;
            }

            float baseCritical = 2f * Mathf.Sqrt(clampedStiffness * clampedMass);
            float ratio = baseCritical > Mathf.Epsilon ? Mathf.Max(dampingSetting, 0f) / baseCritical : 1f;

            float scaledStiffness = clampedStiffness * clampedStrength;
            float scaledCritical = 2f * Mathf.Sqrt(scaledStiffness * clampedMass);
            return scaledCritical * Mathf.Max(ratio, 0f);
        }

        private float ComputeTargetAngle(BoneLink link)
        {
            if (link.Master == null)
            {
                return 0f;
            }

            if (!link.UseLocalRotation)
            {
                return link.Master.eulerAngles.z;
            }

            Transform parent = link.Puppet != null ? link.Puppet.transform.parent : null;
            if (!TryGetParentRotation(parent, out float parentWorldZ))
            {
                return link.Master.eulerAngles.z;
            }

            float masterLocalZ = link.Master.localEulerAngles.z;
            if (masterLocalZ > 180f)
            {
                masterLocalZ -= 360f;
            }

            return parentWorldZ + masterLocalZ;
        }

        private bool TryGetParentRotation(Transform parent, out float angle)
        {
            angle = 0f;

            if (parent == null)
            {
                return false;
            }

            Rigidbody2D parentBody = parent.GetComponent<Rigidbody2D>();
            if (parentBody != null)
            {
                if (!IsBodyDriven(parentBody))
                {
                    return false;
                }

                angle = parentBody.rotation;
                return true;
            }

            if (parent == transform || (PuppetRoot != null && parent == PuppetRoot))
            {
                angle = parent.eulerAngles.z;
                return true;
            }

            return false;
        }

        private bool IsBodyDriven(Rigidbody2D body)
        {
            if (body == null || Links == null)
            {
                return false;
            }

            for (int i = 0; i < Links.Count; i++)
            {
                BoneLink link = Links[i];
                if (link == null || link.Puppet != body)
                {
                    continue;
                }

                if (!link.EnableRotation)
                {
                    return false;
                }

                return link.Strength > 0f;
            }

            return false;
        }

        private float GetTargetAngularVelocity(BoneLink link, float currentTargetAngle)
        {
            Rigidbody2D puppetBody = link.Puppet;
            if (puppetBody == null)
            {
                return 0f;
            }

            float velocity = 0f;
            float dt = Time.fixedDeltaTime;
            if (dt > Mathf.Epsilon && previousTargetAngles.TryGetValue(puppetBody, out float previousAngle))
            {
                float delta = Mathf.DeltaAngle(previousAngle, currentTargetAngle);
                velocity = delta / dt;
            }

            previousTargetAngles[puppetBody] = currentTargetAngle;
            return velocity;
        }

        private void CacheTargetAngle(BoneLink link, float targetAngle)
        {
            if (link?.Puppet == null)
            {
                return;
            }

            previousTargetAngles[link.Puppet] = targetAngle;
        }

        private void CleanupTargetAngleCache(HashSet<Rigidbody2D> activeBodies)
        {
            if (previousTargetAngles.Count == 0)
            {
                return;
            }

            targetCleanupScratch.Clear();

            foreach (KeyValuePair<Rigidbody2D, float> entry in previousTargetAngles)
            {
                if (!activeBodies.Contains(entry.Key))
                {
                    targetCleanupScratch.Add(entry.Key);
                }
            }

            for (int i = 0; i < targetCleanupScratch.Count; i++)
            {
                previousTargetAngles.Remove(targetCleanupScratch[i]);
            }

            targetCleanupScratch.Clear();
        }

        private float ResolveRegionMultiplier(BoneRegion region)
        {
            if (RegionMultipliers == null || RegionMultipliers.Count == 0)
            {
                return 1f;
            }

            if (regionStrengthCache.TryGetValue(region, out float cachedMultiplier))
            {
                return cachedMultiplier;
            }

            foreach (RegionStrength entry in RegionMultipliers)
            {
                if (entry == null || entry.Region != region)
                {
                    continue;
                }

                float value = Mathf.Max(entry.Multiplier, 0f);
                regionStrengthCache[region] = value;
                return value;
            }

            regionStrengthCache[region] = 1f;
            return 1f;
        }

        private float ResolveStateRegionMultiplier(BoneRegion region)
        {
            if (StateRegionMultipliers == null || StateRegionMultipliers.Count == 0)
            {
                return 1f;
            }

            if (regionStateCache.TryGetValue(region, out float cachedMultiplier))
            {
                return cachedMultiplier;
            }

            foreach (StateRegionMultiplier entry in StateRegionMultipliers)
            {
                if (entry == null || entry.Region != region)
                {
                    continue;
                }

                float value = currentState switch
                {
                    BalanceState.Unpinned => entry.Unpinned,
                    BalanceState.Recovery => entry.Recovery,
                    _ => entry.Normal
                };

                value = Mathf.Max(value, 0f);
                regionStateCache[region] = value;
                return value;
            }

            regionStateCache[region] = 1f;
            return 1f;
        }

        private float ResolveContactMultiplier(BoneRegion region)
        {
            if (Contacts == null || Contacts.Count == 0)
            {
                return 1f;
            }

            bool grounded = false;
            float stanceMultiplier = 1f;
            float slipMultiplier = 1f;

            foreach (ContactPoint contact in Contacts)
            {
                if (contact == null)
                {
                    continue;
                }

                if (contact.IsGrounded)
                {
                    grounded = true;
                    stanceMultiplier = Mathf.Max(stanceMultiplier, Mathf.Max(contact.StanceStrength, 0f));
                }
                else
                {
                    slipMultiplier = Mathf.Min(slipMultiplier, Mathf.Clamp(contact.SlipStrength, 0f, 1f));
                }
            }

            if (grounded && (region == BoneRegion.Hips || region == BoneRegion.Legs || region == BoneRegion.Root))
            {
                return stanceMultiplier;
            }

            if (!grounded && (region == BoneRegion.Hips || region == BoneRegion.Legs))
            {
                return Mathf.Clamp(slipMultiplier, 0f, 1f);
            }

            return 1f;
        }

        private void RefreshBodiesCache()
        {
            if (!bodiesDirty && puppetBodies.Count > 0)
            {
                bool missing = false;
                for (int i = 0; i < puppetBodies.Count; i++)
                {
                    if (puppetBodies[i] == null)
                    {
                        missing = true;
                        break;
                    }
                }

                if (!missing)
                {
                    return;
                }
            }

            bodiesDirty = false;
            puppetBodies.Clear();

            HashSet<Rigidbody2D> seen = new HashSet<Rigidbody2D>();
            if (Links != null)
            {
                foreach (BoneLink link in Links)
                {
                    if (link == null || link.Puppet == null)
                    {
                        continue;
                    }

                    if (seen.Add(link.Puppet))
                    {
                        puppetBodies.Add(link.Puppet);
                    }
                }
            }

            if (RootBody != null && seen.Add(RootBody))
            {
                puppetBodies.Add(RootBody);
            }

            List<Rigidbody2D> toRemove = new List<Rigidbody2D>();
            foreach (KeyValuePair<Rigidbody2D, Vector2> entry in previousVelocities)
            {
                if (!seen.Contains(entry.Key))
                {
                    toRemove.Add(entry.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                previousVelocities.Remove(toRemove[i]);
            }
        }

        private void AutoAssignPelvis()
        {
            if (Links == null)
            {
                return;
            }

            if (PelvisBody == null && AutoAssignPelvisFromLinks)
            {
                PelvisBody = FindFirstBodyForRegion(BoneRegion.Hips);
                if (PelvisBody == null)
                {
                    PelvisBody = FindFirstBodyForRegion(BoneRegion.Torso);
                }
            }

            if ((RootTarget == null || RootTarget == transform) && PelvisBody != null)
            {
                RootTarget = PelvisBody.transform;
            }
            else if (RootTarget == null && RootBody != null)
            {
                RootTarget = RootBody.transform;
            }
        }

        private Rigidbody2D FindFirstBodyForRegion(BoneRegion region)
        {
            if (Links == null)
            {
                return null;
            }

            foreach (BoneLink link in Links)
            {
                if (link == null)
                {
                    continue;
                }

                if (link.Region == region && link.Puppet != null)
                {
                    return link.Puppet;
                }
            }

            return null;
        }

        private void UpdateContacts(float dt)
        {
            groundedContacts = 0;

            if (Contacts == null)
            {
                return;
            }

            foreach (ContactPoint contact in Contacts)
            {
                if (contact == null)
                {
                    continue;
                }

                if (!contactTimers.ContainsKey(contact))
                {
                    contactTimers[contact] = 0f;
                }

                bool grounded = contact.Sensor != null && contact.Sensor.IsTouchingLayers(GroundLayers);
                if (grounded)
                {
                    contactTimers[contact] = ContactStickTime;
                    contact.IsGrounded = true;
                }
                else
                {
                    float timer = contactTimers[contact] - dt;
                    if (timer <= 0f)
                    {
                        contact.IsGrounded = false;
                        contactTimers[contact] = 0f;
                    }
                    else
                    {
                        contactTimers[contact] = timer;
                        contact.IsGrounded = true;
                    }
                }

                if (contact.IsGrounded)
                {
                    groundedContacts++;
                }
            }
        }

        private void UpdateImpacts(float dt)
        {
            impactTimer = Mathf.Max(impactTimer - dt, 0f);

            for (int i = 0; i < puppetBodies.Count; i++)
            {
                Rigidbody2D body = puppetBodies[i];
                if (body == null)
                {
                    continue;
                }

                Vector2 velocity = body.linearVelocity;
                if (previousVelocities.TryGetValue(body, out Vector2 previous))
                {
                    float delta = (velocity - previous).magnitude;
                    if (delta >= ImpactVelocityThreshold)
                    {
                        impactTimer = ImpactCooldown;
                    }
                }

                previousVelocities[body] = velocity;
            }
        }

        private void UpdateMetrics(float dt)
        {
            Vector2 rawCom = ComputeCenterOfMassInternal();
            Vector2 rawSupport = ComputeSupportCenterInternal();
            float rawHeight = ComputePelvisHeightInternal();
            float rawTilt = ComputeTorsoTiltInternal();

            if (!metricsInitialized)
            {
                currentCenterOfMass = rawCom;
                currentSupportCenter = rawSupport;
                currentPelvisHeight = rawHeight;
                currentTorsoTilt = rawTilt;
                metricsInitialized = true;
            }
            else
            {
                currentCenterOfMass = SmoothVector(currentCenterOfMass, rawCom, ComSmoothing, dt);
                currentSupportCenter = SmoothVector(currentSupportCenter, rawSupport, SupportSmoothing, dt);
                currentPelvisHeight = SmoothScalar(currentPelvisHeight, rawHeight, HeightSmoothing, dt);
                currentTorsoTilt = SmoothScalar(currentTorsoTilt, rawTilt, TiltSmoothing, dt);
            }

            pelvisLow = currentPelvisHeight < DesiredPelvisHeight - PelvisHeightTolerance;

            tiltExceeded = currentTorsoTilt > MaxTorsoTilt;

            bool hasSupport = groundedContacts > 0;
            float comOffset = Mathf.Abs(currentCenterOfMass.x - currentSupportCenter.x);
            comOutsideBase = hasSupport ? comOffset > ComDistanceThreshold : true;
        }

        private void UpdateBalanceState(float dt)
        {
            stateTimer += dt;
            bool impact = impactTimer > 0f;
            bool unstable = tiltExceeded || comOutsideBase || pelvisLow || impact;

            switch (currentState)
            {
                case BalanceState.Normal:
                    if (unstable)
                    {
                        EnterState(BalanceState.Unpinned);
                    }

                    break;

                case BalanceState.Unpinned:
                    if (!unstable)
                    {
                        stableTimer += dt;
                        if (stableTimer >= StableDuration)
                        {
                            EnterState(BalanceState.Recovery);
                        }
                    }
                    else
                    {
                        stableTimer = 0f;
                    }

                    break;

                case BalanceState.Recovery:
                    if (unstable)
                    {
                        EnterState(BalanceState.Unpinned);
                        break;
                    }

                    if (stateTimer >= RecoveryDuration)
                    {
                        EnterState(BalanceState.Normal);
                    }

                    break;
            }
        }

        private void EnterState(BalanceState nextState)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            stateTimer = 0f;
            stableTimer = 0f;
            regionStateCache.Clear();
        }

        private float ResolveStateStrengthScale()
        {
            return currentState switch
            {
                BalanceState.Unpinned => Mathf.Max(UnpinnedStrengthScale, 0f),
                BalanceState.Recovery => Mathf.Max(RecoveryStrengthScale, 0f),
                _ => Mathf.Max(NormalStrengthScale, 0f)
            };
        }

        private float ResolveRootStrengthScale()
        {
            return currentState switch
            {
                BalanceState.Unpinned => Mathf.Max(UnpinnedRootScale, 0f),
                BalanceState.Recovery => Mathf.Max(RecoveryRootScale, 0f),
                _ => Mathf.Max(NormalRootScale, 0f)
            };
        }

        private Vector2 ComputeCenterOfMassInternal()
        {
            if (CenterOfMassOverride != null)
            {
                return CenterOfMassOverride.position;
            }

            float totalMass = 0f;
            Vector2 sum = Vector2.zero;

            for (int i = 0; i < puppetBodies.Count; i++)
            {
                Rigidbody2D body = puppetBodies[i];
                if (body == null)
                {
                    continue;
                }

                float mass = body.mass;
                totalMass += mass;
                sum += body.worldCenterOfMass * mass;
            }

            if (totalMass > Mathf.Epsilon)
            {
                return sum / totalMass;
            }

            if (RootBody != null)
            {
                return RootBody.worldCenterOfMass;
            }

            return transform.position;
        }

        private Vector2 ComputeSupportCenterInternal()
        {
            if (Contacts != null)
            {
                Vector2 sum = Vector2.zero;
                int count = 0;

                foreach (ContactPoint contact in Contacts)
                {
                    if (contact == null || !contact.IsGrounded)
                    {
                        continue;
                    }

                    if (contact.Body != null)
                    {
                        sum += contact.Body.worldCenterOfMass;
                        count++;
                    }
                    else if (contact.Sensor != null)
                    {
                        sum += Project2D(contact.Sensor.bounds.center);
                        count++;
                    }
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            if (RootBody != null)
            {
                return RootBody.worldCenterOfMass;
            }

            return transform.position;
        }

        private float ComputePelvisHeightInternal()
        {
            if (PelvisBody != null)
            {
                return PelvisBody.worldCenterOfMass.y;
            }

            if (RootBody != null)
            {
                return RootBody.worldCenterOfMass.y;
            }

            return transform.position.y;
        }

        private float ComputeTorsoTiltInternal()
        {
            Transform reference = TorsoReference != null ? TorsoReference : (PelvisBody != null ? PelvisBody.transform : transform);
            Vector2 up = new Vector2(reference.up.x, reference.up.y);
            if (up.sqrMagnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            up.Normalize();
            return Vector2.Angle(up, Vector2.up);
        }

        private Vector2 SmoothVector(Vector2 current, Vector2 target, float smoothing, float dt)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            float factor = 1f - Mathf.Exp(-dt / Mathf.Max(smoothing, 0.0001f));
            return current + (target - current) * factor;
        }

        private float SmoothScalar(float current, float target, float smoothing, float dt)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            float factor = 1f - Mathf.Exp(-dt / Mathf.Max(smoothing, 0.0001f));
            return current + (target - current) * factor;
        }

        private Vector2 EstimateCenterOfMassEditor()
        {
            if (Links == null || Links.Count == 0)
            {
                return transform.position;
            }

            float totalMass = 0f;
            Vector2 sum = Vector2.zero;

            foreach (BoneLink link in Links)
            {
                if (link == null || link.Puppet == null)
                {
                    continue;
                }

                float mass = link.Puppet.mass;
                totalMass += mass;
                sum += (Vector2)link.Puppet.transform.position * mass;
            }

            if (totalMass > Mathf.Epsilon)
            {
                return sum / totalMass;
            }

            return transform.position;
        }

        private Vector2 EstimateSupportCenterEditor()
        {
            if (Contacts != null)
            {
                Vector2 sum = Vector2.zero;
                int count = 0;

                foreach (ContactPoint contact in Contacts)
                {
                    if (contact == null)
                    {
                        continue;
                    }

                    if (contact.Body != null)
                    {
                        sum += (Vector2)contact.Body.transform.position;
                        count++;
                    }
                    else if (contact.Sensor != null)
                    {
                        sum += Project2D(contact.Sensor.bounds.center);
                        count++;
                    }
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            return transform.position;
        }

        private static Vector2 Project2D(Vector3 value)
        {
            return new Vector2(value.x, value.y);
        }

        private bool ShouldAutoPopulate()
        {
            if (PuppetRoot == null)
            {
                return false;
            }

            if (Links == null || Links.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < Links.Count; i++)
            {
                BoneLink link = Links[i];
                if (link == null || link.Puppet == null || link.Master == null)
                {
                    return true;
                }

                if ((link.Puppet != null && !link.Puppet.gameObject.scene.IsValid()) ||
                    (link.Master != null && !link.Master.gameObject.scene.IsValid()))
                {
                    return true;
                }
            }

            Rigidbody2D[] puppetBodies = PuppetRoot.GetComponentsInChildren<Rigidbody2D>(true);
            if (puppetBodies == null || puppetBodies.Length == 0)
            {
                return false;
            }

            HashSet<Rigidbody2D> linkedBodies = new HashSet<Rigidbody2D>();
            for (int i = 0; i < Links.Count; i++)
            {
                BoneLink link = Links[i];
                if (link != null && link.Puppet != null)
                {
                    linkedBodies.Add(link.Puppet);
                }
            }

            for (int i = 0; i < puppetBodies.Length; i++)
            {
                if (puppetBodies[i] != null && !linkedBodies.Contains(puppetBodies[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AutoPopulateLinksInternal(bool logWarnings)
        {
            if (PuppetRoot == null)
            {
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("MasterPuppetLink: PuppetRoot is not assigned, cannot auto populate.", this);
                }

                return false;
            }

            Transform actualMasterRoot = MasterRoot != null ? MasterRoot : transform;

            Rigidbody2D[] puppetBodies = PuppetRoot.GetComponentsInChildren<Rigidbody2D>(true);
            if (puppetBodies == null || puppetBodies.Length == 0)
            {
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("MasterPuppetLink: No Rigidbody2D components found under PuppetRoot.", this);
                }

                return false;
            }

            Dictionary<string, Transform> masterMap = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            Transform[] masterTransforms = actualMasterRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < masterTransforms.Length; i++)
            {
                Transform current = masterTransforms[i];
                if (!masterMap.ContainsKey(current.name))
                {
                    masterMap.Add(current.name, current);
                }
            }

            if (masterMap.Count == 0)
            {
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("MasterPuppetLink: No master transforms found to match against.", this);
                }

                return false;
            }

            List<BoneLink> newLinks = new List<BoneLink>(puppetBodies.Length);
            autoPopulating = true;

            try
            {
                for (int i = 0; i < puppetBodies.Length; i++)
                {
                    Rigidbody2D body = puppetBodies[i];
                    Transform puppetTransform = body.transform;
                    if (!masterMap.TryGetValue(puppetTransform.name, out Transform masterTransform))
                    {
                        continue;
                    }

                    BoneLink existing = FindExistingLinkForBody(body);
                    bool hadExisting = existing != null;
                    BoneLink link = existing ?? new BoneLink();
                    link.Puppet = body;
                    link.Master = masterTransform;

                    if (!hadExisting)
                    {
                        link.Region = GuessRegionFromName(puppetTransform.name);
                        ApplyRegionDefaults(link);
                    }

                    newLinks.Add(link);
                }
            }
            finally
            {
                autoPopulating = false;
            }

            if (newLinks.Count == 0)
            {
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("MasterPuppetLink: No matching master transforms were found for the puppet bodies.", this);
                }

                return false;
            }

            newLinks.Sort(CompareLinks);
            Links = newLinks;
            bodiesDirty = true;
            AutoAssignPelvis();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif

            return true;
        }

        private BoneLink FindExistingLinkForBody(Rigidbody2D body)
        {
            if (Links == null)
            {
                return null;
            }

            for (int i = 0; i < Links.Count; i++)
            {
                BoneLink link = Links[i];
                if (link != null && link.Puppet == body)
                {
                    return link;
                }
            }

            return null;
        }

        private static int CompareLinks(BoneLink a, BoneLink b)
        {
            int regionComparison = GetRegionOrder(a?.Region ?? BoneRegion.Torso).CompareTo(GetRegionOrder(b?.Region ?? BoneRegion.Torso));
            if (regionComparison != 0)
            {
                return regionComparison;
            }

            string aName = a?.Puppet != null ? a.Puppet.name : string.Empty;
            string bName = b?.Puppet != null ? b.Puppet.name : string.Empty;
            return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
        }

        private static int GetRegionOrder(BoneRegion region)
        {
            switch (region)
            {
                case BoneRegion.Root:
                    return 0;
                case BoneRegion.Torso:
                    return 1;
                case BoneRegion.Hips:
                    return 2;
                case BoneRegion.Arms:
                    return 3;
                case BoneRegion.Legs:
                    return 4;
                case BoneRegion.Head:
                    return 5;
                default:
                    return 6;
            }
        }

        private static BoneRegion GuessRegionFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BoneRegion.Torso;
            }

            string lower = name.ToLowerInvariant();
            if (lower.Contains("head") || lower.Contains("hat"))
            {
                return BoneRegion.Head;
            }

            if (lower.Contains("arm") || lower.Contains("hand"))
            {
                return BoneRegion.Arms;
            }

            if (lower.Contains("leg") || lower.Contains("foot"))
            {
                return BoneRegion.Legs;
            }

            if (lower.Contains("hip") || lower.Contains("pelvis"))
            {
                return BoneRegion.Hips;
            }

            if (lower.Contains("bodylow"))
            {
                return BoneRegion.Root;
            }

            if (lower.Contains("torso") || lower.Contains("body"))
            {
                return BoneRegion.Torso;
            }

            return BoneRegion.Torso;
        }

        private Transform FindLikelyPuppetRoot()
        {
            string expectedName = transform.name.Replace("Master", "Puppet");
            string alternateName = transform.name + "_Puppet";

            Transform parent = transform.parent;
            if (parent != null)
            {
                Transform sibling = parent.Find(expectedName);
                if (sibling != null)
                {
                    return sibling;
                }

                sibling = parent.Find(alternateName);
                if (sibling != null)
                {
                    return sibling;
                }
            }

            if (!gameObject.scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject candidate = roots[i];
                if (candidate == gameObject)
                {
                    continue;
                }

                if (candidate.name.Equals(expectedName, System.StringComparison.OrdinalIgnoreCase) ||
                    candidate.name.Equals(alternateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.transform;
                }
            }

            return null;
        }

        private static void ApplyRegionDefaults(BoneLink link)
        {
            switch (link.Region)
            {
                case BoneRegion.Root:
                case BoneRegion.Hips:
                    link.EnablePosition = true;
                    link.EnableRotation = true;
                    link.UseLocalRotation = true;
                    link.PositionStiffness = 120f;
                    if (link.PositionDamping <= 26f)
                    {
                        link.PositionDamping = 40f;
                    }
                    link.PositionDamping = Mathf.Clamp(link.PositionDamping, 35f, 45f);
                    link.RotationStiffness = Mathf.Clamp(link.RotationStiffness, 180f, 280f);
                    if (link.RotationDamping <= 20f)
                    {
                        link.RotationDamping = 28f;
                    }
                    link.RotationDamping = Mathf.Clamp(link.RotationDamping, 25f, 32f);
                    if (link.Strength >= 0.99f)
                    {
                        link.Strength = 0.9f;
                    }
                    link.Strength = Mathf.Clamp(link.Strength, 0.8f, 1f);
                    break;
                case BoneRegion.Torso:
                    link.EnablePosition = true;
                    link.EnableRotation = true;
                    link.UseLocalRotation = true;
                    link.PositionStiffness = Mathf.Clamp(link.PositionStiffness, 100f, 150f);
                    if (link.PositionDamping <= 26f)
                    {
                        link.PositionDamping = 36f;
                    }
                    link.PositionDamping = Mathf.Clamp(link.PositionDamping, 32f, 42f);
                    link.RotationStiffness = Mathf.Clamp(link.RotationStiffness, 180f, 260f);
                    if (link.RotationDamping <= 20f)
                    {
                        link.RotationDamping = 27f;
                    }
                    link.RotationDamping = Mathf.Clamp(link.RotationDamping, 24f, 32f);
                    if (link.Strength >= 0.99f)
                    {
                        link.Strength = 0.85f;
                    }
                    link.Strength = Mathf.Clamp(link.Strength, 0.75f, 0.95f);
                    break;
                case BoneRegion.Legs:
                    link.EnablePosition = false;
                    link.EnableRotation = true;
                    link.UseLocalRotation = true;
                    link.RotationStiffness = Mathf.Clamp(link.RotationStiffness, 170f, 250f);
                    link.RotationDamping = Mathf.Clamp(link.RotationDamping, 18f, 26f);
                    if (link.Strength >= 0.99f)
                    {
                        link.Strength = 0.8f;
                    }
                    link.Strength = Mathf.Clamp(link.Strength, 0.7f, 0.95f);
                    break;
                case BoneRegion.Arms:
                case BoneRegion.Head:
                    link.EnablePosition = false;
                    link.EnableRotation = true;
                    link.UseLocalRotation = true;
                    link.RotationStiffness = Mathf.Clamp(link.RotationStiffness, 160f, 230f);
                    link.RotationDamping = Mathf.Clamp(link.RotationDamping, 18f, 26f);
                    if (link.Strength >= 0.99f)
                    {
                        link.Strength = 0.7f;
                    }
                    link.Strength = Mathf.Clamp(link.Strength, 0.6f, 0.85f);
                    break;
                default:
                    link.EnablePosition = false;
                    link.EnableRotation = true;
                    link.UseLocalRotation = true;
                    break;
            }
        }
    }
}

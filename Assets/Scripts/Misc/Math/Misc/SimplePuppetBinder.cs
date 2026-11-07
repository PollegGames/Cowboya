using System;
using System.Collections.Generic;
using UnityEngine;

namespace CowBoya.Robots
{
    /// <summary>
    /// Minimal master to puppet rotation binding for manually paired bones.
    /// Useful when selected puppet bones should follow an animated master without position binding.
    /// </summary>
    public class SimplePuppetBinder : MonoBehaviour
    {
        [Serializable]
        public class BonePair
        {
            public Transform Master;
            public Transform Puppet;
            [Tooltip("Optional cached Rigidbody2D reference on the puppet for smooth moves.")]
            public Rigidbody2D PuppetBody2D;
            [Tooltip("Overrides the global RotationSharpness for this bone. 0 = use the global value.")]
            public float SharpnessOverride;
            [Tooltip("Maximum rotation speed of this bone in degrees per second. 0 = unlimited. "
                + "Lower values soften the bone (for example legs, to damp the step).")]
            public float MaxAngularSpeed;

            [NonSerialized]
            internal Quaternion targetRotation;
            [NonSerialized]
            internal bool hasRotationTarget;
        }

        [Tooltip("Root transform used as the rotation reference for master bones.")]
        public Transform MasterRoot;

        [Tooltip("Root transform used as the rotation reference for puppet bones.")]
        public Transform PuppetRoot;

        [Tooltip("2D rotation sharpness. Use 0 or less for exact target rotation.")]
        public float RotationSharpness;

        [Tooltip("Manual list of master/puppet transform pairs whose rotations should stay aligned.")]
        public List<BonePair> Pairs = new List<BonePair>();

        [Header("Recovery after ragdoll")]
        [Tooltip("When the binder is re-enabled (end of faint, grab or ragdoll), its strength rises from 0 to 1 over this time.")]
        [Min(0f)] public float RecoveryDuration = 0.6f;
        [Tooltip("Maximum bone rotation speed (degrees/second) while recovering, so limbs never snap back to the pose.")]
        [Min(0f)] public float RecoveryMaxAngularSpeed = 360f;

        private float strength = 1f;
        private bool hasBeenEnabled;

        /// <summary>Current binding strength: 0 = limp, 1 = fully following the master.</summary>
        public float Strength => strength;

        /// <summary>True while the binder blends back in after a ragdoll.</summary>
        public bool IsRecovering => strength < 1f;

        /// <summary>
        /// Starts a progressive return to the animated pose instead of snapping the bones.
        /// </summary>
        public void BeginRecovery()
        {
            strength = RecoveryDuration > 0f ? 0f : 1f;
        }

        private void OnEnable()
        {
            // The first enable is the spawn: bones already match the pose. Later enables end a ragdoll.
            if (hasBeenEnabled)
            {
                BeginRecovery();
            }

            hasBeenEnabled = true;
        }

        private void Reset()
        {
            if (MasterRoot == null)
            {
                MasterRoot = transform;
            }

            if (PuppetRoot == null)
            {
                PuppetRoot = transform;
            }
        }

        private void Awake()
        {
            if (MasterRoot == null)
            {
                MasterRoot = transform;
            }
        }

        private void LateUpdate()
        {
            Transform masterRoot = MasterRoot != null ? MasterRoot : transform;
            Transform puppetRoot = PuppetRoot != null ? PuppetRoot : transform;
            Quaternion masterRootInverseRotation = Quaternion.Inverse(masterRoot.rotation);
            Quaternion puppetRootRotation = puppetRoot.rotation;

            for (int i = 0; i < Pairs.Count; i++)
            {
                BonePair pair = Pairs[i];
                if (pair == null || pair.Master == null || pair.Puppet == null)
                {
                    continue;
                }

                Rigidbody2D rb2D = pair.PuppetBody2D;
                if (rb2D == null)
                {
                    pair.Puppet.TryGetComponent(out rb2D);
                    pair.PuppetBody2D = rb2D;
                }

                Quaternion localMasterRotation = masterRootInverseRotation * pair.Master.rotation;
                Quaternion rotation = puppetRootRotation * localMasterRotation;
                // Rigs that include a Rigidbody need their targets deferred to FixedUpdate;
                // transform writes in LateUpdate only stick on bones without physics.
                if (rb2D != null)
                {
                    pair.targetRotation = rotation;
                    pair.hasRotationTarget = true;
                }
            }
        }

        private void FixedUpdate()
        {
            bool recovering = strength < 1f;
            if (recovering)
            {
                strength = Mathf.MoveTowards(strength, 1f, Time.fixedDeltaTime / Mathf.Max(RecoveryDuration, 0.0001f));
            }

            for (int i = 0; i < Pairs.Count; i++)
            {
                BonePair pair = Pairs[i];
                if (pair == null || pair.Puppet == null)
                {
                    continue;
                }

                Rigidbody2D rb2D = pair.PuppetBody2D;

                if (pair.hasRotationTarget)
                {
                    if (rb2D != null)
                    {
                        rb2D.MoveRotation(ComputeStepAngle(pair, rb2D.rotation, recovering));
                    }
                }
            }
        }

        /// <summary>
        /// Angle a bone moves to this physics step: sharpness smoothing, then the recovery blend,
        /// then the per-bone (or recovery) maximum angular speed.
        /// </summary>
        private float ComputeStepAngle(BonePair pair, float currentAngle, bool recovering)
        {
            float targetAngle = pair.targetRotation.eulerAngles.z;
            float sharpness = pair.SharpnessOverride > 0f ? pair.SharpnessOverride : RotationSharpness;
            if (sharpness > 0f)
            {
                float t = 1f - Mathf.Exp(-sharpness * Time.fixedDeltaTime);
                targetAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
            }

            float maxSpeed = pair.MaxAngularSpeed;
            if (recovering)
            {
                targetAngle = Mathf.LerpAngle(currentAngle, targetAngle, Mathf.Max(strength, 0.05f));
                if (RecoveryMaxAngularSpeed > 0f)
                {
                    maxSpeed = maxSpeed > 0f ? Mathf.Min(maxSpeed, RecoveryMaxAngularSpeed) : RecoveryMaxAngularSpeed;
                }
            }

            if (maxSpeed > 0f)
            {
                targetAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxSpeed * Time.fixedDeltaTime);
            }

            return targetAngle;
        }

        /// <summary>
        /// Clears every deferred physics rotation so a pooled puppet cannot apply a target from its previous use.
        /// </summary>
        public void ClearRotationTargets()
        {
            if (Pairs == null)
            {
                return;
            }

            for (int i = 0; i < Pairs.Count; i++)
            {
                BonePair pair = Pairs[i];
                if (pair == null)
                {
                    continue;
                }

                pair.targetRotation = Quaternion.identity;
                pair.hasRotationTarget = false;
            }
        }
    }
}

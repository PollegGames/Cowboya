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
                        float targetAngle = pair.targetRotation.eulerAngles.z;
                        if (RotationSharpness > 0f)
                        {
                            float t = 1f - Mathf.Exp(-RotationSharpness * Time.fixedDeltaTime);
                            targetAngle = Mathf.LerpAngle(rb2D.rotation, targetAngle, t);
                        }

                        rb2D.MoveRotation(targetAngle);
                    }
                }
            }
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

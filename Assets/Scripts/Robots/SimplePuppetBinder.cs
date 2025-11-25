using System;
using System.Collections.Generic;
using UnityEngine;

namespace CowBoya.Robots
{
    /// <summary>
    /// Minimal master to puppet pose binding that simply copies transforms.
    /// Useful when a physically simulated rig is not required.
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
            [Tooltip("Optional cached Rigidbody reference on the puppet for smooth moves.")]
            public Rigidbody PuppetBody3D;

            [NonSerialized]
            internal Vector3 targetPosition;
            [NonSerialized]
            internal Quaternion targetRotation;
            [NonSerialized]
            internal bool hasPositionTarget;
            [NonSerialized]
            internal bool hasRotationTarget;
        }

        [Tooltip("Root transform used to search for master bones when auto populating.")]
        public Transform MasterRoot;

        [Tooltip("Root transform used to search for puppet bones when auto populating.")]
        public Transform PuppetRoot;

        [Tooltip("Ordered list of master/puppet transform pairs to keep aligned.")]
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

                Rigidbody rb3D = pair.PuppetBody3D;
                if (rb3D == null)
                {
                    pair.Puppet.TryGetComponent(out rb3D);
                    pair.PuppetBody3D = rb3D;
                }

                pair.hasPositionTarget = false;
                pair.hasRotationTarget = false;

                Quaternion localMasterRotation = masterRootInverseRotation * pair.Master.rotation;
                Quaternion rotation = puppetRootRotation * localMasterRotation;
                // Rigs that include a Rigidbody need their targets deferred to FixedUpdate;
                // transform writes in LateUpdate only stick on bones without physics.
                if (rb2D != null || rb3D != null)
                {
                    pair.targetRotation = rotation;
                    pair.hasRotationTarget = true;
                }
                else
                {
                    pair.Puppet.rotation = rotation;
                }

                Vector3 localMasterPosition = masterRootInverseRotation * (pair.Master.position - masterRoot.position);
                Vector3 position = puppetRoot.position + puppetRootRotation * localMasterPosition;
                // Only puppets driven by a Rigidbody were drifting away from their masters,
                // because we used to skip copying the position for those; pure transform rigs
                // were unaffected. Storing a target for physics-driven bones keeps both paths
                // in sync.
                if (rb2D != null || rb3D != null)
                {
                    pair.targetPosition = position;
                    pair.hasPositionTarget = true;
                }
                else
                {
                    pair.Puppet.position = position;
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
                Rigidbody rb3D = pair.PuppetBody3D;

                if (pair.hasRotationTarget)
                {
                    if (rb2D != null)
                    {
                        rb2D.MoveRotation(pair.targetRotation.eulerAngles.z);
                    }
                    else if (rb3D != null)
                    {
                        rb3D.MoveRotation(pair.targetRotation);
                    }
                }

                if (pair.hasPositionTarget)
                {
                    if (rb2D != null)
                    {
                        rb2D.MovePosition(pair.targetPosition);
                    }
                    else if (rb3D != null)
                    {
                        rb3D.MovePosition(pair.targetPosition);
                    }
                }
            }
        }

    }
}

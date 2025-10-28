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
            public bool CopyPosition = true;
            public bool CopyRotation = true;

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

        [Tooltip("Automatically populate bone pairs on Awake when the list is empty.")]
        public bool AutoPopulateOnAwake = true;

        [Tooltip("Ordered list of master/puppet transform pairs to keep aligned.")]
        public List<BonePair> Pairs = new List<BonePair>();

        private readonly Dictionary<string, Transform> masterByName = new Dictionary<string, Transform>();
        private readonly List<Transform> masterBuffer = new List<Transform>();
        private readonly List<Transform> puppetBuffer = new List<Transform>();

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

            TryAutoPopulate();
        }

        private void Awake()
        {
            if (MasterRoot == null)
            {
                MasterRoot = transform;
            }

            if (AutoPopulateOnAwake && Pairs.Count == 0)
            {
                TryAutoPopulate();
            }
        }

        private void LateUpdate()
        {
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

                if (pair.CopyPosition)
                {
                    Vector3 targetPosition = pair.Master.position;
                    if (rb2D != null || rb3D != null)
                    {
                        pair.targetPosition = targetPosition;
                        pair.hasPositionTarget = true;
                    }
                    else
                    {
                        pair.Puppet.position = targetPosition;
                    }
                }
                else
                {
                    pair.hasPositionTarget = false;
                }

                if (pair.CopyRotation)
                {
                    Quaternion rotation = pair.Master.rotation;
                    if (rb2D != null || rb3D != null)
                    {
                        pair.targetRotation = rotation;
                        pair.hasRotationTarget = true;
                    }
                    else
                    {
                        pair.Puppet.rotation = rotation;
                    }
                }
                else
                {
                    pair.hasRotationTarget = false;
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

                if (pair.CopyPosition && pair.hasPositionTarget)
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

                if (pair.CopyRotation && pair.hasRotationTarget)
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
            }
        }

        public void TryAutoPopulate()
        {
            Pairs.Clear();
            masterByName.Clear();
            masterBuffer.Clear();
            puppetBuffer.Clear();

            if (MasterRoot == null || PuppetRoot == null)
            {
                Debug.LogWarning("SimplePuppetBinder: MasterRoot and PuppetRoot must be assigned for auto populate.", this);
                return;
            }

            masterBuffer.AddRange(MasterRoot.GetComponentsInChildren<Transform>(true));
            puppetBuffer.AddRange(PuppetRoot.GetComponentsInChildren<Transform>(true));

            for (int i = 0; i < masterBuffer.Count; i++)
            {
                Transform master = masterBuffer[i];
                if (!masterByName.ContainsKey(master.name))
                {
                    masterByName.Add(master.name, master);
                }
            }

            for (int i = 0; i < puppetBuffer.Count; i++)
            {
                Transform puppet = puppetBuffer[i];
                if (!masterByName.TryGetValue(puppet.name, out Transform master))
                {
                    continue;
                }

                Rigidbody2D rb2D = null;
                puppet.TryGetComponent(out rb2D);

                Rigidbody rb3D = null;
                puppet.TryGetComponent(out rb3D);

                Pairs.Add(new BonePair
                {
                    Master = master,
                    Puppet = puppet,
                    PuppetBody2D = rb2D,
                    PuppetBody3D = rb3D
                });
            }

            if (Pairs.Count == 0)
            {
                Debug.LogWarning("SimplePuppetBinder: No matching transforms were found during auto populate.", this);
            }
        }
    }
}

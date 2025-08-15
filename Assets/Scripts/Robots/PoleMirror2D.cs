using UnityEngine;

public sealed class PoleMirror2D : MonoBehaviour
{
    [System.Serializable]
    public struct Pole
    {
        public Transform target;    // pole Transform
        public Vector3 localRight;  // right-facing local position (0 = auto-capture)
        public bool mirrorZRotation;
    }

    [SerializeField] private Pole[] poles;
    [SerializeField] private bool captureOnAwake = true;
    [SerializeField] private bool startFacingRight = true;

    private bool facingRight;

    private void Awake()
    {
        if (captureOnAwake)
        {
            for (int i = 0; i < poles.Length; i++)
                if (poles[i].target && poles[i].localRight == Vector3.zero)
                    poles[i].localRight = poles[i].target.localPosition;
        }

        // Force an initial apply
        facingRight = !startFacingRight;
        SetFacing(startFacingRight);
    }

    public void SetFacing(bool isRight)
    {
        if (facingRight == isRight) return;
        facingRight = isRight;

        for (int i = 0; i < poles.Length; i++)
        {
            var p = poles[i];
            if (!p.target) continue;

            // mirror local X from the captured right pose
            var pos = p.localRight;
            if (!isRight) pos.x = -pos.x;
            p.target.localPosition = pos;

            if (p.mirrorZRotation)
            {
                var e = p.target.localEulerAngles;
                float z = e.z > 180f ? e.z - 360f : e.z; // [-180,180]
                z = isRight ? Mathf.Abs(z) : -Mathf.Abs(z);
                e.z = z < 0 ? z + 360f : z;
                p.target.localEulerAngles = e;
            }
        }
    }
}

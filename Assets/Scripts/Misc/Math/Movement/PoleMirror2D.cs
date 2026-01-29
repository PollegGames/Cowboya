using UnityEngine;

public sealed class PoleMirror2D : MonoBehaviour
{
    [System.Serializable]
    public struct Pole
    {
        public Transform target;    // pole Transform
        public bool mirrorZRotation;
        [HideInInspector] public float rightLocalX; // captured in Awake
    }

    [SerializeField] private Pole[] poles;
    [SerializeField] private bool startFacingRight = true;

    private bool facingRight;

    private void Awake()
    {
        for (int i = 0; i < poles.Length; i++)
            if (poles[i].target)
                poles[i].rightLocalX = poles[i].target.localPosition.x;

        // Force an initial apply
        facingRight = !startFacingRight;
        SetFacing(startFacingRight);
    }

    /// <summary>
    /// Mirrors all poles to face the specified direction.
    /// </summary>
    /// <param name="isRight">True when facing right.</param>
    public void SetFacing(bool isRight)
    {
        if (facingRight == isRight) return;
        facingRight = isRight;

        for (int i = 0; i < poles.Length; i++)
        {
            var p = poles[i];
            if (!p.target) continue;

            var pos = p.target.localPosition;
            pos.x = isRight ? p.rightLocalX : -p.rightLocalX;
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

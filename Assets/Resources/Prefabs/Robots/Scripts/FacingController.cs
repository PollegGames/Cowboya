using System.Collections.Generic;
using UnityEngine;

public class FacingController : MonoBehaviour
{
    [Header("Poles")]
    [SerializeField] private Transform leftLegPole;
    [SerializeField] private Transform rightLegPole;
    [SerializeField] private Transform leftArmPole;
    [SerializeField] private Transform rightArmPole;

    private bool legsRight = true;
    private bool armsRight = true;

    // Store original local positions of poles
    private Vector3 leftLegPoleInitial;
    private Vector3 rightLegPoleInitial;
    private Vector3 leftArmPoleInitial;
    private Vector3 rightArmPoleInitial;

    private void Start()
    {
        // Save initial local positions
        leftLegPoleInitial = leftLegPole.localPosition;
        rightLegPoleInitial = rightLegPole.localPosition;
        leftArmPoleInitial = leftArmPole.localPosition;
        rightArmPoleInitial = rightArmPole.localPosition;

        MirrorLegPoles(legsRight);
        MirrorArmPoles(armsRight);
    }

    public void SetLegFacing(bool faceRight)
    {
        if (faceRight == legsRight) return;
        legsRight = faceRight;
        MirrorLegPoles(faceRight);
    }

    public void SetArmFacing(bool faceRight)
    {
        if (faceRight == armsRight) return;
        armsRight = faceRight;
        MirrorArmPoles(faceRight);
    }

    private void MirrorLegPoles(bool faceRight)
    {
        float sign = faceRight ? 1f : -1f;

        if (leftLegPole != null)
            leftLegPole.localPosition = new Vector3(leftLegPoleInitial.x * sign, leftLegPoleInitial.y, leftLegPoleInitial.z);
        if (rightLegPole != null)
            rightLegPole.localPosition = new Vector3(rightLegPoleInitial.x * sign, rightLegPoleInitial.y, rightLegPoleInitial.z);
    }

    private void MirrorArmPoles(bool faceRight)
    {
        float sign = faceRight ? 1f : -1f;

        if (leftArmPole != null)
            leftArmPole.localPosition = new Vector3(leftArmPoleInitial.x * sign, leftArmPoleInitial.y, leftArmPoleInitial.z);
        if (rightArmPole != null)
            rightArmPole.localPosition = new Vector3(rightArmPoleInitial.x * sign, rightArmPoleInitial.y, rightArmPoleInitial.z);
    }
}

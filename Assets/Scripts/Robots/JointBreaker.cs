using System.Collections.Generic;
using UnityEngine;

public class JointBreaker : MonoBehaviour
{
    [Header("HingeJoint2D references")]
    public List<HingeJoint2D> hingeJoints = new List<HingeJoint2D>();

    [Header("FixedJoint2D references")]
    public List<FixedJoint2D> fixedJoints = new List<FixedJoint2D>();

    [Header("IK Solvers (Hinge2DIkSolver)")]
    public List<Hinge2DIkSolver> ikSolvers = new List<Hinge2DIkSolver>();

    private void Awake()
    {
        hingeJoints.AddRange(GetComponentsInChildren<HingeJoint2D>());
        fixedJoints.AddRange(GetComponentsInChildren<FixedJoint2D>());
        ikSolvers.AddRange(GetComponentsInChildren<Hinge2DIkSolver>());
    }

    public void BreakAll()
    {
        foreach (var hj in hingeJoints)
            if (hj != null) hj.breakForce = 0f;

        foreach (var fj in fixedJoints)
            if (fj != null) fj.breakForce = 0f;

        DisableAllSolvers();
    }

    public void DestroyAll()
    {
        foreach (var hj in hingeJoints)
            if (hj != null) Destroy(hj);

        foreach (var fj in fixedJoints)
            if (fj != null) Destroy(fj);

        hingeJoints.Clear();
        fixedJoints.Clear();

        DisableAllSolvers();
    }

    public void DisableAllSolvers()
    {
        foreach (var solver in ikSolvers)
            if (solver != null) solver.enabled = false;
    }
}

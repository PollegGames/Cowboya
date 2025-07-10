using UnityEngine;
using System;

[RequireComponent(typeof(MeshRenderer))]
public class SecurityMachine : MonoBehaviour
{
    [SerializeField] private bool isOn = true;
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private EnemyController currentGuard;
    private IWaypointService waypointService;

    public bool IsOn => isOn;
    public bool HasGuard => currentGuard != null;
    public EnemyController CurrentGuard => currentGuard;

    public void Initialize(IWaypointService service)
    {
        waypointService = service;
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    public void SetState(bool on)
    {
        if (isOn == on) return;

        if (!on)
        {
            SendCurrentGuardToRest();
        }

        isOn = on;
        ApplyMaterial();
    }

    public void ToggleState() => SetState(!isOn);

    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;
        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    private void OnValidate()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    public void OnSecurityGuardReady(EnemyController newGuard)
    {
        if (!isOn)
        {
            SendGuardToRest(currentGuard);
            currentGuard = null;
            SendGuardToRest(newGuard);
        }
        else
        {
            SendGuardToRest(currentGuard);
            SetGuardToCheck(newGuard);
            currentGuard = newGuard;
        }
    }

    private void SendGuardToRest(EnemyController guard)
    {
        if (guard == null) return;
        var sm = guard.GetComponent<EnemyStateMachine>();
        sm?.ChangeState(new Enemy_SecurityGuardRest(guard, sm, waypointService));
    }

    private void SetGuardToCheck(EnemyController guard)
    {
        if (guard == null) return;
        var sm = guard.GetComponent<EnemyStateMachine>();
        sm?.ChangeState(new Enemy_SecurityCheck(guard, sm, waypointService));
    }

    private void SendCurrentGuardToRest()
    {
        SendGuardToRest(currentGuard);
        currentGuard = null;
    }
}

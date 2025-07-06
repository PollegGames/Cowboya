using UnityEngine;

public enum MachineType
{
    WorkStation,
    RestStation,
    SecurityStation,
}

public class FactoryMachine : MonoBehaviour
{
    public bool isOn = true;
    public Material materialOn;
    public Material materialOff;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MachineType machineType;

    private EnemyWorkerController currentWorker;
    private bool haveWorkerConnected = false;
    public bool HaveWorkerConnected => haveWorkerConnected;

    public void SetState(bool on)
    {
        Debug.Log($"Setting FactoryMachine state to {(on ? "On" : "Off")}");
        isOn = on;
        meshRenderer.material = on ? materialOn : materialOff;
    }

    public void Toggle()
    {
        SetState(!isOn);
    }

    private void OnValidate()
    {
        if (meshRenderer != null)
            meshRenderer.material = isOn ? materialOn : materialOff;
    }

    public void OnWorkerReady(EnemyWorkerController newWorker)
    {
        if (!isOn) return;

        if (newWorker != null)
        {
            if (currentWorker != null)
            {
                if (machineType == MachineType.WorkStation)
                {
                    currentWorker.stateMachine.ChangeState(
                        new Worker_GoingToRestStation(
                            currentWorker, currentWorker.stateMachine, currentWorker.waypointService));
                }
            }

            if (machineType == MachineType.WorkStation)
            {
                newWorker.stateMachine.ChangeState(
                    new Worker_IsWork(
                        newWorker, newWorker.stateMachine, newWorker.waypointService));
            }
            else
            {
                newWorker.stateMachine.ChangeState(
                   new Worker_Idle(
                       newWorker, newWorker.stateMachine, newWorker.waypointService));
            }
        }

        currentWorker = newWorker;
        haveWorkerConnected = true;
    }
}
using UnityEngine;

public class RunStepExitTrigger : MonoBehaviour
{
    [SerializeField] private PlayerRunStats runStats;
    [SerializeField] private PlayerSaveService saveService;
    private bool hasRequestedTransition;

    private void Awake()
    {
        ResolveRunStats();
        if (saveService == null)
            saveService = FindFirstObjectByType<PlayerSaveService>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player") || hasRequestedTransition)
            return;

        hasRequestedTransition = true;
        if (!LaboratoryManager.TryFinalizeActiveVisit(collision))
        {
            hasRequestedTransition = false;
            return;
        }
        CaptureAndSave(collision);

        if (RunProgressManager.Instance != null)
        {
            RunProgressManager.Instance.LoadNextStep();
        }
        else
        {
            Debug.LogError("RunStepExitTrigger: RunProgressManager instance is missing.");
        }
    }

    private void CaptureAndSave(Collider2D collision)
    {
        RobotStateController controller = collision.GetComponentInParent<RobotStateController>();
        CowboyGrabController grabController = collision.GetComponentInParent<CowboyGrabController>();
        Inventory inventory = collision.GetComponentInParent<Inventory>();

        if (grabController != null)
        {
            grabController.ReleaseAllImmediate();
            inventory?.DropAll();
        }

        if (runStats != null && controller != null)
        {
            EnergyBot energyBot = controller.GetComponent<EnergyBot>();
            Attack attack = controller.Stats.Attacks.Count > 0 ? controller.Stats.Attacks[0] : null;
            AttackHitbox[] attackHitboxes = controller.GetComponentsInChildren<AttackHitbox>(true);
            runStats.Capture(controller.Stats, energyBot, attack, attackHitboxes);
            saveService?.SaveGame(controller, runStats);
        }
    }

    private void ResolveRunStats()
    {
        if (RunProgressManager.Instance == null || RunProgressManager.Instance.RunStats == null)
            return;

        runStats = RunProgressManager.Instance.RunStats;
    }
}

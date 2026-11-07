using System.Collections.Generic;
using UnityEngine;

/// <summary>What a laboratory cube machine does with the cubes it accepts.</summary>
public enum LaboratoryCubeMachineKind {
    /// <summary>Stores white cubes; the bank unlocks the next room at the end of the visit.</summary>
    WhiteCubeRoomBuilder = 0,
    /// <summary>Consumes colored cubes and upgrades the matching player stat immediately.</summary>
    ColorCubeUpgrade = 1
}

/// <summary>
/// Cube intake of a laboratory machine. The player deposits a cube by carrying it into the
/// intake trigger; loose cubes falling in are ignored so the collected-cube chute never feeds
/// the machine by accident. Feedback is console logs until the machine visuals exist.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class LaboratoryCubeMachine : MonoBehaviour {
    [SerializeField] private LaboratoryCubeMachineKind kind = LaboratoryCubeMachineKind.WhiteCubeRoomBuilder;
    [Tooltip("Upgrade values per colored cube. Defaults: health +10, energy +10, recharge +5, force +5.")]
    [SerializeField] private CubeUpgradeSO upgradeValues;

    private readonly HashSet<int> consumedCubeIds = new HashSet<int>();
    private readonly HashSet<int> reportedRejectionIds = new HashSet<int>();

    public LaboratoryCubeMachineKind Kind => kind;

    /// <summary>Sets the machine behaviour. Used when the intake is created at runtime.</summary>
    public void Configure(LaboratoryCubeMachineKind machineKind, CubeUpgradeSO values = null) {
        kind = machineKind;
        if (values != null) {
            upgradeValues = values;
        }
    }

    private void Awake() {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) {
        TryAcceptFromCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other) {
        TryAcceptFromCollider(other);
    }

    private void TryAcceptFromCollider(Collider2D other) {
        if (other == null) {
            return;
        }

        CubePickup cube = other.GetComponentInParent<CubePickup>();
        if (cube == null) {
            return;
        }

        CowboyGrabController holder = cube.transform.parent != null
            ? cube.transform.parent.GetComponentInParent<CowboyGrabController>()
            : null;
        if (holder == null) {
            return;
        }

        TryAccept(cube, holder, ResolveProgress());
    }

    /// <summary>
    /// Accepts one cube carried by the player when the run state allows it.
    /// Returns true when the cube was consumed.
    /// </summary>
    public bool TryAccept(CubePickup cube, CowboyGrabController holder, LaboratoryProgress progress) {
        if (cube == null || progress == null) {
            return false;
        }

        int cubeId = cube.GetInstanceID();
        if (consumedCubeIds.Contains(cubeId)) {
            return false;
        }

        LaboratoryCubeType type = ResolveCubeType(cube);
        if (!CanAccept(type, progress, out string rejection)) {
            if (reportedRejectionIds.Add(cubeId)) {
                Debug.Log($"[Laboratory] {kind} refused a {type} cube: {rejection}", this);
            }
            return false;
        }

        holder?.TryDetachHeldObject(cube);

        bool deposited = kind == LaboratoryCubeMachineKind.WhiteCubeRoomBuilder
            ? progress.TryDepositWhiteCube()
            : progress.TryDepositColorCube(type);
        if (!deposited) {
            Debug.LogWarning($"[Laboratory] {kind} could not store the {type} cube in the run state.", this);
            return false;
        }

        consumedCubeIds.Add(cubeId);
        if (kind == LaboratoryCubeMachineKind.WhiteCubeRoomBuilder) {
            LogWhiteCubeDeposit(progress);
        }
        else {
            ApplyColorCube(type, holder);
        }

        ConsumeCube(cube);
        return true;
    }

    private bool CanAccept(LaboratoryCubeType type, LaboratoryProgress progress, out string rejection) {
        if (kind == LaboratoryCubeMachineKind.WhiteCubeRoomBuilder) {
            if (type != LaboratoryCubeType.White) {
                rejection = "this machine only takes white cubes.";
                return false;
            }

            if (!progress.HasVisitedLaboratory) {
                rejection = "the machine starts working on the second laboratory visit.";
                return false;
            }

            if (!progress.CanDepositWhiteCube) {
                rejection = "it already holds enough cubes for every locked room.";
                return false;
            }

            rejection = null;
            return true;
        }

        if (type == LaboratoryCubeType.White) {
            rejection = "this machine only takes colored cubes.";
            return false;
        }

        if (!progress.CanDepositColorCube) {
            rejection = "the colored-cube room is not unlocked.";
            return false;
        }

        rejection = null;
        return true;
    }

    private void LogWhiteCubeDeposit(LaboratoryProgress progress) {
        int bank = progress.WhiteCubeMachineBank;
        if (!progress.TryGetNextRoomToUnlock(out LaboratoryRoomId nextRoom, out int cost)) {
            Debug.Log($"[Laboratory] White cube stored ({bank}). Every room is already unlocked.", this);
            return;
        }

        string status = bank >= cost
            ? $"room {nextRoom} unlocks at the next visit"
            : $"{cost - bank} more for room {nextRoom}";
        Debug.Log($"[Laboratory] White cube stored: {bank}/{cost} — {status}.", this);
    }

    private void ApplyColorCube(LaboratoryCubeType type, CowboyGrabController holder) {
        if (!TryMapUpgrade(type, out CubeUpgradeType upgrade)) {
            return;
        }

        CubeUpgradeSO values = upgradeValues != null
            ? upgradeValues
            : upgradeValues = ScriptableObject.CreateInstance<CubeUpgradeSO>();
        RobotStateController player = holder != null ? holder.GetComponentInParent<RobotStateController>() : null;
        if (player == null || player.Stats == null) {
            Debug.LogWarning($"[Laboratory] {type} cube consumed but no player stats were found to upgrade.", this);
            return;
        }

        PlayerRunStats runStats = RunProgressManager.Instance != null ? RunProgressManager.Instance.RunStats : null;
        EnergyBot energyBot = player.GetComponent<EnergyBot>();
        Attack attack = player.Stats.Attacks.Count > 0 ? player.Stats.Attacks[0] : null;
        AttackHitbox[] hitboxes = player.GetComponentsInChildren<AttackHitbox>(true);

        if (runStats != null) {
            // Same capture/apply path as level transitions, so the bonus survives the next scene.
            runStats.Capture(player.Stats, energyBot, attack, hitboxes);
            runStats.AddCubeBonus(upgrade, values);
            runStats.Apply(player.Stats, energyBot, attack, hitboxes);
        }
        else {
            values.Store(upgrade);
            values.ApplyUpgrade(player.Stats);
        }

        Debug.Log(
            $"[Laboratory] {type} cube applied. Player: health {player.Stats.MaxHealth}, "
            + $"energy {player.Stats.MaxEnergy}, recharge {player.Stats.EnergyRechargeRate}, "
            + $"force {(attack != null ? attack.Damage.ToString() : "?")}.",
            this);
    }

    private static bool TryMapUpgrade(LaboratoryCubeType type, out CubeUpgradeType upgrade) {
        switch (type) {
            case LaboratoryCubeType.MaxHealth:
                upgrade = CubeUpgradeType.MaxHealth;
                return true;
            case LaboratoryCubeType.MaxEnergy:
                upgrade = CubeUpgradeType.MaxEnergy;
                return true;
            case LaboratoryCubeType.EnergyRecharge:
                upgrade = CubeUpgradeType.EnergyRecharge;
                return true;
            case LaboratoryCubeType.AttackDamage:
                upgrade = CubeUpgradeType.AttackDamage;
                return true;
            default:
                upgrade = default;
                return false;
        }
    }

    /// <summary>Resolves the laboratory resource type of a physical cube.</summary>
    public static LaboratoryCubeType ResolveCubeType(CubePickup cube) {
        CubeUpgrade upgrade = cube != null ? cube.GetComponent<CubeUpgrade>() : null;
        if (upgrade == null) {
            return LaboratoryCubeType.White;
        }

        switch (upgrade.UpgradeType) {
            case CubeUpgradeType.MaxHealth:
                return LaboratoryCubeType.MaxHealth;
            case CubeUpgradeType.MaxEnergy:
                return LaboratoryCubeType.MaxEnergy;
            case CubeUpgradeType.EnergyRecharge:
                return LaboratoryCubeType.EnergyRecharge;
            default:
                return LaboratoryCubeType.AttackDamage;
        }
    }

    private static LaboratoryProgress ResolveProgress() {
        if (LaboratoryManager.ActiveInstance != null && LaboratoryManager.ActiveInstance.Progress != null) {
            return LaboratoryManager.ActiveInstance.Progress;
        }

        return RunProgressManager.Instance != null ? RunProgressManager.Instance.LaboratoryProgress : null;
    }

    private static void ConsumeCube(CubePickup cube) {
        foreach (Collider2D cubeCollider in cube.GetComponentsInChildren<Collider2D>(true)) {
            cubeCollider.enabled = false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            DestroyImmediate(cube.gameObject);
            return;
        }
#endif
        Destroy(cube.gameObject);
    }
}

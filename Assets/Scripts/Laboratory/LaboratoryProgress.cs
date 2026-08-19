using System;
using UnityEngine;

/// <summary>
/// Persistent disposition applied to DocBot when a laboratory visit begins.
/// </summary>
public enum LaboratoryScientistDisposition {
    Work = 0,
    CowardForVisit = 1
}

/// <summary>
/// Authoritative result of the most recently finalized laboratory visit.
/// </summary>
public enum LaboratoryVisitOutcome {
    None = 0,
    Survived = 1,
    WhiteCubeScheduled = 2,
    ScientistDied = 3,
    JunkReturnedAfterScientistDeath = 4
}

/// <summary>
/// Owns run-persistent laboratory state and the transactional state of one visit.
/// </summary>
[Serializable]
public sealed class LaboratoryProgress : ISerializationCallbackReceiver {
    public const int CurrentSchemaVersion = 3;
    public const int JunkVariantCount = 8;
    public const int LaboratoryCubeTypeCount = 5;

    [SerializeField] private int schemaVersion = CurrentSchemaVersion;
    [SerializeField] private bool hasVisitedLaboratory;

    [Header("Visit")]
    [SerializeField] private bool hasActiveVisit;
    [SerializeField] private int activeVisitId = -1;
    [SerializeField] private int lastFinalizedVisitId = -1;
    [SerializeField] private LaboratoryVisitOutcome lastVisitOutcome;

    [Header("Scientist")]
    [SerializeField] private LaboratoryScientistDisposition currentVisitDisposition;
    [SerializeField] private LaboratoryScientistDisposition nextVisitDisposition;
    [SerializeField] private bool acceptedJunkThisVisit;
    [SerializeField] private JunkVariant acceptedJunkVariant;
    [SerializeField] private bool scientistDiedThisVisit;

    [Header("Resources")]
    [SerializeField] private int availableWhiteCubeCount;
    // Kept only to migrate schema version 2 data into laboratoryFreeCubeCounts.
    [SerializeField] private int laboratoryFreeWhiteCubeCount;
    [SerializeField] private int whiteCubeCountPendingForNextVisit;
    [SerializeField] private int[] storedJunkCounts = new int[JunkVariantCount];
    [SerializeField] private int[] incomingCubeCounts = new int[LaboratoryCubeTypeCount];
    [SerializeField] private int[] laboratoryFreeCubeCounts = new int[LaboratoryCubeTypeCount];

    public int SchemaVersion => schemaVersion;
    public bool HasVisitedLaboratory => hasVisitedLaboratory;
    public bool HasActiveVisit => hasActiveVisit;
    public int ActiveVisitId => hasActiveVisit ? activeVisitId : -1;
    public int LastFinalizedVisitId => lastFinalizedVisitId;
    public LaboratoryVisitOutcome LastVisitOutcome => lastVisitOutcome;
    public LaboratoryScientistDisposition CurrentVisitDisposition => currentVisitDisposition;
    public LaboratoryScientistDisposition NextVisitDisposition => nextVisitDisposition;
    public bool AcceptedJunkThisVisit => hasActiveVisit && acceptedJunkThisVisit;
    public JunkVariant AcceptedJunkVariant => AcceptedJunkThisVisit ? acceptedJunkVariant : JunkVariant.None;
    public bool ScientistDiedThisVisit => hasActiveVisit && scientistDiedThisVisit;
    public int AvailableWhiteCubeCount => availableWhiteCubeCount;
    public int LaboratoryFreeWhiteCubeCount => GetLaboratoryFreeCubeCount(LaboratoryCubeType.White);
    public int WhiteCubeCountPendingForNextVisit => whiteCubeCountPendingForNextVisit;

    public int StoredJunkTotal {
        get {
            EnsureStoredJunkCounts();
            long total = 0;
            for (int i = 0; i < storedJunkCounts.Length; i++) {
                total += storedJunkCounts[i];
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }

    /// <summary>
    /// Starts a new visit and promotes rewards produced by the previous visit.
    /// Repeating an active or finalized visit identifier has no effect.
    /// </summary>
    public bool TryBeginVisit(int visitId) {
        SanitizeState();
        if (visitId < 0 || hasActiveVisit || visitId <= lastFinalizedVisitId) {
            return false;
        }

        availableWhiteCubeCount = SaturatingAdd(
            availableWhiteCubeCount,
            whiteCubeCountPendingForNextVisit);
        whiteCubeCountPendingForNextVisit = 0;

        for (int i = 0; i < LaboratoryCubeTypeCount; i++) {
            laboratoryFreeCubeCounts[i] = SaturatingAdd(
                laboratoryFreeCubeCounts[i],
                incomingCubeCounts[i]);
            incomingCubeCounts[i] = 0;
        }

        hasActiveVisit = true;
        activeVisitId = visitId;
        currentVisitDisposition = nextVisitDisposition;
        acceptedJunkThisVisit = false;
        acceptedJunkVariant = JunkVariant.None;
        scientistDiedThisVisit = false;
        return true;
    }

    /// <summary>
    /// Records the only Junk DocBot may accept during the active visit.
    /// </summary>
    public bool TryAcceptJunk(JunkVariant variant) {
        SanitizeState();
        if (!hasActiveVisit
            || currentVisitDisposition != LaboratoryScientistDisposition.Work
            || scientistDiedThisVisit
            || acceptedJunkThisVisit
            || !IsValidJunkVariant(variant)) {
            return false;
        }

        acceptedJunkThisVisit = true;
        acceptedJunkVariant = variant;
        return true;
    }

    /// <summary>
    /// Records DocBot's death once for the active visit.
    /// </summary>
    public bool TryMarkScientistDead() {
        SanitizeState();
        if (!hasActiveVisit || scientistDiedThisVisit) {
            return false;
        }

        scientistDiedThisVisit = true;
        return true;
    }

    /// <summary>
    /// Finalizes the active visit exactly once.
    /// </summary>
    public bool TryFinalizeVisit() {
        return TryFinalizeVisit(out _);
    }

    /// <summary>
    /// Finalizes the active visit exactly once and reports its authoritative outcome.
    /// </summary>
    public bool TryFinalizeVisit(out LaboratoryVisitOutcome outcome) {
        SanitizeState();
        if (!hasActiveVisit) {
            outcome = lastVisitOutcome;
            return false;
        }

        if (scientistDiedThisVisit) {
            nextVisitDisposition = LaboratoryScientistDisposition.CowardForVisit;
            if (acceptedJunkThisVisit) {
                StoreJunk(acceptedJunkVariant);
                outcome = LaboratoryVisitOutcome.JunkReturnedAfterScientistDeath;
            }
            else {
                outcome = LaboratoryVisitOutcome.ScientistDied;
            }
        }
        else {
            nextVisitDisposition = LaboratoryScientistDisposition.Work;
            if (acceptedJunkThisVisit) {
                whiteCubeCountPendingForNextVisit = SaturatingAdd(
                    whiteCubeCountPendingForNextVisit,
                    1);
                outcome = LaboratoryVisitOutcome.WhiteCubeScheduled;
            }
            else {
                outcome = LaboratoryVisitOutcome.Survived;
            }
        }

        hasVisitedLaboratory = true;
        lastVisitOutcome = outcome;
        lastFinalizedVisitId = activeVisitId;
        hasActiveVisit = false;
        activeVisitId = -1;
        acceptedJunkThisVisit = false;
        acceptedJunkVariant = JunkVariant.None;
        scientistDiedThisVisit = false;
        return true;
    }

    /// <summary>
    /// Atomically transfers one white cube from DocBot's available rewards to
    /// the laboratory's run-persistent free storage.
    /// </summary>
    public bool TryClaimAvailableWhiteCube() {
        SanitizeState();
        if (!hasActiveVisit || availableWhiteCubeCount <= 0) {
            return false;
        }

        availableWhiteCubeCount--;
        int whiteIndex = GetCubeIndex(LaboratoryCubeType.White);
        laboratoryFreeCubeCounts[whiteIndex] = SaturatingAdd(
            laboratoryFreeCubeCounts[whiteIndex],
            1);
        return true;
    }

    /// <summary>
    /// Consumes one free white cube for a future laboratory machine.
    /// </summary>
    public bool TryTakeLaboratoryFreeWhiteCube() {
        return TryTakeLaboratoryFreeCube(LaboratoryCubeType.White);
    }

    /// <summary>
    /// Gets the collected quantity waiting to enter the next laboratory visit.
    /// Invalid cube types report zero.
    /// </summary>
    public int GetIncomingCubeCount(LaboratoryCubeType type) {
        SanitizeState();
        int index = GetCubeIndex(type);
        return index >= 0 ? incomingCubeCounts[index] : 0;
    }

    /// <summary>
    /// Gets the free laboratory quantity for one exact cube type.
    /// Invalid cube types report zero.
    /// </summary>
    public int GetLaboratoryFreeCubeCount(LaboratoryCubeType type) {
        SanitizeState();
        int index = GetCubeIndex(type);
        return index >= 0 ? laboratoryFreeCubeCounts[index] : 0;
    }

    /// <summary>
    /// Returns a defensive snapshot indexed by <see cref="LaboratoryCubeType"/>.
    /// </summary>
    public int[] GetLaboratoryFreeCubeSnapshot() {
        SanitizeState();
        return (int[])laboratoryFreeCubeCounts.Clone();
    }

    /// <summary>
    /// Stores a positive number of collected cubes for the next laboratory visit.
    /// The operation is rejected without mutation when the type, amount, or sum is invalid.
    /// </summary>
    public bool TryStoreIncomingCube(LaboratoryCubeType type, int amount = 1) {
        SanitizeState();
        int index = GetCubeIndex(type);
        if (index < 0
            || amount <= 0
            || incomingCubeCounts[index] > int.MaxValue - amount) {
            return false;
        }

        incomingCubeCounts[index] += amount;
        return true;
    }

    /// <summary>
    /// Atomically removes a positive number of free cubes of the requested type.
    /// </summary>
    public bool TryTakeLaboratoryFreeCube(LaboratoryCubeType type, int amount = 1) {
        SanitizeState();
        int index = GetCubeIndex(type);
        if (index < 0 || amount <= 0 || laboratoryFreeCubeCounts[index] < amount) {
            return false;
        }

        laboratoryFreeCubeCounts[index] -= amount;
        return true;
    }

    /// <summary>
    /// Gets the stored quantity for one exact Junk variant.
    /// </summary>
    public int GetStoredJunkCount(JunkVariant variant) {
        EnsureStoredJunkCounts();
        int index = GetJunkIndex(variant);
        return index >= 0 ? storedJunkCounts[index] : 0;
    }

    /// <summary>
    /// Removes one stored Junk of the requested variant when available.
    /// </summary>
    public bool TryTakeStoredJunk(JunkVariant variant) {
        EnsureStoredJunkCounts();
        int index = GetJunkIndex(variant);
        if (index < 0 || storedJunkCounts[index] <= 0) {
            return false;
        }

        storedJunkCounts[index]--;
        return true;
    }

    /// <summary>
    /// Clears every laboratory value for a new run.
    /// </summary>
    public void Reset() {
        schemaVersion = CurrentSchemaVersion;
        hasVisitedLaboratory = false;
        hasActiveVisit = false;
        activeVisitId = -1;
        lastFinalizedVisitId = -1;
        lastVisitOutcome = LaboratoryVisitOutcome.None;
        currentVisitDisposition = LaboratoryScientistDisposition.Work;
        nextVisitDisposition = LaboratoryScientistDisposition.Work;
        acceptedJunkThisVisit = false;
        acceptedJunkVariant = JunkVariant.None;
        scientistDiedThisVisit = false;
        availableWhiteCubeCount = 0;
        laboratoryFreeWhiteCubeCount = 0;
        whiteCubeCountPendingForNextVisit = 0;
        storedJunkCounts = new int[JunkVariantCount];
        incomingCubeCounts = new int[LaboratoryCubeTypeCount];
        laboratoryFreeCubeCounts = new int[LaboratoryCubeTypeCount];
    }

    public void OnBeforeSerialize() {
        SanitizeState();
    }

    public void OnAfterDeserialize() {
        SanitizeState();
    }

    private void StoreJunk(JunkVariant variant) {
        EnsureStoredJunkCounts();
        int index = GetJunkIndex(variant);
        if (index >= 0) {
            storedJunkCounts[index] = SaturatingAdd(storedJunkCounts[index], 1);
        }
    }

    private void SanitizeState() {
        int serializedSchemaVersion = schemaVersion;
        EnsureCubeCounts();
        if (serializedSchemaVersion < 3 && laboratoryFreeWhiteCubeCount > 0) {
            int whiteIndex = GetCubeIndex(LaboratoryCubeType.White);
            laboratoryFreeCubeCounts[whiteIndex] = SaturatingAdd(
                laboratoryFreeCubeCounts[whiteIndex],
                laboratoryFreeWhiteCubeCount);
        }

        laboratoryFreeWhiteCubeCount = 0;
        schemaVersion = CurrentSchemaVersion;
        availableWhiteCubeCount = Math.Max(0, availableWhiteCubeCount);
        whiteCubeCountPendingForNextVisit = Math.Max(0, whiteCubeCountPendingForNextVisit);
        lastFinalizedVisitId = Math.Max(-1, lastFinalizedVisitId);

        if (!IsValidDisposition(currentVisitDisposition)) {
            currentVisitDisposition = LaboratoryScientistDisposition.Work;
        }

        if (!IsValidDisposition(nextVisitDisposition)) {
            nextVisitDisposition = LaboratoryScientistDisposition.Work;
        }

        if (!IsValidOutcome(lastVisitOutcome)) {
            lastVisitOutcome = LaboratoryVisitOutcome.None;
        }

        EnsureStoredJunkCounts();

        bool activeVisitIsValid = hasActiveVisit
            && activeVisitId >= 0
            && activeVisitId > lastFinalizedVisitId;
        if (!activeVisitIsValid) {
            hasActiveVisit = false;
            activeVisitId = -1;
            acceptedJunkThisVisit = false;
            acceptedJunkVariant = JunkVariant.None;
            scientistDiedThisVisit = false;
            return;
        }

        if (acceptedJunkThisVisit && !IsValidJunkVariant(acceptedJunkVariant)) {
            acceptedJunkThisVisit = false;
            acceptedJunkVariant = JunkVariant.None;
        }
        else if (!acceptedJunkThisVisit) {
            acceptedJunkVariant = JunkVariant.None;
        }
    }

    private void EnsureStoredJunkCounts() {
        if (storedJunkCounts == null || storedJunkCounts.Length != JunkVariantCount) {
            int[] previousCounts = storedJunkCounts;
            storedJunkCounts = new int[JunkVariantCount];
            if (previousCounts != null) {
                int copyCount = Math.Min(previousCounts.Length, storedJunkCounts.Length);
                for (int i = 0; i < copyCount; i++) {
                    storedJunkCounts[i] = Math.Max(0, previousCounts[i]);
                }
            }
        }

        for (int i = 0; i < storedJunkCounts.Length; i++) {
            storedJunkCounts[i] = Math.Max(0, storedJunkCounts[i]);
        }
    }

    private void EnsureCubeCounts() {
        incomingCubeCounts = EnsureCountArray(incomingCubeCounts, LaboratoryCubeTypeCount);
        laboratoryFreeCubeCounts = EnsureCountArray(
            laboratoryFreeCubeCounts,
            LaboratoryCubeTypeCount);
    }

    private static int[] EnsureCountArray(int[] counts, int requiredLength) {
        if (counts == null || counts.Length != requiredLength) {
            int[] previousCounts = counts;
            counts = new int[requiredLength];
            if (previousCounts != null) {
                int copyCount = Math.Min(previousCounts.Length, counts.Length);
                for (int i = 0; i < copyCount; i++) {
                    counts[i] = Math.Max(0, previousCounts[i]);
                }
            }
        }

        for (int i = 0; i < counts.Length; i++) {
            counts[i] = Math.Max(0, counts[i]);
        }

        return counts;
    }

    private static int GetJunkIndex(JunkVariant variant) {
        int index = (int)variant - 1;
        return index >= 0 && index < JunkVariantCount ? index : -1;
    }

    private static int GetCubeIndex(LaboratoryCubeType type) {
        int index = (int)type;
        return index >= 0 && index < LaboratoryCubeTypeCount ? index : -1;
    }

    private static bool IsValidJunkVariant(JunkVariant variant) {
        return GetJunkIndex(variant) >= 0;
    }

    private static bool IsValidDisposition(LaboratoryScientistDisposition disposition) {
        return disposition == LaboratoryScientistDisposition.Work
            || disposition == LaboratoryScientistDisposition.CowardForVisit;
    }

    private static bool IsValidOutcome(LaboratoryVisitOutcome outcome) {
        int value = (int)outcome;
        return value >= (int)LaboratoryVisitOutcome.None
            && value <= (int)LaboratoryVisitOutcome.JunkReturnedAfterScientistDeath;
    }

    private static int SaturatingAdd(int first, int second) {
        long result = (long)Math.Max(0, first) + Math.Max(0, second);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }
}

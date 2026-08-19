using NUnit.Framework;
using UnityEngine;

public class LaboratoryProgressTests {
    private static readonly LaboratoryCubeType[] AllCubeTypes = {
        LaboratoryCubeType.White,
        LaboratoryCubeType.MaxHealth,
        LaboratoryCubeType.MaxEnergy,
        LaboratoryCubeType.EnergyRecharge,
        LaboratoryCubeType.AttackDamage
    };

    private static readonly JunkVariant[] AllJunkVariants = {
        JunkVariant.Junk1,
        JunkVariant.Junk2,
        JunkVariant.Junk3,
        JunkVariant.Junk4,
        JunkVariant.Junk5,
        JunkVariant.Junk6,
        JunkVariant.Junk7,
        JunkVariant.Junk8
    };

    [Test]
    public void NewProgress_StartsWithWorkAndNoResources() {
        var progress = new LaboratoryProgress();

        Assert.AreEqual(LaboratoryProgress.CurrentSchemaVersion, progress.SchemaVersion);
        Assert.IsFalse(progress.HasVisitedLaboratory);
        Assert.IsFalse(progress.HasActiveVisit);
        Assert.AreEqual(LaboratoryScientistDisposition.Work, progress.NextVisitDisposition);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.LaboratoryFreeWhiteCubeCount);
        Assert.AreEqual(0, progress.WhiteCubeCountPendingForNextVisit);
        Assert.AreEqual(0, progress.StoredJunkTotal);
        foreach (LaboratoryCubeType type in AllCubeTypes) {
            Assert.AreEqual(0, progress.GetIncomingCubeCount(type), type.ToString());
            Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(type), type.ToString());
        }
    }

    [Test]
    public void IncomingCubes_StoreEveryTypeAndRepeatedQuantities() {
        var progress = new LaboratoryProgress();

        foreach (LaboratoryCubeType type in AllCubeTypes) {
            Assert.IsTrue(progress.TryStoreIncomingCube(type), type.ToString());
            Assert.IsTrue(progress.TryStoreIncomingCube(type, 2), type.ToString());
        }

        foreach (LaboratoryCubeType type in AllCubeTypes) {
            Assert.AreEqual(3, progress.GetIncomingCubeCount(type), type.ToString());
            Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(type), type.ToString());
        }
    }

    [Test]
    public void BeginVisit_PromotesIncomingCubesExactlyOnceAndPreservesExistingFreeCubes() {
        var progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.White, 2));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxHealth, 3));
        Assert.IsTrue(progress.TryBeginVisit(1));
        Assert.IsTrue(progress.TryTakeLaboratoryFreeCube(LaboratoryCubeType.White));
        Assert.IsTrue(progress.TryFinalizeVisit());

        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.White, 4));
        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.AttackDamage, 5));
        Assert.IsTrue(progress.TryBeginVisit(3));
        Assert.IsFalse(progress.TryBeginVisit(3));

        Assert.AreEqual(5, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));
        Assert.AreEqual(3, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(5, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.AttackDamage));
        foreach (LaboratoryCubeType type in AllCubeTypes) {
            Assert.AreEqual(0, progress.GetIncomingCubeCount(type), type.ToString());
        }
    }

    [Test]
    public void CubeQueriesAndTransfers_RejectInvalidInputsWithoutMutation() {
        var progress = new LaboratoryProgress();
        var invalidType = (LaboratoryCubeType)99;

        Assert.IsFalse(progress.TryStoreIncomingCube(invalidType));
        Assert.IsFalse(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxEnergy, 0));
        Assert.IsFalse(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxEnergy, -1));
        Assert.AreEqual(0, progress.GetIncomingCubeCount(invalidType));

        Assert.IsTrue(progress.TryStoreIncomingCube(LaboratoryCubeType.MaxEnergy, 2));
        Assert.IsTrue(progress.TryBeginVisit(1));
        Assert.IsFalse(progress.TryTakeLaboratoryFreeCube(invalidType));
        Assert.IsFalse(progress.TryTakeLaboratoryFreeCube(LaboratoryCubeType.MaxEnergy, 0));
        Assert.IsFalse(progress.TryTakeLaboratoryFreeCube(LaboratoryCubeType.MaxEnergy, 3));
        Assert.AreEqual(2, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxEnergy));
        Assert.IsTrue(progress.TryTakeLaboratoryFreeCube(LaboratoryCubeType.MaxEnergy, 2));
        Assert.IsFalse(progress.TryTakeLaboratoryFreeCube(LaboratoryCubeType.MaxEnergy));
    }

    [Test]
    public void IncomingCubeStore_RejectsOverflowWithoutChangingTheCount() {
        const string json = "{\"schemaVersion\":3,"
            + "\"incomingCubeCounts\":[0,0,0,0,2147483647]}";
        LaboratoryProgress progress = JsonUtility.FromJson<LaboratoryProgress>(json);

        Assert.IsFalse(progress.TryStoreIncomingCube(LaboratoryCubeType.AttackDamage));
        Assert.AreEqual(
            int.MaxValue,
            progress.GetIncomingCubeCount(LaboratoryCubeType.AttackDamage));
    }

    [Test]
    public void LaboratoryFreeSnapshot_IsDefensiveAndExcludesDocBotPendingAndAvailableCubes() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);
        progress.TryAcceptJunk(JunkVariant.Junk1);
        progress.TryFinalizeVisit();
        progress.TryBeginVisit(3);

        Assert.AreEqual(1, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));

        int[] snapshot = progress.GetLaboratoryFreeCubeSnapshot();
        Assert.AreEqual(LaboratoryProgress.LaboratoryCubeTypeCount, snapshot.Length);
        snapshot[(int)LaboratoryCubeType.White] = 99;
        Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));

        Assert.IsTrue(progress.TryClaimAvailableWhiteCube());
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);
        Assert.AreEqual(1, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));
    }

    [Test]
    public void CubeCounts_RoundTripAndSanitizeMalformedSerializedArrays() {
        const string json = "{\"schemaVersion\":3,\"incomingCubeCounts\":[-2,3],"
            + "\"laboratoryFreeCubeCounts\":[1,-4,5,6,7,8]}";

        LaboratoryProgress progress = JsonUtility.FromJson<LaboratoryProgress>(json);

        Assert.IsNotNull(progress);
        Assert.AreEqual(0, progress.GetIncomingCubeCount(LaboratoryCubeType.White));
        Assert.AreEqual(3, progress.GetIncomingCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(0, progress.GetIncomingCubeCount(LaboratoryCubeType.MaxEnergy));
        Assert.AreEqual(1, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.White));
        Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(7, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.AttackDamage));

        string restoredJson = JsonUtility.ToJson(progress);
        LaboratoryProgress restored = JsonUtility.FromJson<LaboratoryProgress>(restoredJson);
        Assert.AreEqual(3, restored.GetIncomingCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(5, restored.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxEnergy));
    }

    [Test]
    public void SchemaTwoWhiteCubeStorage_MigratesToTypedFreeInventoryOnce() {
        const string json = "{\"schemaVersion\":2,\"laboratoryFreeWhiteCubeCount\":4}";

        LaboratoryProgress progress = JsonUtility.FromJson<LaboratoryProgress>(json);

        Assert.IsNotNull(progress);
        Assert.AreEqual(LaboratoryProgress.CurrentSchemaVersion, progress.SchemaVersion);
        Assert.AreEqual(4, progress.LaboratoryFreeWhiteCubeCount);
        progress.OnBeforeSerialize();
        Assert.AreEqual(4, progress.LaboratoryFreeWhiteCubeCount);
    }

    [Test]
    public void BeginVisit_IsIdempotentAndRequiresPreviousFinalization() {
        var progress = new LaboratoryProgress();

        Assert.IsTrue(progress.TryBeginVisit(1));
        Assert.IsFalse(progress.TryBeginVisit(1));
        Assert.IsFalse(progress.TryBeginVisit(3));
        Assert.AreEqual(1, progress.ActiveVisitId);

        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.IsFalse(progress.TryBeginVisit(1));
        Assert.IsTrue(progress.TryBeginVisit(3));
        Assert.AreEqual(3, progress.ActiveVisitId);
    }

    [Test]
    public void WorkVisit_AcceptsOnlyOneValidJunk() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);

        Assert.IsFalse(progress.TryAcceptJunk(JunkVariant.None));
        Assert.IsFalse(progress.TryAcceptJunk((JunkVariant)99));
        Assert.IsTrue(progress.TryAcceptJunk(JunkVariant.Junk4));
        Assert.AreEqual(JunkVariant.Junk4, progress.AcceptedJunkVariant);
        Assert.IsFalse(progress.TryAcceptJunk(JunkVariant.Junk7));
    }

    [Test]
    public void SurvivingWithJunk_SchedulesCubeOnlyForNextVisit() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);
        progress.TryAcceptJunk(JunkVariant.Junk3);

        Assert.IsTrue(progress.TryFinalizeVisit(out LaboratoryVisitOutcome outcome));
        Assert.AreEqual(LaboratoryVisitOutcome.WhiteCubeScheduled, outcome);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(1, progress.WhiteCubeCountPendingForNextVisit);
        Assert.IsFalse(progress.TryClaimAvailableWhiteCube());
        Assert.IsFalse(progress.TryFinalizeVisit(out LaboratoryVisitOutcome repeatedOutcome));
        Assert.AreEqual(outcome, repeatedOutcome);
        Assert.AreEqual(1, progress.WhiteCubeCountPendingForNextVisit);

        Assert.IsTrue(progress.TryBeginVisit(3));
        Assert.IsFalse(progress.TryBeginVisit(3));
        Assert.AreEqual(1, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.WhiteCubeCountPendingForNextVisit);
        Assert.IsTrue(progress.TryClaimAvailableWhiteCube());
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);
        Assert.IsFalse(progress.TryClaimAvailableWhiteCube());

        string json = JsonUtility.ToJson(progress);
        LaboratoryProgress restored = JsonUtility.FromJson<LaboratoryProgress>(json);
        Assert.IsNotNull(restored);
        Assert.AreEqual(1, restored.LaboratoryFreeWhiteCubeCount);
        Assert.IsTrue(restored.TryTakeLaboratoryFreeWhiteCube());
        Assert.IsFalse(restored.TryTakeLaboratoryFreeWhiteCube());

        Assert.IsTrue(progress.TryTakeLaboratoryFreeWhiteCube());
        Assert.IsFalse(progress.TryTakeLaboratoryFreeWhiteCube());
    }

    [Test]
    public void DeathAfterJunk_ReturnsExactJunkAndDoesNotScheduleCube() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);
        progress.TryAcceptJunk(JunkVariant.Junk8);

        Assert.IsTrue(progress.TryMarkScientistDead());
        Assert.IsFalse(progress.TryMarkScientistDead());
        Assert.IsTrue(progress.TryFinalizeVisit(out LaboratoryVisitOutcome outcome));
        Assert.AreEqual(LaboratoryVisitOutcome.JunkReturnedAfterScientistDeath, outcome);
        Assert.AreEqual(1, progress.GetStoredJunkCount(JunkVariant.Junk8));
        Assert.AreEqual(1, progress.StoredJunkTotal);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.WhiteCubeCountPendingForNextVisit);
        Assert.AreEqual(
            LaboratoryScientistDisposition.CowardForVisit,
            progress.NextVisitDisposition);

        Assert.IsFalse(progress.TryFinalizeVisit(out LaboratoryVisitOutcome repeatedOutcome));
        Assert.AreEqual(outcome, repeatedOutcome);
        Assert.AreEqual(1, progress.GetStoredJunkCount(JunkVariant.Junk8));

        Assert.IsTrue(progress.TryBeginVisit(3));
        Assert.AreEqual(
            LaboratoryScientistDisposition.CowardForVisit,
            progress.CurrentVisitDisposition);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
    }

    [Test]
    public void CowardVisit_RejectsJunkAndSurvivalRestoresWork() {
        var progress = CreateProgressAfterScientistDeath();

        Assert.IsTrue(progress.TryBeginVisit(3));
        Assert.AreEqual(
            LaboratoryScientistDisposition.CowardForVisit,
            progress.CurrentVisitDisposition);
        Assert.IsFalse(progress.TryAcceptJunk(JunkVariant.Junk1));
        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.AreEqual(LaboratoryScientistDisposition.Work, progress.NextVisitDisposition);

        Assert.IsTrue(progress.TryBeginVisit(5));
        Assert.AreEqual(
            LaboratoryScientistDisposition.Work,
            progress.CurrentVisitDisposition);
        Assert.IsTrue(progress.TryAcceptJunk(JunkVariant.Junk1));
    }

    [Test]
    public void DyingDuringCowardVisit_KeepsNextVisitCowardly() {
        var progress = CreateProgressAfterScientistDeath();
        progress.TryBeginVisit(3);

        Assert.IsTrue(progress.TryMarkScientistDead());
        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.AreEqual(
            LaboratoryScientistDisposition.CowardForVisit,
            progress.NextVisitDisposition);

        Assert.IsTrue(progress.TryBeginVisit(5));
        Assert.AreEqual(
            LaboratoryScientistDisposition.CowardForVisit,
            progress.CurrentVisitDisposition);
    }

    [Test]
    public void AllJunkVariants_RetainIdentityThroughSerializationAndStorage() {
        foreach (JunkVariant variant in AllJunkVariants) {
            var progress = new LaboratoryProgress();
            Assert.IsTrue(progress.TryBeginVisit(1), variant.ToString());
            Assert.IsTrue(progress.TryAcceptJunk(variant), variant.ToString());

            string json = JsonUtility.ToJson(progress);
            LaboratoryProgress restored = JsonUtility.FromJson<LaboratoryProgress>(json);

            Assert.IsNotNull(restored, variant.ToString());
            Assert.AreEqual(variant, restored.AcceptedJunkVariant, variant.ToString());
            Assert.IsTrue(restored.TryMarkScientistDead(), variant.ToString());
            Assert.IsTrue(restored.TryFinalizeVisit(), variant.ToString());
            Assert.AreEqual(1, restored.GetStoredJunkCount(variant), variant.ToString());
            Assert.AreEqual(1, restored.StoredJunkTotal, variant.ToString());
        }
    }

    [Test]
    public void Reset_ClearsVisitConsequencesForANewRun() {
        var progress = new LaboratoryProgress();
        progress.TryStoreIncomingCube(LaboratoryCubeType.MaxHealth, 2);
        progress.TryBeginVisit(1);
        progress.TryStoreIncomingCube(LaboratoryCubeType.AttackDamage, 3);
        progress.TryAcceptJunk(JunkVariant.Junk6);
        progress.TryMarkScientistDead();
        progress.TryFinalizeVisit();

        progress.Reset();

        Assert.IsFalse(progress.HasVisitedLaboratory);
        Assert.IsFalse(progress.HasActiveVisit);
        Assert.AreEqual(-1, progress.LastFinalizedVisitId);
        Assert.AreEqual(LaboratoryVisitOutcome.None, progress.LastVisitOutcome);
        Assert.AreEqual(LaboratoryScientistDisposition.Work, progress.NextVisitDisposition);
        Assert.AreEqual(0, progress.StoredJunkTotal);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.LaboratoryFreeWhiteCubeCount);
        Assert.AreEqual(0, progress.WhiteCubeCountPendingForNextVisit);
        foreach (LaboratoryCubeType type in AllCubeTypes) {
            Assert.AreEqual(0, progress.GetIncomingCubeCount(type), type.ToString());
            Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(type), type.ToString());
        }
        Assert.IsTrue(progress.TryBeginVisit(0));
    }

    [Test]
    public void StoredJunk_CanBeTakenWithoutGoingNegative() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);
        progress.TryAcceptJunk(JunkVariant.Junk2);
        progress.TryMarkScientistDead();
        progress.TryFinalizeVisit();

        Assert.IsTrue(progress.TryTakeStoredJunk(JunkVariant.Junk2));
        Assert.IsFalse(progress.TryTakeStoredJunk(JunkVariant.Junk2));
        Assert.IsFalse(progress.TryTakeStoredJunk(JunkVariant.None));
        Assert.AreEqual(0, progress.GetStoredJunkCount(JunkVariant.Junk2));
        Assert.AreEqual(0, progress.StoredJunkTotal);
    }

    private static LaboratoryProgress CreateProgressAfterScientistDeath() {
        var progress = new LaboratoryProgress();
        progress.TryBeginVisit(1);
        progress.TryMarkScientistDead();
        progress.TryFinalizeVisit();
        return progress;
    }
}

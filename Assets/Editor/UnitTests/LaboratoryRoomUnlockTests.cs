using NUnit.Framework;

public class LaboratoryRoomUnlockTests {
    private int nextVisitId;

    [SetUp]
    public void SetUp() {
        nextVisitId = 1;
    }

    [Test]
    public void NewRun_OnlyWhiteCubeRoomIsUnlocked() {
        var progress = new LaboratoryProgress();

        Assert.IsTrue(progress.IsRoomUnlocked(LaboratoryRoomId.WhiteCubes));
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.ColorCubes));
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.Stats));
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.Upgrades));
        Assert.IsTrue(progress.TryGetNextRoomToUnlock(out LaboratoryRoomId next, out int cost));
        Assert.AreEqual(LaboratoryRoomId.ColorCubes, next);
        Assert.AreEqual(1, cost);
    }

    [Test]
    public void FirstVisit_WhiteCubeMachineRefusesCubes() {
        var progress = new LaboratoryProgress();
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 3);

        Assert.IsFalse(progress.CanDepositWhiteCube);
        Assert.IsFalse(progress.TryDepositWhiteCube());
        Assert.AreEqual(3, progress.LaboratoryFreeWhiteCubeCount, "Refused cubes stay free and respawn next visit.");
    }

    [Test]
    public void SecondVisit_DepositUnlocksColorRoomWhenVisitEnds() {
        var progress = new LaboratoryProgress();
        CompleteVisit(progress);
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 1);

        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.AreEqual(0, progress.LaboratoryFreeWhiteCubeCount);
        Assert.AreEqual(1, progress.WhiteCubeMachineBank);
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.ColorCubes), "Unlock waits for the end of the visit.");

        Assert.IsTrue(progress.TryFinalizeVisit());

        Assert.IsTrue(progress.IsRoomUnlocked(LaboratoryRoomId.ColorCubes));
        Assert.AreEqual(0, progress.WhiteCubeMachineBank);
        Assert.IsTrue(progress.TryGetRoomUnlockedAtLastFinalize(out LaboratoryRoomId unlocked));
        Assert.AreEqual(LaboratoryRoomId.ColorCubes, unlocked);
    }

    [Test]
    public void ExtraWhiteCubes_StayInMachineAndUnlockOneRoomPerVisit() {
        var progress = new LaboratoryProgress();
        CompleteVisit(progress);
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 6);
        for (int i = 0; i < 6; i++) {
            Assert.IsTrue(progress.TryDepositWhiteCube(), $"deposit {i + 1}");
        }

        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.IsTrue(progress.IsRoomUnlocked(LaboratoryRoomId.ColorCubes));
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.Stats));
        Assert.AreEqual(5, progress.WhiteCubeMachineBank);

        CompleteVisit(progress);
        Assert.IsTrue(progress.IsRoomUnlocked(LaboratoryRoomId.Stats));
        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.Upgrades));
        Assert.AreEqual(3, progress.WhiteCubeMachineBank);

        CompleteVisit(progress);
        Assert.IsTrue(progress.IsRoomUnlocked(LaboratoryRoomId.Upgrades));
        Assert.AreEqual(0, progress.WhiteCubeMachineBank);
        Assert.IsTrue(progress.AllRoomsUnlocked);

        CompleteVisit(progress);
        Assert.IsFalse(progress.TryGetRoomUnlockedAtLastFinalize(out _), "Nothing left to unlock.");
    }

    [Test]
    public void InsufficientBank_KeepsCubesForLaterVisits() {
        var progress = new LaboratoryProgress();
        CompleteVisit(progress);
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 1);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit()); // Color room (cost 1).

        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 1);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit());

        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.Stats), "Stats costs 2 white cubes.");
        Assert.AreEqual(1, progress.WhiteCubeMachineBank);
    }

    [Test]
    public void WhiteCubeMachine_RefusesCubesBeyondTheTotalRemainingCost() {
        var progress = new LaboratoryProgress();
        CompleteVisit(progress);
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 7);

        for (int i = 0; i < 6; i++) {
            Assert.IsTrue(progress.TryDepositWhiteCube(), $"deposit {i + 1}");
        }

        Assert.IsFalse(progress.TryDepositWhiteCube(), "1 + 2 + 3 cubes cover every room.");
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);
    }

    [Test]
    public void ColorCubeMachine_RequiresUnlockedRoomAndColoredCube() {
        var progress = new LaboratoryProgress();
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.MaxHealth, 1);
        Assert.IsFalse(progress.TryDepositColorCube(LaboratoryCubeType.MaxHealth), "Room still locked.");
        Assert.IsTrue(progress.TryFinalizeVisit());

        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 1);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit());

        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 0);
        Assert.IsFalse(progress.TryDepositColorCube(LaboratoryCubeType.White));
        Assert.IsTrue(progress.TryDepositColorCube(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(0, progress.GetLaboratoryFreeCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.IsFalse(progress.TryDepositColorCube(LaboratoryCubeType.MaxHealth), "No free cube left.");
    }

    [Test]
    public void Reset_LocksEveryRoomAndEmptiesTheMachine() {
        var progress = new LaboratoryProgress();
        CompleteVisit(progress);
        BeginVisitWithFreeCubes(progress, LaboratoryCubeType.White, 3);
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryDepositWhiteCube());
        Assert.IsTrue(progress.TryFinalizeVisit());

        progress.Reset();

        Assert.IsFalse(progress.IsRoomUnlocked(LaboratoryRoomId.ColorCubes));
        Assert.AreEqual(0, progress.WhiteCubeMachineBank);
    }

    private void BeginVisitWithFreeCubes(LaboratoryProgress progress, LaboratoryCubeType type, int amount) {
        if (amount > 0) {
            Assert.IsTrue(progress.TryStoreIncomingCube(type, amount));
        }

        Assert.IsTrue(progress.TryBeginVisit(nextVisitId++));
    }

    private void CompleteVisit(LaboratoryProgress progress) {
        Assert.IsTrue(progress.TryBeginVisit(nextVisitId++));
        Assert.IsTrue(progress.TryFinalizeVisit());
    }
}

using NUnit.Framework;

public class CollectorRobotFlyPrefabTests {
    [Test]
    public void CollectorRobotFlyPrefabMatchesApprovedPhysicsBaseline() {
        Assert.DoesNotThrow(CollectorRobotFlyPrefabBuilder.ValidateBuiltPrefab);
    }

    [Test]
    public void CollectorRobotFlyPrefabFallsAndRespectsHingeLimits() {
        Assert.DoesNotThrow(CollectorRobotFlyPrefabBuilder.ValidatePhysicsBehaviour);
    }
}

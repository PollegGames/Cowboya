using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CollectorFlightMotorTests {
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private SimulationMode2D previousSimulationMode;

    [SetUp]
    public void SetUp() {
        previousSimulationMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
    }

    [TearDown]
    public void TearDown() {
        Physics2D.simulationMode = previousSimulationMode;
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }
        createdObjects.Clear();
    }

    [Test]
    public void DisabledMotorPreservesFallingPhysics() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);

        Simulate(motor, 12, 0.02f);

        Assert.That(body.linearVelocity.y, Is.LessThan(-0.5f));
        Assert.That(motor.IsFlightActive, Is.False);
        Assert.That(motor.LastAppliedForce, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void PendingLaunchActivatesWhenMotorReceivesItsEnableCallback() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);
        GameObject root = motor.gameObject;
        root.SetActive(false);

        motor.StartFlight(
            () => body.position + Vector2.right * 5f,
            new CollectorFlightProfile(2f, 4f, 4f, 16f, 55f));

        Assert.That(motor.IsFlightActive, Is.False);

        root.SetActive(true);
        InvokePrivate(motor, "OnEnable");
        motor.StepPhysics(0.02f);

        Assert.That(motor.IsFlightActive, Is.True);
        Assert.That(motor.HasLiveTarget, Is.True);
        Assert.That(motor.LastAppliedForce.sqrMagnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void EnabledMotorCompensatesActualSupportedGravity() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);
        body.mass = 1.5f;
        body.gravityScale = 1f;
        CollectorFlightProfile profile = new CollectorFlightProfile(2f, 4f, 5f, 16f, 55f);
        motor.StartFlight(() => body.position, profile);

        Simulate(motor, 30, 0.02f);

        Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(0.05f));
        Assert.That(Mathf.Abs(body.position.y), Is.LessThan(0.05f));
        Assert.That(
            motor.LastGravityCompensationForce.y,
            Is.EqualTo(-Physics2D.gravity.y * body.mass).Within(0.001f));
    }

    [Test]
    public void SteeringHonorsConfiguredSpeedAccelerationAndForceCaps() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);
        CollectorFlightProfile profile = new CollectorFlightProfile(4f, 8f, 2f, 5f, 30f);
        motor.StartFlight(() => new Vector2(100f, 0f), profile);

        motor.StepPhysics(0.02f);

        Assert.That(motor.LastDesiredVelocity.magnitude, Is.LessThanOrEqualTo(2.001f));
        Assert.That(motor.LastRequestedAcceleration.magnitude, Is.LessThanOrEqualTo(5.001f));
        Assert.That(motor.LastAppliedForce.magnitude, Is.LessThanOrEqualTo(30.001f));
        Assert.That(motor.ForceBudgetInsufficient, Is.False);
        _ = body;
    }

    [Test]
    public void LiveTargetMovesWithoutRestartingMotor() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);
        Vector2 target = Vector2.right;
        motor.StartFlight(
            () => target,
            new CollectorFlightProfile(2f, 4f, 5f, 16f, 55f));

        motor.StepPhysics(0.02f);
        Assert.That(motor.CurrentTarget, Is.EqualTo(Vector2.right));

        target = new Vector2(-2f, 3f);
        motor.StepPhysics(0.02f);

        Assert.That(motor.CurrentTarget, Is.EqualTo(target));
        Assert.That(motor.IsFlightActive, Is.True);
        _ = body;
    }

    [Test]
    public void MotorBrakesButDoesNotOverwriteCollisionImpulse() {
        CollectorFlightMotor2D motor = CreateMotor(out Rigidbody2D body);
        motor.StartFlight(
            () => body.position,
            new CollectorFlightProfile(2f, 4f, 5f, 16f, 55f));
        body.AddForce(Vector2.right * 5f, ForceMode2D.Impulse);

        motor.StepPhysics(0.02f);
        Physics2D.Simulate(0.02f);

        Assert.That(body.linearVelocity.x, Is.GreaterThan(0.1f));
    }

    [Test]
    public void SensorFiltersSelfAndOwnedCargoWithoutCollisionMatrixChanges() {
        GameObject collector = CreateObject("Collector");
        BoxCollider2D selfCollider = collector.AddComponent<BoxCollider2D>();
        CollectorObstacleSensor2D sensor = collector.AddComponent<CollectorObstacleSensor2D>();
        sensor.ConfigureReferences(collector.transform);

        GameObject cargo = CreateObject("Cargo");
        Rigidbody2D cargoBody = cargo.AddComponent<Rigidbody2D>();
        BoxCollider2D cargoCollider = cargo.AddComponent<BoxCollider2D>();
        sensor.SetOwnedBodyPredicate(candidate => candidate == cargoBody);

        GameObject obstacle = CreateObject("Obstacle");
        BoxCollider2D obstacleCollider = obstacle.AddComponent<BoxCollider2D>();

        Assert.That(sensor.ShouldIgnore(selfCollider), Is.True);
        Assert.That(sensor.ShouldIgnore(cargoCollider), Is.True);
        Assert.That(sensor.ShouldIgnore(obstacleCollider), Is.False);
    }

    [Test]
    public void VisualStepSpinsOnlyWhileFlightIsActiveAndCanReset() {
        GameObject root = CreateObject("VisualRoot");
        GameObject pivotObject = CreateObject("PropellerPivot");
        pivotObject.transform.SetParent(root.transform, false);
        pivotObject.transform.localRotation = Quaternion.Euler(0f, 0f, 17f);
        CollectorFlightVisuals visuals = root.AddComponent<CollectorFlightVisuals>();
        visuals.ConfigureReferences(pivotObject.transform, null);

        visuals.SetFlightActive(true);
        visuals.StepVisual(0.1f);

        Assert.That(visuals.CurrentSpinSpeed, Is.GreaterThan(0f));
        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(17f, pivotObject.transform.localEulerAngles.z)),
            Is.GreaterThan(0.1f));

        visuals.ResetVisual();

        Assert.That(visuals.CurrentSpinSpeed, Is.EqualTo(0f));
        Assert.That(
            Mathf.DeltaAngle(17f, pivotObject.transform.localEulerAngles.z),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void PhysicalResetAdvancesRatherThanReusesCommandToken() {
        GameObject root = CreateObject("CollectorBodyTokenTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorObstacleSensor2D sensor = root.AddComponent<CollectorObstacleSensor2D>();
        CollectorFlightVisuals visuals = root.AddComponent<CollectorFlightVisuals>();
        CollectorMagnetController2D magnet = root.AddComponent<CollectorMagnetController2D>();
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            null,
            null,
            null,
            null,
            motor,
            sensor,
            magnet,
            visuals);

        body.StopAllActuators();
        int priorUseToken = body.CurrentCommandToken;
        body.ResetPhysicalState();

        Assert.That(body.CurrentCommandToken, Is.GreaterThan(priorUseToken));
        Assert.That(body.CurrentCommandToken, Is.GreaterThan(0));
    }

    [Test]
    public void GatheringHoldsReachedHeightWhileFollowingUnsecuredPartHorizontally() {
        GameObject root = CreateObject("CollectorGatherHoldTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        bodyRigidbody.position = new Vector2(2f, 3f);
        GameObject magnetObject = CreateObject("CollectorGatherHoldMagnet");
        magnetObject.transform.SetParent(root.transform, false);
        Rigidbody2D magnetRigidbody = magnetObject.AddComponent<Rigidbody2D>();
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        motor.ConfigureReferences(bodyRigidbody, magnetRigidbody, null);
        CollectorMagnetController2D magnet = root.AddComponent<CollectorMagnetController2D>();
        magnet.ConfigureReferences(bodyRigidbody, magnetRigidbody);
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            magnetRigidbody,
            null,
            null,
            null,
            motor,
            null,
            magnet,
            null);
        CollectorMissionAssignment assignment = CreateCollectorAssignment(out Rigidbody2D part);
        part.position = new Vector2(2f, 2.25f);

        body.BeginGathering(assignment);
        body.StepPhysics(0.02f);
        Vector2 heldPosition = motor.CurrentTarget;
        part.position += new Vector2(5f, 10f);
        body.StepPhysics(0.02f);

        Assert.That(heldPosition, Is.EqualTo(new Vector2(2f, 3f)));
        Assert.That(motor.CurrentTarget.x, Is.EqualTo(7f).Within(0.001f));
        Assert.That(motor.CurrentTarget.y, Is.EqualTo(heldPosition.y).Within(0.001f));
    }

    [Test]
    public void StallRecovery_TriesAbsoluteRightThenOriginalRouteThenLeft() {
        GameObject root = CreateObject("CollectorStallRecoveryTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        bodyRigidbody.gravityScale = 0f;
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            null,
            null,
            null,
            null,
            motor,
            null,
            null,
            null);
        SetPrivateField(body, "stallTimeout", 0.5f);
        SetPrivateField(body, "minimumProgress", 0.1f);
        SetPrivateField(body, "maximumRecoveryAttempts", 2);
        SetPrivateField(body, "recoveryDuration", 0.25f);
        SetPrivateField(body, "recoveryOffsetDistance", 1.2f);
        CollectorMissionAssignment assignment = CreateCollectorAssignment(
            out Rigidbody2D part);
        part.gravityScale = 0f;
        part.position = Vector2.left * 10f;

        body.BeginOutbound(assignment);
        body.StepPhysics(0.02f);
        body.StepPhysics(0.51f);

        Assert.That(body.IsStallRecoveryActive, Is.True);
        Assert.That(body.StallRecoveryAttemptCount, Is.EqualTo(1));
        Assert.That(body.StallRecoveryTarget.x, Is.EqualTo(1.2f).Within(0.001f));
        Assert.That(body.StallRecoveryTarget.y, Is.EqualTo(0f).Within(0.001f));

        body.StepPhysics(0.26f);
        Assert.That(body.IsStallRecoveryActive, Is.False);
        body.StepPhysics(0.02f);
        Assert.That(motor.CurrentTarget.x, Is.EqualTo(-10f).Within(0.001f));

        body.StepPhysics(0.51f);
        Assert.That(body.IsStallRecoveryActive, Is.True);
        Assert.That(body.StallRecoveryAttemptCount, Is.EqualTo(2));
        Assert.That(body.StallRecoveryTarget.x, Is.EqualTo(-1.2f).Within(0.001f));
        Assert.That(body.StallRecoveryTarget.y, Is.EqualTo(0f).Within(0.001f));

        body.StepPhysics(0.26f);
        body.StepPhysics(0.02f);
        body.StepPhysics(0.51f);

        Assert.That(body.IsStallRecoveryActive, Is.False);
        Assert.That(body.LastFlightFaultReason, Is.EqualTo("stalled_after_recovery"));
    }

    [Test]
    public void StallRecovery_RestoresAttemptBudgetAfterRouteProgress() {
        GameObject root = CreateObject("CollectorRepeatedStallRecoveryTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        bodyRigidbody.gravityScale = 0f;
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            null,
            null,
            null,
            null,
            motor,
            null,
            null,
            null);
        SetPrivateField(body, "stallTimeout", 0.5f);
        SetPrivateField(body, "minimumProgress", 0.1f);
        SetPrivateField(body, "maximumRecoveryAttempts", 2);
        SetPrivateField(body, "recoveryDuration", 0.25f);
        SetPrivateField(body, "recoveryOffsetDistance", 1.2f);
        CollectorMissionAssignment assignment = CreateCollectorAssignment(
            out Rigidbody2D part);
        part.gravityScale = 0f;
        part.position = Vector2.left * 10f;

        body.BeginOutbound(assignment);
        body.StepPhysics(0.02f);
        body.StepPhysics(0.51f);
        body.StepPhysics(0.26f);
        Assert.That(body.StallRecoveryAttemptCount, Is.EqualTo(1));

        bodyRigidbody.position = Vector2.left;
        body.StepPhysics(0.02f);

        Assert.That(body.StallRecoveryAttemptCount, Is.EqualTo(0));
        body.StepPhysics(0.51f);
        Assert.That(body.StallRecoveryAttemptCount, Is.EqualTo(1));
        Assert.That(body.StallRecoveryTarget.x, Is.EqualTo(0.2f).Within(0.001f));
    }

    [Test]
    public void OutboundArrival_UsesProximityDwellForMovingCorpse() {
        GameObject root = CreateObject("CollectorMovingTargetApproachTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        bodyRigidbody.gravityScale = 0f;
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            null,
            null,
            null,
            null,
            motor,
            null,
            null,
            null);
        CollectorMissionAssignment assignment = CreateCollectorAssignment(out Rigidbody2D part);
        part.gravityScale = 0f;
        part.position = Vector2.zero;
        part.linearVelocity = Vector2.right * 2f;
        bodyRigidbody.position = Vector2.up * 0.75f;
        bodyRigidbody.linearVelocity = Vector2.right * 2f;
        CollectorBodyObservation? observation = null;
        body.OnObservation += received => observation = received;

        body.BeginOutbound(assignment);
        body.StepPhysics(0.36f);

        Assert.That(bodyRigidbody.linearVelocity.magnitude, Is.GreaterThan(1f));
        Assert.That(observation.HasValue, Is.True);
        Assert.That(
            observation.Value.Type,
            Is.EqualTo(CollectorBodyObservationType.TargetApproachChanged));
        Assert.That(observation.Value.Assignment, Is.SameAs(assignment));
    }

    [Test]
    public void ReplacedCommandRejectsPriorCommandObservation() {
        GameObject root = CreateObject("CollectorBodyStaleObservationTest");
        Rigidbody2D bodyRigidbody = root.AddComponent<Rigidbody2D>();
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorRobotBodyController body = root.AddComponent<CollectorRobotBodyController>();
        body.ConfigureReferences(
            bodyRigidbody,
            null,
            null,
            null,
            null,
            motor,
            null,
            null,
            null);
        CollectorMissionAssignment assignment = new CollectorMissionAssignment(
            1,
            null,
            null,
            new CollectorTargetClaim(10, 1, 1));

        body.BeginLaunch(assignment);
        int priorCommandToken = body.CurrentCommandToken;
        CollectorBodyObservation staleObservation =
            CollectorBodyObservation.LaunchExit(assignment, priorCommandToken);
        body.CancelCurrentCommand(assignment);
        body.BeginLaunch(assignment);
        CollectorBodyObservation currentObservation =
            CollectorBodyObservation.LaunchExit(assignment, body.CurrentCommandToken);

        Assert.That(body.IsObservationCurrent(staleObservation), Is.False);
        Assert.That(body.IsObservationCurrent(currentObservation), Is.True);
        Assert.That(body.CurrentCommandToken, Is.GreaterThan(priorCommandToken));
    }

    private CollectorFlightMotor2D CreateMotor(out Rigidbody2D body) {
        GameObject root = CreateObject("CollectorFlightTest");
        body = root.AddComponent<Rigidbody2D>();
        body.mass = 1f;
        body.gravityScale = 1f;
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        motor.ConfigureReferences(body, null, null);
        Physics2D.SyncTransforms();
        return motor;
    }

    private CollectorMissionAssignment CreateCollectorAssignment(out Rigidbody2D part) {
        GameObject targetObject = CreateObject("CollectorGatherTarget");
        RobotStateController state = targetObject.AddComponent<RobotStateController>();
        GameObject partObject = CreateObject("CollectorGatherPart");
        partObject.transform.SetParent(targetObject.transform);
        part = partObject.AddComponent<Rigidbody2D>();
        partObject.AddComponent<BoxCollider2D>();
        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject homeObject = CreateObject("CollectorGatherHome");
        SpawnRobotCollectorController home = homeObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(21, home, out CollectorTargetClaim claim));
        return new CollectorMissionAssignment(21, home, target, claim);
    }

    private void Simulate(CollectorFlightMotor2D motor, int steps, float deltaTime) {
        for (int i = 0; i < steps; i++) {
            motor.StepPhysics(deltaTime);
            Physics2D.Simulate(deltaTime);
        }
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static void InvokePrivate(object target, string methodName) {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Expected method '{methodName}'.");
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected field '{fieldName}'.");
        field.SetValue(target, value);
    }
}

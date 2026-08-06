using System;
using System.Collections.Generic;
using CowBoya.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds and validates the physics-only master/puppet baseline for CollectorRobot_Fly.
/// </summary>
public static class CollectorRobotFlyPrefabBuilder {
    public const string FinalPrefabPath = "Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab";
    public const string MasterPrefabPath = "Assets/Resources/Prefabs/Robots/Collector/Others/CollectorRobot_Fly_Master.prefab";
    public const string PuppetPrefabPath = "Assets/Resources/Prefabs/Robots/Collector/Others/CollectorRobot_Fly_Puppet.prefab";
    public const string MachinePrefabPath = "Assets/Resources/Prefabs/Map/Basic/Machines/SpawnRobotCollector.prefab";

    private const string EnemyTag = "Enemy";
    private const string EnemyLayerName = "Enemy";
    private const float FinalScale = 0.4f;
    private const float BodyMass = 1.5f;
    private const float MagnetMass = 0.35f;
    private const float LinearDamping = 1f;
    private const float AngularDamping = 2f;
    private const float GravityScale = 1f;
    private const float LowerMagnetAngle = -90f;
    private const float UpperMagnetAngle = 90f;

    /// <summary>
    /// Creates the Collector master/puppet working prefabs and assembles the final physics prefab.
    /// </summary>
    [MenuItem("Tools/CowBoya/Build Collector Robot Fly Physics")]
    public static void BuildAndValidate() {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        if (enemyLayer < 0) {
            throw new InvalidOperationException($"Required layer '{EnemyLayerName}' does not exist.");
        }

        EnsureWorkingPrefabs(enemyLayer);
        NormalizeMasterWorkingPrefab(enemyLayer);
        NormalizePuppetWorkingPrefab(enemyLayer);
        BuildFinalPrefab(enemyLayer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureMachinePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateBuiltPrefab();
        ValidateMachinePrefab();
        ValidatePhysicsBehaviour();
    }

    /// <summary>
    /// Validates the generated prefab against the approved physics-only baseline.
    /// </summary>
    public static void ValidateBuiltPrefab() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(prefab != null, $"Missing final prefab at '{FinalPrefabPath}'.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(MasterPrefabPath) != null,
            $"Missing master working prefab at '{MasterPrefabPath}'.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(PuppetPrefabPath) != null,
            $"Missing puppet working prefab at '{PuppetPrefabPath}'.");

        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        Require(prefab.name == "CollectorRobot_Fly", "Final root has the wrong name.");
        Require(prefab.tag == EnemyTag, "Final root must use the Enemy tag.");
        Require(prefab.layer == enemyLayer, "Final root must use the Enemy layer.");
        Require(Approximately(prefab.transform.localPosition, Vector3.zero), "Final root position must be zero.");
        Require(Approximately(prefab.transform.localRotation, Quaternion.identity), "Final root rotation must be identity.");
        Require(Approximately(prefab.transform.localScale, Vector3.one * FinalScale), "Final root scale must be 0.4.");
        Require(prefab.GetComponent<Rigidbody2D>() == null, "Final container must not have a Rigidbody2D.");

        Transform puppetRoot = FindDirectChild(prefab.transform, "CollectorRobot_Fly_Puppet");
        Transform puppetBody = FindDirectChild(puppetRoot, "bone_Body");
        Transform puppetMagnet = FindDirectChild(puppetRoot, "bone_Magnet");
        Transform puppetSprites = FindDirectChild(puppetRoot, "Sprites");
        Transform masterRoot = FindDescendantExact(puppetBody, "CollectorRobot_Fly_Master");
        Transform masterBody = FindDirectChild(masterRoot, "bone_Body");
        Transform masterMagnet = FindDirectChild(masterRoot, "bone_Magnet");
        FindDirectChild(masterRoot, "Sprites");
        FindDirectChild(puppetSprites, "Body");
        FindDirectChild(puppetSprites, "Magnet");
        Transform propellerPivot = FindDirectChild(puppetBody, "PropellerPivot");
        Transform helice = FindDirectChild(propellerPivot, "Helice");

        Require(masterRoot.IsChildOf(puppetBody), "Master root must follow the physical body through hierarchy parenting.");
        Require(puppetRoot.tag == EnemyTag, "Puppet root must use the Enemy tag.");
        Require(puppetBody.gameObject.tag == EnemyTag, "Physical body must use the Enemy tag.");
        Require(puppetMagnet.gameObject.tag == EnemyTag, "Physical magnet must use the Enemy tag.");
        Require(puppetRoot.gameObject.layer == enemyLayer, "Puppet root must use the Enemy layer.");
        Require(puppetBody.gameObject.layer == enemyLayer, "Physical body must use the Enemy layer.");
        Require(puppetMagnet.gameObject.layer == enemyLayer, "Physical magnet must use the Enemy layer.");

        Rigidbody2D[] rigidbodies = prefab.GetComponentsInChildren<Rigidbody2D>(true);
        Require(rigidbodies.Length == 2, $"Expected exactly two Rigidbody2D components, found {rigidbodies.Length}.");
        Rigidbody2D bodyRigidbody = puppetBody.GetComponent<Rigidbody2D>();
        Rigidbody2D magnetRigidbody = puppetMagnet.GetComponent<Rigidbody2D>();
        Require(bodyRigidbody != null, "Puppet body is missing its Rigidbody2D.");
        Require(magnetRigidbody != null, "Puppet magnet is missing its Rigidbody2D.");
        ValidateRigidbody(bodyRigidbody, BodyMass, RigidbodyInterpolation2D.Interpolate,
            CollisionDetectionMode2D.Continuous, "body");
        ValidateRigidbody(magnetRigidbody, MagnetMass, RigidbodyInterpolation2D.Interpolate,
            CollisionDetectionMode2D.Discrete, "magnet");

        Collider2D[] colliders = prefab.GetComponentsInChildren<Collider2D>(true);
        Require(colliders.Length == 2, $"Expected exactly two Collider2D components, found {colliders.Length}.");
        BoxCollider2D bodyCollider = puppetBody.GetComponent<BoxCollider2D>();
        BoxCollider2D magnetCollider = puppetMagnet.GetComponent<BoxCollider2D>();
        Require(bodyCollider != null && !bodyCollider.isTrigger, "Body needs one non-trigger BoxCollider2D.");
        Require(magnetCollider != null && !magnetCollider.isTrigger, "Magnet needs one non-trigger BoxCollider2D.");
        Require(bodyCollider.size.x > 0f && bodyCollider.size.y > 0f, "Body collider has an invalid size.");
        Require(magnetCollider.size.x > 0f && magnetCollider.size.y > 0f, "Magnet collider has an invalid size.");

        HingeJoint2D[] hinges = prefab.GetComponentsInChildren<HingeJoint2D>(true);
        Require(hinges.Length == 1, $"Expected exactly one HingeJoint2D, found {hinges.Length}.");
        HingeJoint2D hinge = hinges[0];
        Require(hinge.transform == puppetMagnet, "HingeJoint2D must be on the physical magnet.");
        Require(hinge.connectedBody == bodyRigidbody, "HingeJoint2D must connect to the physical body.");
        Require(!hinge.enableCollision, "Connected body and magnet collision must be disabled.");
        Require(!hinge.useMotor, "Magnet hinge motor must remain disabled.");
        Require(hinge.useLimits, "Magnet hinge limits must be enabled.");
        Require(Mathf.Approximately(hinge.limits.min, LowerMagnetAngle), "Magnet lower hinge limit must be -90 degrees.");
        Require(Mathf.Approximately(hinge.limits.max, UpperMagnetAngle), "Magnet upper hinge limit must be 90 degrees.");
        Require(float.IsPositiveInfinity(hinge.breakForce), "Magnet hinge break force must be Infinity.");
        Require(float.IsPositiveInfinity(hinge.breakTorque), "Magnet hinge break torque must be Infinity.");
        Vector3 hingeAnchor = hinge.transform.TransformPoint(hinge.anchor);
        Vector3 connectedAnchor = bodyRigidbody.transform.TransformPoint(hinge.connectedAnchor);
        Require(Vector3.Distance(hingeAnchor, connectedAnchor) < 0.001f,
            "Magnet hinge anchors do not occupy the same world point.");

        SimplePuppetBinder[] binders = prefab.GetComponentsInChildren<SimplePuppetBinder>(true);
        Require(binders.Length == 1, $"Expected exactly one SimplePuppetBinder, found {binders.Length}.");
        SimplePuppetBinder binder = binders[0];
        Require(binder.transform == prefab.transform, "SimplePuppetBinder must be on the final container.");
        Require(binder.MasterRoot == masterRoot, "Binder MasterRoot is incorrect.");
        Require(binder.PuppetRoot == puppetRoot, "Binder PuppetRoot is incorrect.");
        Require(Mathf.Approximately(binder.RotationSharpness, 0f), "Binder RotationSharpness must be zero.");
        Require(binder.Pairs != null && binder.Pairs.Count == 2, "Binder must contain exactly two pairs.");
        ValidatePair(binder.Pairs[0], masterBody, puppetBody, bodyRigidbody, "body");
        ValidatePair(binder.Pairs[1], masterMagnet, puppetMagnet, magnetRigidbody, "magnet");

        RobotMemoryNew memory = RequireSingleRootComponent<RobotMemoryNew>(prefab);
        RobotHeartNew heart = RequireSingleRootComponent<RobotHeartNew>(prefab);
        RobotBrainNew brain = RequireSingleRootComponent<RobotBrainNew>(prefab);
        RobotStateController state = RequireSingleRootComponent<RobotStateController>(prefab);
        HealthBot health = RequireSingleRootComponent<HealthBot>(prefab);
        JointBreaker jointBreaker = RequireSingleRootComponent<JointBreaker>(prefab);
        CollectorFlightMotor2D motor = RequireSingleRootComponent<CollectorFlightMotor2D>(prefab);
        CollectorObstacleSensor2D sensor = RequireSingleRootComponent<CollectorObstacleSensor2D>(prefab);
        CollectorMagnetController2D magnetController = RequireSingleRootComponent<CollectorMagnetController2D>(prefab);
        CollectorFlightVisuals visuals = RequireSingleRootComponent<CollectorFlightVisuals>(prefab);
        CollectorRobotBodyController collectorBody = RequireSingleRootComponent<CollectorRobotBodyController>(prefab);
        CollectorRobotObservationBridge bridge = RequireSingleRootComponent<CollectorRobotObservationBridge>(prefab);
        CollectorPoolLifecycle lifecycle = RequireSingleRootComponent<CollectorPoolLifecycle>(prefab);
        Require(memory != null && brain != null && health != null && jointBreaker != null
            && sensor != null && magnetController != null && visuals != null
            && bridge != null && lifecycle != null,
            "Collector runtime pipeline contains a missing component reference.");
        Require(heart.Role == RobotRole.Collector, "Collector Heart must serialize the Collector role.");
        Require(state.Stats != null, "Collector must serialize a non-null baseline RobotStats instance.");
        Require(collectorBody.BodyRigidbody == bodyRigidbody,
            "Collector body facade has the wrong body Rigidbody reference.");
        Require(collectorBody.MagnetRigidbody == magnetRigidbody,
            "Collector body facade has the wrong magnet Rigidbody reference.");
        Require(motor.BodyRigidbody == bodyRigidbody,
            "Collector flight motor has the wrong body Rigidbody reference.");

        Require(prefab.GetComponentsInChildren<Animator>(true).Length == 0,
            "Animator is explicitly deferred and must not exist in this prefab.");
        Require(propellerPivot.GetComponentsInChildren<Rigidbody2D>(true).Length == 0,
            "Propeller hierarchy must not contain a Rigidbody2D.");
        Require(propellerPivot.GetComponentsInChildren<Collider2D>(true).Length == 0,
            "Propeller hierarchy must not contain a Collider2D.");
        Require(Approximately(propellerPivot.localRotation, Quaternion.identity),
            "PropellerPivot must have an identity local rest rotation.");
        Require(Approximately(propellerPivot.localScale, Vector3.one),
            "PropellerPivot must have a unit local scale.");
        Require(Vector3.Distance(propellerPivot.position, helice.position) < 0.001f,
            "PropellerPivot must be centred on Helice.");
        Require(helice.GetComponent<Renderer>() != null, "Helice must retain its visible renderer.");

        Renderer[] masterRenderers = masterRoot.GetComponentsInChildren<Renderer>(true);
        Require(masterRenderers.Length > 0, "Master copy unexpectedly contains no renderers.");
        for (int i = 0; i < masterRenderers.Length; i++) {
            Require(!masterRenderers[i].enabled, $"Master renderer '{masterRenderers[i].name}' must be disabled.");
        }

        Renderer[] puppetRenderers = puppetRoot.GetComponentsInChildren<Renderer>(true);
        int enabledPuppetRendererCount = 0;
        for (int i = 0; i < puppetRenderers.Length; i++) {
            if (puppetRenderers[i].enabled && !puppetRenderers[i].transform.IsChildOf(masterRoot)) {
                enabledPuppetRendererCount++;
            }
        }

        Require(enabledPuppetRendererCount >= 3, "Puppet must retain its visible Body, Magnet, and Helice renderers.");
    }

    /// <summary>
    /// Validates that the spawning machine owns a Collector prefab, live markers, and one query intake.
    /// </summary>
    public static void ValidateMachinePrefab() {
        GameObject machine = AssetDatabase.LoadAssetAtPath<GameObject>(MachinePrefabPath);
        Require(machine != null, $"Missing Collector machine prefab at '{MachinePrefabPath}'.");
        SpawnRobotCollectorController controller = machine.GetComponent<SpawnRobotCollectorController>();
        Require(controller != null, "Collector machine is missing its controller.");
        GameObject collector = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(controller.CollectorPrefab == collector, "Collector machine has the wrong Collector prefab reference.");
        Require(controller.LaunchExitPoint != null && controller.LaunchExitPoint.name == "LaunchExitPoint",
            "Collector machine is missing its live LaunchExitPoint.");
        Require(controller.DockApproachPoint != null && controller.DockApproachPoint.name == "DockApproachPoint",
            "Collector machine is missing its live DockApproachPoint.");
        Require(controller.IntakePoint != null && controller.IntakePoint.name == "IntakePoint",
            "Collector machine is missing its live IntakePoint.");
        Require(controller.IntakeZone != null && controller.IntakeZone.isTrigger,
            "Collector machine needs one trigger intake zone.");
        Require(controller.LaunchExitPoint.parent == controller.IntakePoint.parent
            && controller.DockApproachPoint.parent == controller.IntakePoint.parent,
            "Collector live markers must share the moving SpawnPoint parent.");
    }

    /// <summary>
    /// Simulates the prefab in isolated 2D physics scenes to verify falling, collision, attachment, and hinge limits.
    /// </summary>
    public static void ValidatePhysicsBehaviour() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(prefab != null, $"Missing final prefab at '{FinalPrefabPath}'.");
        ValidateFallingAndAttachment(prefab);
        ValidateHingeLimitSimulation(prefab);
    }

    private static void EnsureWorkingPrefabs(int enemyLayer) {
        bool needsMaster = AssetDatabase.LoadAssetAtPath<GameObject>(MasterPrefabPath) == null;
        bool needsPuppet = AssetDatabase.LoadAssetAtPath<GameObject>(PuppetPrefabPath) == null;
        if (!needsMaster && !needsPuppet) {
            return;
        }

        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(sourcePrefab != null, $"Missing Collector source prefab at '{FinalPrefabPath}'.");
        Require(sourcePrefab.GetComponentInChildren<SimplePuppetBinder>(true) == null,
            "Working prefabs are missing but the final prefab is already assembled. Refusing to derive helpers from the assembled output.");

        if (needsMaster) {
            BuildWorkingPrefab(sourcePrefab, MasterPrefabPath, "CollectorRobot_Fly_Master",
                root => PrepareMaster(root, enemyLayer));
        }

        if (needsPuppet) {
            BuildWorkingPrefab(sourcePrefab, PuppetPrefabPath, "CollectorRobot_Fly_Puppet",
                root => PreparePuppet(root, enemyLayer));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void BuildWorkingPrefab(GameObject sourcePrefab, string outputPath, string rootName,
        Action<GameObject> prepare) {
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try {
            GameObject instance = InstantiatePrefab(sourcePrefab, previewScene);
            UnpackCompletely(instance);
            instance.name = rootName;
            ResetRootTransform(instance.transform);
            prepare(instance);
            bool success;
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath, out success);
            Require(success, $"Unity failed to save working prefab '{outputPath}'.");
        } finally {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void PrepareMaster(GameObject root, int enemyLayer) {
        FindDirectChild(root.transform, "bone_Body");
        FindDirectChild(root.transform, "bone_Magnet");
        RemoveDeferredAndPhysicsComponents(root);
        SetLayerRecursively(root, enemyLayer);
        OrganizeDirectRenderers(root.transform, null, enemyLayer);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Require(renderers.Length > 0, "Master working copy contains no renderers to disable.");
        for (int i = 0; i < renderers.Length; i++) {
            renderers[i].enabled = false;
        }
    }

    private static void PreparePuppet(GameObject root, int enemyLayer) {
        Transform bodyBone = FindDirectChild(root.transform, "bone_Body");
        FindDirectChild(root.transform, "bone_Magnet");
        RemoveDeferredAndPhysicsComponents(root);
        SetLayerRecursively(root, enemyLayer);

        Transform helice = FindDescendantExact(root.transform, "Helice");
        Require(helice.GetComponent<Renderer>() != null, "Helice is missing its renderer before reparenting.");
        Require(helice.GetComponent<Rigidbody2D>() == null, "Helice must remain non-physical.");
        OrganizeDirectRenderers(root.transform, helice, enemyLayer);

        GameObject pivotObject = new GameObject("PropellerPivot");
        SceneManager.MoveGameObjectToScene(pivotObject, root.scene);
        pivotObject.layer = enemyLayer;
        Transform pivot = pivotObject.transform;
        pivot.SetParent(bodyBone, false);
        pivot.position = helice.position;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        helice.SetParent(pivot, true);

        Require(Vector3.Distance(pivot.position, helice.position) < 0.001f,
            "Propeller pivot is not centred on the Helice transform.");
    }

    private static void BuildFinalPrefab(int enemyLayer) {
        GameObject masterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MasterPrefabPath);
        GameObject puppetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuppetPrefabPath);
        Require(masterPrefab != null, "Master working prefab could not be loaded.");
        Require(puppetPrefab != null, "Puppet working prefab could not be loaded.");

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try {
            GameObject finalRoot = new GameObject("CollectorRobot_Fly");
            SceneManager.MoveGameObjectToScene(finalRoot, previewScene);
            finalRoot.transform.localPosition = Vector3.zero;
            finalRoot.transform.localRotation = Quaternion.identity;
            finalRoot.transform.localScale = Vector3.one * FinalScale;
            finalRoot.tag = EnemyTag;
            finalRoot.layer = enemyLayer;

            GameObject puppetRootObject = InstantiatePrefab(puppetPrefab, previewScene);
            UnpackCompletely(puppetRootObject);
            puppetRootObject.name = "CollectorRobot_Fly_Puppet";
            puppetRootObject.transform.SetParent(finalRoot.transform, false);
            ResetRootTransform(puppetRootObject.transform);
            puppetRootObject.tag = EnemyTag;
            SetLayerRecursively(puppetRootObject, enemyLayer);

            Transform puppetRoot = puppetRootObject.transform;
            Transform puppetBody = FindDirectChild(puppetRoot, "bone_Body");
            Transform puppetMagnet = FindDirectChild(puppetRoot, "bone_Magnet");
            Transform puppetSprites = FindDirectChild(puppetRoot, "Sprites");
            Transform bodyVisual = FindDirectChild(puppetSprites, "Body");
            Transform magnetVisual = FindDirectChild(puppetSprites, "Magnet");
            Renderer bodyRenderer = bodyVisual.GetComponent<Renderer>();
            Renderer magnetRenderer = magnetVisual.GetComponent<Renderer>();
            Require(bodyRenderer != null && bodyRenderer.enabled, "Visible Body renderer is missing or disabled.");
            Require(magnetRenderer != null && magnetRenderer.enabled, "Visible Magnet renderer is missing or disabled.");

            GameObject masterRootObject = InstantiatePrefab(masterPrefab, previewScene);
            UnpackCompletely(masterRootObject);
            masterRootObject.name = "CollectorRobot_Fly_Master";
            masterRootObject.transform.SetParent(finalRoot.transform, false);
            ResetRootTransform(masterRootObject.transform);
            masterRootObject.transform.SetParent(puppetBody, true);
            SetLayerRecursively(masterRootObject, enemyLayer);

            Transform masterRoot = masterRootObject.transform;
            Transform masterBody = FindDirectChild(masterRoot, "bone_Body");
            Transform masterMagnet = FindDirectChild(masterRoot, "bone_Magnet");

            puppetBody.gameObject.tag = EnemyTag;
            puppetMagnet.gameObject.tag = EnemyTag;
            Rigidbody2D bodyRigidbody = ConfigureRigidbody(puppetBody.gameObject, BodyMass,
                RigidbodyInterpolation2D.Interpolate, CollisionDetectionMode2D.Continuous);
            Rigidbody2D magnetRigidbody = ConfigureRigidbody(puppetMagnet.gameObject, MagnetMass,
                RigidbodyInterpolation2D.Interpolate, CollisionDetectionMode2D.Discrete);

            BoxCollider2D bodyCollider = puppetBody.gameObject.AddComponent<BoxCollider2D>();
            FitBoxCollider(bodyCollider, puppetBody, bodyRenderer.bounds, 0.92f);
            BoxCollider2D magnetCollider = puppetMagnet.gameObject.AddComponent<BoxCollider2D>();
            FitBoxCollider(magnetCollider, puppetMagnet, magnetRenderer.bounds, 0.9f);

            HingeJoint2D hinge = puppetMagnet.gameObject.AddComponent<HingeJoint2D>();
            hinge.connectedBody = bodyRigidbody;
            hinge.enableCollision = false;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector2.zero;
            hinge.connectedAnchor = bodyRigidbody.transform.InverseTransformPoint(hinge.transform.position);
            hinge.useMotor = false;
            hinge.useLimits = true;
            hinge.limits = new JointAngleLimits2D {
                min = LowerMagnetAngle,
                max = UpperMagnetAngle
            };
            hinge.breakForce = Mathf.Infinity;
            hinge.breakTorque = Mathf.Infinity;

            SimplePuppetBinder binder = finalRoot.AddComponent<SimplePuppetBinder>();
            binder.MasterRoot = masterRoot;
            binder.PuppetRoot = puppetRoot;
            binder.RotationSharpness = 0f;
            binder.Pairs = new List<SimplePuppetBinder.BonePair> {
                new SimplePuppetBinder.BonePair {
                    Master = masterBody,
                    Puppet = puppetBody,
                    PuppetBody2D = bodyRigidbody
                },
                new SimplePuppetBinder.BonePair {
                    Master = masterMagnet,
                    Puppet = puppetMagnet,
                    PuppetBody2D = magnetRigidbody
                }
            };

            RobotMemoryNew memory = finalRoot.AddComponent<RobotMemoryNew>();
            RobotHeartNew heart = finalRoot.AddComponent<RobotHeartNew>();
            RobotBrainNew brain = finalRoot.AddComponent<RobotBrainNew>();
            HealthBot health = finalRoot.AddComponent<HealthBot>();
            JointBreaker jointBreaker = finalRoot.AddComponent<JointBreaker>();
            RobotStateController state = finalRoot.AddComponent<RobotStateController>();
            CollectorFlightMotor2D motor = finalRoot.AddComponent<CollectorFlightMotor2D>();
            CollectorObstacleSensor2D sensor = finalRoot.AddComponent<CollectorObstacleSensor2D>();
            CollectorMagnetController2D magnetController = finalRoot.AddComponent<CollectorMagnetController2D>();
            CollectorFlightVisuals visuals = finalRoot.AddComponent<CollectorFlightVisuals>();
            CollectorRobotBodyController collectorBody = finalRoot.AddComponent<CollectorRobotBodyController>();
            CollectorRobotObservationBridge bridge = finalRoot.AddComponent<CollectorRobotObservationBridge>();
            CollectorPoolLifecycle lifecycle = finalRoot.AddComponent<CollectorPoolLifecycle>();

            Transform propellerPivot = FindDirectChild(puppetBody, "PropellerPivot");
            heart.ConfigureRole(RobotRole.Collector, resetStack: true);
            state.Stats = new EnemyRobotFactory().CreateRobot();
            state.Stats.RobotName = "Collector";
            motor.ConfigureReferences(bodyRigidbody, magnetRigidbody, sensor);
            sensor.ConfigureReferences(finalRoot.transform);
            magnetController.ConfigureReferences(bodyRigidbody, magnetRigidbody);
            visuals.ConfigureReferences(propellerPivot, motor);
            collectorBody.ConfigureReferences(bodyRigidbody, magnetRigidbody, masterMagnet,
                hinge, binder, motor, sensor, magnetController, visuals);
            bridge.ConfigureReferences(collectorBody, brain);
            lifecycle.ConfigureReferences(memory, brain, heart, state, jointBreaker, binder,
                collectorBody, magnetController, visuals, bridge);

            _ = health;

            bool success;
            PrefabUtility.SaveAsPrefabAsset(finalRoot, FinalPrefabPath, out success);
            Require(success, $"Unity failed to save final prefab '{FinalPrefabPath}'.");
        } finally {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void ConfigureMachinePrefab() {
        GameObject collectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(collectorPrefab != null, "Collector prefab could not be loaded for machine wiring.");

        GameObject machine = PrefabUtility.LoadPrefabContents(MachinePrefabPath);
        Require(machine != null, "Collector machine prefab could not be opened for runtime wiring.");
        try {
            SpawnRobotCollectorController controller = machine.GetComponent<SpawnRobotCollectorController>();
            Require(controller != null, "Collector machine prefab is missing SpawnRobotCollectorController.");
            Transform movingSpawnPoint = FindDirectChild(machine.transform, "SpawnPoint");
            Transform launchPoint = FindOrCreateDirectChild(movingSpawnPoint, "LaunchExitPoint");
            Transform dockPoint = FindOrCreateDirectChild(movingSpawnPoint, "DockApproachPoint");
            Transform machineIntakePoint = FindOrCreateDirectChild(movingSpawnPoint, "IntakePoint");
            Transform intakeZoneTransform = FindOrCreateDirectChild(movingSpawnPoint, "CollectorIntakeZone");

            ConfigureMarker(launchPoint, new Vector3(4f, 1f, 0f));
            ConfigureMarker(dockPoint, new Vector3(4f, 1f, 0f));
            ConfigureMarker(machineIntakePoint, Vector3.zero);
            ConfigureMarker(intakeZoneTransform, Vector3.zero);
            BoxCollider2D machineIntakeZone = intakeZoneTransform.GetComponent<BoxCollider2D>();
            if (machineIntakeZone == null)
                machineIntakeZone = intakeZoneTransform.gameObject.AddComponent<BoxCollider2D>();
            machineIntakeZone.isTrigger = true;
            machineIntakeZone.offset = Vector2.zero;
            machineIntakeZone.size = new Vector2(3.5f, 3.5f);

            controller.ConfigureMissionReferences(collectorPrefab, movingSpawnPoint, launchPoint,
                dockPoint, machineIntakePoint, machineIntakeZone);

            bool success;
            PrefabUtility.SaveAsPrefabAsset(machine, MachinePrefabPath, out success);
            Require(success, $"Unity failed to save machine prefab '{MachinePrefabPath}'.");
        } finally {
            PrefabUtility.UnloadPrefabContents(machine);
        }
    }

    private static void NormalizeMasterWorkingPrefab(int enemyLayer) {
        GameObject root = PrefabUtility.LoadPrefabContents(MasterPrefabPath);
        Require(root != null, "Master working prefab could not be opened for hierarchy normalization.");
        try {
            FindDirectChild(root.transform, "bone_Body");
            FindDirectChild(root.transform, "bone_Magnet");
            RemoveDeferredAndPhysicsComponents(root);
            SetLayerRecursively(root, enemyLayer);
            OrganizeDirectRenderers(root.transform, null, enemyLayer);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].enabled = false;
            }

            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, MasterPrefabPath, out success);
            Require(success, $"Unity failed to normalize working prefab '{MasterPrefabPath}'.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void NormalizePuppetWorkingPrefab(int enemyLayer) {
        GameObject root = PrefabUtility.LoadPrefabContents(PuppetPrefabPath);
        Require(root != null, "Puppet working prefab could not be opened for propeller normalization.");
        try {
            Transform bodyBone = FindDirectChild(root.transform, "bone_Body");
            Transform propellerPivot = FindDirectChild(bodyBone, "PropellerPivot");
            Transform helice = FindDirectChild(propellerPivot, "Helice");
            OrganizeDirectRenderers(root.transform, helice, enemyLayer);
            Vector3 heliceWorldPosition = helice.position;
            Quaternion heliceWorldRotation = helice.rotation;

            propellerPivot.position = heliceWorldPosition;
            propellerPivot.localRotation = Quaternion.identity;
            propellerPivot.localScale = Vector3.one;
            helice.SetPositionAndRotation(heliceWorldPosition, heliceWorldRotation);
            SetLayerRecursively(root, enemyLayer);

            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, PuppetPrefabPath, out success);
            Require(success, $"Unity failed to normalize working prefab '{PuppetPrefabPath}'.");
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateFallingAndAttachment(GameObject prefab) {
        Scene scene = CreatePhysicsValidationScene();
        try {
            PhysicsScene2D physicsScene = scene.GetPhysicsScene2D();
            GameObject instance = InstantiatePrefab(prefab, scene);
            instance.transform.position = new Vector3(0f, 4f, 0f);

            Transform puppetRoot = FindDirectChild(instance.transform, "CollectorRobot_Fly_Puppet");
            Transform body = FindDirectChild(puppetRoot, "bone_Body");
            Transform magnet = FindDirectChild(puppetRoot, "bone_Magnet");
            Transform masterRoot = FindDescendantExact(body, "CollectorRobot_Fly_Master");
            Transform propellerPivot = FindDirectChild(body, "PropellerPivot");
            Rigidbody2D bodyRigidbody = body.GetComponent<Rigidbody2D>();
            Rigidbody2D magnetRigidbody = magnet.GetComponent<Rigidbody2D>();
            HingeJoint2D hinge = magnet.GetComponent<HingeJoint2D>();
            BoxCollider2D bodyCollider = body.GetComponent<BoxCollider2D>();
            BoxCollider2D magnetCollider = magnet.GetComponent<BoxCollider2D>();
            float initialBodyY = bodyRigidbody.position.y;
            Vector3 masterLocalPosition = masterRoot.localPosition;
            Quaternion masterLocalRotation = masterRoot.localRotation;
            Vector3 propellerLocalPosition = propellerPivot.localPosition;
            Quaternion propellerLocalRotation = propellerPivot.localRotation;

            GameObject floor = new GameObject("CollectorRobotFly_ValidationFloor");
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(0f, 0f, 0f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(30f, 0.5f);

            Simulate(physicsScene, 300, 0.02f);

            Require(bodyRigidbody.position.y < initialBodyY - 1f,
                "Physics validation failed: the Collector body did not fall under gravity.");
            Require(bodyCollider.bounds.min.y > -0.15f,
                "Physics validation failed: the Collector body passed through the floor.");
            Require(magnetCollider.bounds.min.y > -0.2f,
                "Physics validation failed: the Collector magnet passed through the floor.");
            Vector3 hingeAnchor = hinge.transform.TransformPoint(hinge.anchor);
            Vector3 connectedAnchor = bodyRigidbody.transform.TransformPoint(hinge.connectedAnchor);
            Require(Vector3.Distance(hingeAnchor, connectedAnchor) < 0.08f,
                "Physics validation failed: the magnet separated from its hinge anchor.");
            Require(Approximately(masterRoot.localPosition, masterLocalPosition),
                "Physics validation failed: the nested master changed its body-relative position.");
            Require(Approximately(masterRoot.localRotation, masterLocalRotation),
                "Physics validation failed: the nested master changed its body-relative rotation.");
            Require(Approximately(propellerPivot.localPosition, propellerLocalPosition),
                "Physics validation failed: the propeller pivot did not remain attached to the body.");
            Require(Approximately(propellerPivot.localRotation, propellerLocalRotation),
                "Physics validation failed: the propeller pivot changed its neutral rotation.");
            Require(Vector2.Distance(bodyRigidbody.position, magnetRigidbody.position) < 5f,
                "Physics validation failed: the magnet was lost from the Collector body.");
        } finally {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void ValidateHingeLimitSimulation(GameObject prefab) {
        float positiveTorqueLimit = SimulateHingeToLimit(prefab, 5f);
        float negativeTorqueLimit = SimulateHingeToLimit(prefab, -5f);
        Require(Mathf.Abs(positiveTorqueLimit) >= 80f && Mathf.Abs(positiveTorqueLimit) <= 92f,
            $"Physics validation failed: positive-torque hinge stop was {positiveTorqueLimit:F2} degrees instead of approximately 90.");
        Require(Mathf.Abs(negativeTorqueLimit) >= 80f && Mathf.Abs(negativeTorqueLimit) <= 92f,
            $"Physics validation failed: negative-torque hinge stop was {negativeTorqueLimit:F2} degrees instead of approximately 90.");
        Require(Mathf.Sign(positiveTorqueLimit) != Mathf.Sign(negativeTorqueLimit),
            $"Physics validation failed: hinge stops did not cover both sides of the arc ({positiveTorqueLimit:F2}, {negativeTorqueLimit:F2}).");
    }

    private static float SimulateHingeToLimit(GameObject prefab, float torque) {
        Scene scene = CreatePhysicsValidationScene();
        try {
            PhysicsScene2D physicsScene = scene.GetPhysicsScene2D();
            GameObject instance = InstantiatePrefab(prefab, scene);
            Transform puppetRoot = FindDirectChild(instance.transform, "CollectorRobot_Fly_Puppet");
            Transform body = FindDirectChild(puppetRoot, "bone_Body");
            Transform magnet = FindDirectChild(puppetRoot, "bone_Magnet");
            Rigidbody2D bodyRigidbody = body.GetComponent<Rigidbody2D>();
            Rigidbody2D magnetRigidbody = magnet.GetComponent<Rigidbody2D>();
            HingeJoint2D hinge = magnet.GetComponent<HingeJoint2D>();
            SimplePuppetBinder binder = instance.GetComponent<SimplePuppetBinder>();

            binder.enabled = false;
            bodyRigidbody.gravityScale = 0f;
            magnetRigidbody.gravityScale = 0f;
            bodyRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
            bodyRigidbody.linearVelocity = Vector2.zero;
            bodyRigidbody.angularVelocity = 0f;
            magnetRigidbody.linearVelocity = Vector2.zero;
            magnetRigidbody.angularVelocity = 0f;

            for (int i = 0; i < 300; i++) {
                magnetRigidbody.AddTorque(torque, ForceMode2D.Force);
                Require(physicsScene.Simulate(0.02f), "Unity refused to simulate the Collector hinge validation scene.");
            }

            return hinge.jointAngle;
        } finally {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Scene CreatePhysicsValidationScene() {
        Scene scene = EditorSceneManager.NewPreviewScene();
        Require(scene.IsValid(), "Unity failed to create an isolated 2D physics validation scene.");
        Require(scene.GetPhysicsScene2D().IsValid(), "The isolated validation scene has no valid 2D physics scene.");
        return scene;
    }

    private static void Simulate(PhysicsScene2D physicsScene, int steps, float stepTime) {
        for (int i = 0; i < steps; i++) {
            Require(physicsScene.Simulate(stepTime), "Unity refused to simulate the Collector physics validation scene.");
        }
    }

    private static Rigidbody2D ConfigureRigidbody(GameObject target, float mass,
        RigidbodyInterpolation2D interpolation, CollisionDetectionMode2D collisionDetection) {
        Rigidbody2D rigidbody = target.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Dynamic;
        rigidbody.simulated = true;
        rigidbody.useAutoMass = false;
        rigidbody.mass = mass;
        rigidbody.linearDamping = LinearDamping;
        rigidbody.angularDamping = AngularDamping;
        rigidbody.gravityScale = GravityScale;
        rigidbody.interpolation = interpolation;
        rigidbody.collisionDetectionMode = collisionDetection;
        rigidbody.sleepMode = RigidbodySleepMode2D.StartAwake;
        rigidbody.constraints = RigidbodyConstraints2D.None;
        rigidbody.sharedMaterial = null;
        return rigidbody;
    }

    private static void FitBoxCollider(BoxCollider2D collider, Transform localSpace, Bounds worldBounds,
        float sizeMultiplier) {
        Require(worldBounds.size.x > 0f && worldBounds.size.y > 0f,
            $"Renderer bounds for '{localSpace.name}' are empty.");

        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
        Vector3 worldMin = worldBounds.min;
        Vector3 worldMax = worldBounds.max;
        Vector3[] corners = {
            new Vector3(worldMin.x, worldMin.y, localSpace.position.z),
            new Vector3(worldMin.x, worldMax.y, localSpace.position.z),
            new Vector3(worldMax.x, worldMin.y, localSpace.position.z),
            new Vector3(worldMax.x, worldMax.y, localSpace.position.z)
        };

        for (int i = 0; i < corners.Length; i++) {
            Vector3 localPoint = localSpace.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, localPoint);
            max = Vector3.Max(max, localPoint);
        }

        Vector2 size = new Vector2(max.x - min.x, max.y - min.y) * sizeMultiplier;
        collider.offset = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        collider.size = new Vector2(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f));
        collider.isTrigger = false;
        collider.usedByEffector = false;
        collider.sharedMaterial = null;
    }

    private static void ValidateRigidbody(Rigidbody2D rigidbody, float expectedMass,
        RigidbodyInterpolation2D expectedInterpolation, CollisionDetectionMode2D expectedCollision,
        string label) {
        Require(rigidbody.bodyType == RigidbodyType2D.Dynamic, $"Collector {label} must be Dynamic.");
        Require(rigidbody.simulated, $"Collector {label} must be simulated.");
        Require(!rigidbody.useAutoMass, $"Collector {label} must use an explicit mass.");
        Require(Mathf.Approximately(rigidbody.mass, expectedMass), $"Collector {label} mass is incorrect.");
        Require(Mathf.Approximately(rigidbody.linearDamping, LinearDamping),
            $"Collector {label} linear damping is incorrect.");
        Require(Mathf.Approximately(rigidbody.angularDamping, AngularDamping),
            $"Collector {label} angular damping is incorrect.");
        Require(Mathf.Approximately(rigidbody.gravityScale, GravityScale),
            $"Collector {label} gravity scale is incorrect.");
        Require(rigidbody.interpolation == expectedInterpolation, $"Collector {label} interpolation is incorrect.");
        Require(rigidbody.collisionDetectionMode == expectedCollision,
            $"Collector {label} collision detection is incorrect.");
        Require(rigidbody.constraints == RigidbodyConstraints2D.None,
            $"Collector {label} must not freeze rotation or position.");
    }

    private static void ValidatePair(SimplePuppetBinder.BonePair pair, Transform expectedMaster,
        Transform expectedPuppet, Rigidbody2D expectedBody, string label) {
        Require(pair != null, $"Binder {label} pair is null.");
        Require(pair.Master == expectedMaster, $"Binder {label} Master reference is incorrect.");
        Require(pair.Puppet == expectedPuppet, $"Binder {label} Puppet reference is incorrect.");
        Require(pair.PuppetBody2D == expectedBody, $"Binder {label} Rigidbody2D cache is not explicitly assigned.");
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Scene destinationScene) {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, destinationScene) as GameObject;
        Require(instance != null, $"Unity failed to instantiate prefab '{prefab.name}'.");
        return instance;
    }

    private static void UnpackCompletely(GameObject instance) {
        if (PrefabUtility.GetPrefabInstanceStatus(instance) == PrefabInstanceStatus.NotAPrefab) {
            return;
        }

        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);
    }

    private static void RemoveDeferredAndPhysicsComponents(GameObject root) {
        DestroyComponents(root.GetComponentsInChildren<Joint2D>(true));
        DestroyComponents(root.GetComponentsInChildren<Collider2D>(true));
        DestroyComponents(root.GetComponentsInChildren<Rigidbody2D>(true));
        DestroyComponents(root.GetComponentsInChildren<Animator>(true));
        DestroyComponents(root.GetComponentsInChildren<SimplePuppetBinder>(true));
    }

    private static void DestroyComponents<T>(T[] components) where T : Component {
        for (int i = 0; i < components.Length; i++) {
            UnityEngine.Object.DestroyImmediate(components[i]);
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer) {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++) {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static Transform OrganizeDirectRenderers(Transform root, Transform excludedRenderer,
        int layer) {
        Transform sprites = FindOptionalDirectChild(root, "Sprites");
        if (sprites == null) {
            GameObject spritesObject = new GameObject("Sprites");
            SceneManager.MoveGameObjectToScene(spritesObject, root.gameObject.scene);
            spritesObject.layer = layer;
            sprites = spritesObject.transform;
            sprites.SetParent(root, false);
        }

        sprites.localPosition = Vector3.zero;
        sprites.localRotation = Quaternion.identity;
        sprites.localScale = Vector3.one;
        sprites.gameObject.layer = layer;

        List<Transform> renderObjects = new List<Transform>();
        for (int i = 0; i < root.childCount; i++) {
            Transform child = root.GetChild(i);
            if (child == sprites || child == excludedRenderer) {
                continue;
            }

            if (child.GetComponent<Renderer>() != null) {
                renderObjects.Add(child);
            }
        }

        for (int i = 0; i < renderObjects.Count; i++) {
            renderObjects[i].SetParent(sprites, true);
            renderObjects[i].gameObject.layer = layer;
        }

        return sprites;
    }

    private static void ResetRootTransform(Transform root) {
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
    }

    private static Transform FindDirectChild(Transform parent, string childName) {
        Transform result = null;
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name != childName) {
                continue;
            }

            Require(result == null, $"Found more than one direct child named '{childName}' under '{parent.name}'.");
            result = child;
        }

        Require(result != null, $"Could not find direct child '{childName}' under '{parent.name}'.");
        return result;
    }

    private static Transform FindOptionalDirectChild(Transform parent, string childName) {
        Transform result = null;
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name != childName) {
                continue;
            }

            Require(result == null, $"Found more than one direct child named '{childName}' under '{parent.name}'.");
            result = child;
        }

        return result;
    }

    private static Transform FindOrCreateDirectChild(Transform parent, string childName) {
        Transform result = FindOptionalDirectChild(parent, childName);
        if (result != null)
            return result;

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        return child.transform;
    }

    private static void ConfigureMarker(Transform marker, Vector3 localPosition) {
        marker.localPosition = localPosition;
        marker.localRotation = Quaternion.identity;
        marker.localScale = Vector3.one;
    }

    private static Transform FindDescendantExact(Transform root, string objectName) {
        Transform result = null;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++) {
            if (transforms[i] == root || transforms[i].name != objectName) {
                continue;
            }

            Require(result == null, $"Found more than one descendant named '{objectName}' under '{root.name}'.");
            result = transforms[i];
        }

        Require(result != null, $"Could not find descendant '{objectName}' under '{root.name}'.");
        return result;
    }

    private static bool Approximately(Vector3 left, Vector3 right) {
        return (left - right).sqrMagnitude < 0.000001f;
    }

    private static bool Approximately(Quaternion left, Quaternion right) {
        return Quaternion.Angle(left, right) < 0.001f;
    }

    private static T RequireSingleRootComponent<T>(GameObject root) where T : Component {
        T[] components = root.GetComponents<T>();
        Require(components.Length == 1,
            $"Expected exactly one root {typeof(T).Name}, found {components.Length}.");
        return components[0];
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}

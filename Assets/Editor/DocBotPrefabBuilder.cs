using System;
using System.Collections.Generic;
using System.Linq;
using CowBoya.Robots;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>
/// Imports the DocBot PSD with the Worker rig, then builds the laboratory-only
/// Master/Puppet robot and wires it into ROOM_Laboratory_1.
/// </summary>
public static class DocBotPrefabBuilder {
    public const string DocBotPsdPath =
        "Assets/Resources/Prefabs/Robots/RobotPackage/RobotDrawings/DocBot/DocBot.psd";
    public const string WorkerPsdPath =
        "Assets/Resources/Prefabs/Robots/RobotPackage/RobotDrawings/Worker/Format PSD/Worker_Base.psd";
    public const string WorkerMasterPrefabPath =
        "Assets/Resources/Prefabs/Robots/RobotPackage/RobotDrawings/Worker/Format PSD/Worker_Master.prefab";
    public const string WorkerPuppetPrefabPath =
        "Assets/Resources/Prefabs/Robots/RobotPackage/RobotDrawings/Worker/Format PSD/Worker_Puppet.prefab";
    public const string WorkerFinalPrefabPath =
        "Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab";
    public const string FinalPrefabPath =
        "Assets/Resources/Prefabs/Robots/DocBot/DocBot.prefab";
    public const string MasterPrefabPath =
        "Assets/Resources/Prefabs/Robots/DocBot/Others/DocBot_Master.prefab";
    public const string PuppetPrefabPath =
        "Assets/Resources/Prefabs/Robots/DocBot/Others/DocBot_Puppet.prefab";
    public const string AnimatorControllerPath =
        "Assets/Resources/Prefabs/Robots/DocBot/DocBot_Animator.controller";

    public const string LaboratoryRoomPrefabPath =
        "Assets/Resources/Prefabs/Map/ROOM_Laboratory_1.prefab";
    public const string WhiteCubePrefabPath =
        "Assets/Resources/Prefabs/IntereableObjects/CubeNormal.prefab";

    private const string EnemyTag = "Enemy";
    private const string EnemyLayerName = "Enemy";
    private const string WorkerMasterName = "Worker_Master";
    private const string WorkerPuppetName = "Worker_Puppet";
    private const string DocBotMasterName = "DocBot_Master";
    private const string DocBotPuppetName = "DocBot_Puppet";
    private const string WorkerRoomsTriggerZoneName = "RoomsTriggerZone";
    private const string ReceiverTriggerName = "DocBotJunkReceiverTrigger";
    private const float ReceiverWorldPadding = 1.5f;

    // Worker3 leaves these Rigidbody2D caches empty and lets the binder discover
    // them at runtime. Generated DocBot assets author the complete mapping instead,
    // both to make the prefab deterministic and to avoid a first-frame null cache.
    private static readonly string[] BinderBoneNames = {
        "Hips_Bone",
        "Torso_Bone",
        "BodyLow_Bone",
        "Body_Bone",
        "Head_Bone",
        "LHand_Bone",
        "RHand_Bone",
        "RFoot_Bone",
        "LFoot_Bone",
        "RLeg_Bone",
        "LLeg_Bone",
        "LArm_Bone",
        "RArm_Bone"
    };

    private static readonly AnimatorParameterContract[] AnimatorParameters = {
        new AnimatorParameterContract("Direction", AnimatorControllerParameterType.Float),
        new AnimatorParameterContract("Speed", AnimatorControllerParameterType.Float),
        new AnimatorParameterContract("VerticalDirection", AnimatorControllerParameterType.Float),
        new AnimatorParameterContract("IsWalking", AnimatorControllerParameterType.Bool),
        new AnimatorParameterContract("IsJumping", AnimatorControllerParameterType.Bool),
        new AnimatorParameterContract("IsCrouching", AnimatorControllerParameterType.Bool),
        new AnimatorParameterContract("IsVerticalWalking", AnimatorControllerParameterType.Bool)
    };

    // Do not simplify this into a name-based lookup. Several PSD layers have the
    // same name (and some names contain a significant trailing space). DocBot also
    // puts its three upper-body layers at different IDs than Worker.
    private static readonly LayerMapping[] LayerMappings = {
        new LayerMapping(3, "LHand ", 3, "LHand "),
        new LayerMapping(4, "LHand ", 4, "LHand "),
        new LayerMapping(5, "RArm ", 5, "RArm "),
        new LayerMapping(6, "RArm ", 6, "RArm "),
        new LayerMapping(7, "RFoot", 7, "RFoot"),
        new LayerMapping(8, "RFoot", 8, "RFoot"),
        new LayerMapping(9, "UpRLeg", 9, "UpRLeg"),
        new LayerMapping(10, "UpRLeg", 10, "UpRLeg"),
        new LayerMapping(11, "Torso", 11, "Torso"),
        new LayerMapping(12, "Hips", 12, "Hips"),
        new LayerMapping(13, "BodyLow ", 13, "BodyLow "),
        new LayerMapping(16, "Calque 196", 14, "Calque 162"),
        new LayerMapping(14, "Head", 15, "Head"),
        new LayerMapping(15, "BodyUp", 16, "BodyUp")
    };

    private sealed class LayerMapping {
        public LayerMapping(
            int workerLayerId,
            string workerLayerName,
            int docBotLayerId,
            string docBotLayerName) {
            WorkerLayerId = workerLayerId;
            WorkerLayerName = workerLayerName;
            DocBotLayerId = docBotLayerId;
            DocBotLayerName = docBotLayerName;
        }

        public int WorkerLayerId { get; }
        public string WorkerLayerName { get; }
        public int DocBotLayerId { get; }
        public string DocBotLayerName { get; }
    }

    private sealed class ImportedLayer {
        public ImportedLayer(int layerId, string name, GUID spriteId) {
            LayerId = layerId;
            Name = name;
            SpriteId = spriteId;
        }

        public int LayerId { get; }
        public string Name { get; }
        public GUID SpriteId { get; }
    }

    private sealed class AnimatorParameterContract {
        public AnimatorParameterContract(
            string name,
            AnimatorControllerParameterType type) {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public AnimatorControllerParameterType Type { get; }
    }

    /// <summary>
    /// Reimports the DocBot drawing, transfers the Worker rig, creates all three
    /// prefabs, and configures the recurring laboratory room.
    /// </summary>
    [MenuItem("Tools/CowBoya/Build DocBot Master Puppet")]
    public static void BuildAndValidate() {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        Require(enemyLayer >= 0, $"Required layer '{EnemyLayerName}' does not exist.");

        EnsureAssetFolder("Assets/Resources/Prefabs/Robots/DocBot/Others");
        PSDImporter docBotImporter = EnsureDocBotImporter();
        TransferWorkerRig(docBotImporter);
        RuntimeAnimatorController controller = EnsureAnimatorController();

        BuildMasterPrefab(enemyLayer, controller);
        BuildPuppetPrefab(enemyLayer);
        BuildFinalPrefab(enemyLayer, controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureLaboratoryRoom();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ValidateGeneratedAssets();
        Debug.Log($"DocBot Master/Puppet prefab built successfully at '{FinalPrefabPath}'.");
    }

    /// <summary>
    /// Validates the imported rig, generated prefabs, runtime wiring, and room link.
    /// </summary>
    [MenuItem("Tools/CowBoya/Validate DocBot Master Puppet")]
    public static void ValidateGeneratedAssets() {
        ValidateImportedRig();

        Require(AssetDatabase.LoadAssetAtPath<GameObject>(MasterPrefabPath) != null,
            $"Missing DocBot Master prefab at '{MasterPrefabPath}'.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(PuppetPrefabPath) != null,
            $"Missing DocBot Puppet prefab at '{PuppetPrefabPath}'.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        Require(prefab != null, $"Missing DocBot prefab at '{FinalPrefabPath}'.");
        Require(prefab.name == "DocBot", "The final prefab root must be named DocBot.");
        Require(prefab.tag == EnemyTag, "DocBot must retain the Enemy tag.");
        Require(prefab.layer == LayerMask.NameToLayer(EnemyLayerName),
            "DocBot must use the Enemy layer.");
        Require(Approximately(prefab.transform.localPosition, Vector3.zero),
            "DocBot prefab root position must be zero.");
        Require(Approximately(prefab.transform.localRotation, Quaternion.identity),
            "DocBot prefab root rotation must be identity.");
        Require(Approximately(prefab.transform.localScale, new Vector3(0.5f, 0.5f, 1f)),
            "DocBot must retain Worker3's authored 0.5, 0.5, 1 scale.");

        Transform puppetRoot = FindUniqueDescendant(prefab.transform, DocBotPuppetName);
        Transform masterRoot = FindUniqueDescendant(prefab.transform, DocBotMasterName);
        Require(masterRoot.IsChildOf(puppetRoot),
            "DocBot Master must remain parented below the physical Puppet hierarchy.");

        Rigidbody2D[] bodies = prefab.GetComponentsInChildren<Rigidbody2D>(true);
        BoxCollider2D[] bodyColliders = prefab.GetComponentsInChildren<BoxCollider2D>(true);
        HingeJoint2D[] hinges = prefab.GetComponentsInChildren<HingeJoint2D>(true);
        Require(bodies.Length == 13,
            $"DocBot must retain exactly 13 Worker rigidbodies; found {bodies.Length}.");
        Require(bodyColliders.Length == 13,
            $"DocBot must retain exactly 13 Worker body colliders; found {bodyColliders.Length}.");
        Require(hinges.Length == 12,
            $"DocBot must retain exactly 12 Worker hinges; found {hinges.Length}.");

        SimplePuppetBinder binder = RequireSingle<SimplePuppetBinder>(prefab);
        Require(binder.MasterRoot == masterRoot, "DocBot binder has the wrong MasterRoot.");
        Require(binder.PuppetRoot == puppetRoot, "DocBot binder has the wrong PuppetRoot.");
        Require(binder.Pairs != null && binder.Pairs.Count == BinderBoneNames.Length,
            "DocBot binder must retain all 13 Worker bone pairs.");
        HashSet<Rigidbody2D> pairedBodies = new HashSet<Rigidbody2D>();
        for (int i = 0; i < binder.Pairs.Count; i++) {
            SimplePuppetBinder.BonePair pair = binder.Pairs[i];
            string boneName = BinderBoneNames[i];
            Transform expectedMaster = FindUniqueDescendant(masterRoot, boneName);
            Transform expectedPuppet = FindUniqueDescendantOutside(
                puppetRoot,
                boneName,
                masterRoot);
            Rigidbody2D expectedBody = expectedPuppet.GetComponent<Rigidbody2D>();

            Require(pair != null,
                $"DocBot binder pair {i} ('{boneName}') is null.");
            Require(pair.Master == expectedMaster,
                $"DocBot binder pair {i} must target Master/{boneName}.");
            Require(pair.Puppet == expectedPuppet,
                $"DocBot binder pair {i} must target physical Puppet/{boneName}.");
            Require(expectedBody != null,
                $"Physical Puppet bone '{boneName}' has no Rigidbody2D.");
            Require(pair.PuppetBody2D == expectedBody,
                $"DocBot binder pair {i} has the wrong cached Rigidbody2D.");
            Require(pairedBodies.Add(pair.PuppetBody2D),
                $"DocBot binder pair {i} reuses another pair's Rigidbody2D.");
        }

        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        Require(renderers.Length == LayerMappings.Length * 2,
            $"Expected 28 DocBot SpriteRenderers; found {renderers.Length}.");
        for (int i = 0; i < renderers.Length; i++) {
            SpriteRenderer renderer = renderers[i];
            Require(renderer.sprite != null, $"Renderer '{renderer.name}' has no sprite.");
            Require(AssetDatabase.GetAssetPath(renderer.sprite) == DocBotPsdPath,
                $"Renderer '{renderer.name}' still references a non-DocBot sprite.");
            if (renderer.transform.IsChildOf(masterRoot)) {
                Require(!renderer.enabled,
                    $"Master renderer '{renderer.name}' must remain disabled.");
            }
            else {
                Require(renderer.enabled,
                    $"Puppet renderer '{renderer.name}' must remain visible.");
            }
        }

        SpriteSkin[] spriteSkins = prefab.GetComponentsInChildren<SpriteSkin>(true);
        Require(spriteSkins.Length == renderers.Length,
            $"Every DocBot renderer needs one SpriteSkin; found {spriteSkins.Length} skins "
            + $"for {renderers.Length} renderers.");
        for (int i = 0; i < spriteSkins.Length; i++) {
            SpriteSkin skin = spriteSkins[i];
            Require(skin.rootBone != null,
                $"SpriteSkin '{skin.name}' has no root bone.");
            Require(skin.boneTransforms != null && skin.boneTransforms.Length == 1
                && skin.boneTransforms[0] != null,
                $"SpriteSkin '{skin.name}' must retain one rigid bone transform.");
        }

        Animator animator = RequireSingle<Animator>(prefab);
        AnimatorController expectedController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        Require(expectedController != null, "DocBot Animator Controller asset is missing.");
        Require(animator.runtimeAnimatorController == expectedController,
            "DocBot Animator must use its generated controller.");
        ValidateAnimatorController(expectedController);

        Require(prefab.GetComponentsInChildren<RobotBrainNew>(true).Length == 0,
            "DocBot must not contain the Worker planning brain.");
        Require(prefab.GetComponentsInChildren<RobotHeartNew>(true).Length == 0,
            "DocBot must not contain the Worker task heart.");
        Require(prefab.GetComponentsInChildren<RobotAttackController>(true).Length == 0,
            "DocBot must never contain a combat controller.");
        Require(prefab.GetComponentsInChildren<EnemyArmTargetController>(true).Length == 0,
            "DocBot must not contain an enemy attack arm target.");
        Require(prefab.GetComponentsInChildren<FollowEnemyTriggerHandler>(true).Length == 0,
            "DocBot must not contain Worker follow AI.");
        Require(prefab.GetComponentsInChildren<PooledEnemy>(true).Length == 0,
            "DocBot is spawned by LaboratoryManager and must not retain Worker pool AI glue.");
        Require(prefab.GetComponentsInChildren<PositionTriggerZone>(true).Length == 0,
            "DocBot must not retain Worker's unused RoomsTriggerZone polling component.");

        RobotStateController state = RequireSingle<RobotStateController>(prefab);
        HealthBot health = RequireSingle<HealthBot>(prefab);
        JointBreaker breaker = RequireSingle<JointBreaker>(prefab);
        DamageFeedback feedback = RequireSingle<DamageFeedback>(prefab);
        EnemyGrabbable grabbable = RequireSingle<EnemyGrabbable>(prefab);
        DocBotItemHolder holder = RequireSingle<DocBotItemHolder>(prefab);
        DocBotHandReachController reach = RequireSingle<DocBotHandReachController>(prefab);
        DocBotController controller = RequireSingle<DocBotController>(prefab);
        DocBotJunkReceiver receiver = RequireSingle<DocBotJunkReceiver>(prefab);
        DeadRobotCollectable collectable = RequireSingle<DeadRobotCollectable>(prefab);

        Require(state.Health == health, "DocBot state must reference its HealthBot.");
        Require(health.DamageFeedback == feedback,
            "DocBot HealthBot must reference the Puppet DamageFeedback.");
        Require(controller.StateController == state,
            "DocBotController has the wrong RobotStateController.");
        Require(controller.ItemHolder == holder,
            "DocBotController has the wrong item holder.");
        Require(holder.LeftHandAnchor != null && holder.LeftHandAnchor.name == "LHand_Bone",
            "DocBot left item anchor must be the physical left hand.");
        Require(holder.RightHandAnchor != null && holder.RightHandAnchor.name == "RHand_Bone",
            "DocBot right item anchor must be the physical right hand.");
        Require(!holder.LeftHandAnchor.IsChildOf(masterRoot)
            && !holder.RightHandAnchor.IsChildOf(masterRoot),
            "DocBot items must attach to Puppet hands, not Master hands.");
        Require(grabbable.PausesBehaviour(receiver),
            "Grabbing DocBot must pause its junk receiver.");
        Require(grabbable.PausesBehaviour(reach),
            "Grabbing DocBot must pause its procedural reach.");
        Require(grabbable.PausesBehaviour(binder),
            "Grabbing DocBot must pause its Master/Puppet binder.");

        CircleCollider2D receiverCollider = receiver.GetComponent<CircleCollider2D>();
        Require(receiverCollider != null && receiverCollider.isTrigger,
            "DocBot junk receiver needs one CircleCollider2D trigger.");
        Require(receiver.gameObject.name == ReceiverTriggerName,
            "DocBot junk receiver is on the wrong GameObject.");
        Require(Mathf.Approximately(receiver.AcceptanceDelay, 1f),
            "DocBot junk acceptance delay must remain one second.");
        Require(collectable.GetComponent<RobotStateController>() == state,
            "DeadRobotCollectable must be on the DocBot root.");

        Component[] rootComponents = prefab.GetComponents<Component>();
        Require(Array.IndexOf(rootComponents, controller) < Array.IndexOf(rootComponents, collectable),
            "DeadRobotCollectable must be authored after DocBotController.");

        ValidateSerializedObjectReference(reach, "leftArmSolverTarget",
            FindUniqueDescendant(masterRoot, "LArm_Solver_Target"));
        ValidateSerializedObjectReference(reach, "rightArmSolverTarget",
            FindUniqueDescendant(masterRoot, "RArm_Solver_Target"));
        ValidateSerializedObjectReference(receiver, "docBot", controller);
        ValidateSerializedObjectReference(receiver, "itemHolder", holder);
        ValidateSerializedObjectReference(receiver, "handReach", reach);
        ValidateLaboratoryRoom(controller);
        _ = breaker;
    }

    private static PSDImporter EnsureDocBotImporter() {
        Require(AssetDatabase.LoadMainAssetAtPath(DocBotPsdPath) != null,
            $"Missing DocBot PSD at '{DocBotPsdPath}'.");
        Require(AssetImporter.GetAtPath(WorkerPsdPath) is PSDImporter,
            $"Worker PSD at '{WorkerPsdPath}' is not imported by PSDImporter.");

        if (!(AssetImporter.GetAtPath(DocBotPsdPath) is PSDImporter)) {
            AssetDatabase.SetImporterOverride<PSDImporter>(DocBotPsdPath);
            AssetDatabase.ImportAsset(
                DocBotPsdPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        PSDImporter importer = AssetImporter.GetAtPath(DocBotPsdPath) as PSDImporter;
        Require(importer != null,
            "Unity could not activate PSDImporter for DocBot.psd. Check the 2D PSD Importer package.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.useMosaicMode = true;
        importer.useCharacterMode = true;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteMeshType = SpriteMeshType.Tight;
        importer.mipmapEnabled = true;

        SerializedObject serializedImporter = new SerializedObject(importer);
        SetRequiredBool(serializedImporter, false, "m_ImportHiddenLayers", "importHiddenLayers");
        SetRequiredBool(serializedImporter, false, "m_GeneratePhysicsShape", "generatePhysicsShape");
        SetRequiredBool(serializedImporter, true,
            "m_KeepDupilcateSpriteName", "keepDupilcateSpriteName");
        SetRequiredInt(serializedImporter, 2, "m_LayerMappingOption", "layerMappingOption");
        SetRequiredInt(serializedImporter, (int)SpriteAlignment.BottomCenter,
            "m_DocumentAlignment", "documentAlignment");
        SetRequiredVector2(serializedImporter, new Vector2(0.5f, 0f),
            "m_DocumentPivot", "documentPivot");
        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        importer = AssetImporter.GetAtPath(DocBotPsdPath) as PSDImporter;
        Require(importer != null, "DocBot PSDImporter disappeared after reimport.");
        ValidateLayerContract(ReadImportedLayers(importer), isWorker: false);
        return importer;
    }

    private static void TransferWorkerRig(PSDImporter docBotImporter) {
        PSDImporter workerImporter = AssetImporter.GetAtPath(WorkerPsdPath) as PSDImporter;
        Require(workerImporter != null, "Worker PSDImporter is unavailable.");

        Dictionary<int, ImportedLayer> workerLayers = ReadImportedLayers(workerImporter);
        Dictionary<int, ImportedLayer> docBotLayers = ReadImportedLayers(docBotImporter);
        ValidateLayerContract(workerLayers, isWorker: true);
        ValidateLayerContract(docBotLayers, isWorker: false);

        ISpriteEditorDataProvider workerProvider = workerImporter;
        ISpriteEditorDataProvider docBotProvider = docBotImporter;
        workerProvider.InitSpriteEditorDataProvider();
        docBotProvider.InitSpriteEditorDataProvider();

        ICharacterDataProvider workerCharacterProvider =
            workerProvider.GetDataProvider<ICharacterDataProvider>();
        ICharacterDataProvider docBotCharacterProvider =
            docBotProvider.GetDataProvider<ICharacterDataProvider>();
        ISpriteBoneDataProvider workerBoneProvider =
            workerProvider.GetDataProvider<ISpriteBoneDataProvider>();
        ISpriteBoneDataProvider docBotBoneProvider =
            docBotProvider.GetDataProvider<ISpriteBoneDataProvider>();
        ISpriteMeshDataProvider docBotMeshProvider =
            docBotProvider.GetDataProvider<ISpriteMeshDataProvider>();

        Require(workerCharacterProvider != null && docBotCharacterProvider != null,
            "Character data providers require Character Mode on both PSD importers.");
        Require(workerBoneProvider != null && docBotBoneProvider != null
            && docBotMeshProvider != null,
            "The PSD importer did not expose the required bone and mesh data providers.");

        CharacterData workerCharacter = workerCharacterProvider.GetCharacterData();
        CharacterData docBotCharacter = docBotCharacterProvider.GetCharacterData();
        Require(workerCharacter.bones != null && workerCharacter.bones.Length == 13,
            "Worker source rig must contain exactly 13 character bones.");
        Require(workerCharacter.parts != null
            && workerCharacter.parts.Length == LayerMappings.Length,
            "Worker source rig must contain exactly 14 character parts.");
        Require(docBotCharacter.parts != null
            && docBotCharacter.parts.Length == LayerMappings.Length,
            "DocBot import must contain exactly 14 visible character parts.");
        Require(workerCharacter.dimension == docBotCharacter.dimension,
            $"Worker and DocBot canvases must have identical dimensions for a direct rig transfer "
            + $"(Worker {workerCharacter.dimension}, DocBot {docBotCharacter.dimension}).");

        CharacterPart[] destinationParts = docBotCharacter.parts.ToArray();
        Dictionary<string, CharacterPart> workerParts = workerCharacter.parts
            .ToDictionary(part => part.spriteId, part => part, StringComparer.Ordinal);
        Dictionary<string, int> destinationPartIndices = destinationParts
            .Select((part, index) => new { part.spriteId, index })
            .ToDictionary(pair => pair.spriteId, pair => pair.index, StringComparer.Ordinal);

        for (int i = 0; i < LayerMappings.Length; i++) {
            LayerMapping mapping = LayerMappings[i];
            ImportedLayer workerLayer = workerLayers[mapping.WorkerLayerId];
            ImportedLayer docBotLayer = docBotLayers[mapping.DocBotLayerId];
            string workerSpriteId = workerLayer.SpriteId.ToString();
            string docBotSpriteId = docBotLayer.SpriteId.ToString();

            Require(workerParts.TryGetValue(workerSpriteId, out CharacterPart workerPart),
                $"Worker character data has no part for layer {mapping.WorkerLayerId}.");
            Require(destinationPartIndices.TryGetValue(docBotSpriteId, out int destinationIndex),
                $"DocBot character data has no part for layer {mapping.DocBotLayerId}.");
            Require(workerPart.bones != null && workerPart.bones.Length == 1,
                $"Worker layer {mapping.WorkerLayerId} must use exactly one rigid bone.");

            CharacterPart destinationPart = destinationParts[destinationIndex];
            destinationPart.bones = workerPart.bones.ToArray();
            destinationParts[destinationIndex] = destinationPart;

            List<SpriteBone> sourceBones = workerBoneProvider.GetBones(workerLayer.SpriteId);
            Require(sourceBones != null && sourceBones.Count == 1,
                $"Worker layer {mapping.WorkerLayerId} must expose one per-sprite bone.");

            Vector2Int spritePositionDelta =
                workerPart.spritePosition.position - destinationPart.spritePosition.position;
            List<SpriteBone> destinationBones = new List<SpriteBone>(sourceBones.Count);
            for (int boneIndex = 0; boneIndex < sourceBones.Count; boneIndex++) {
                SpriteBone bone = sourceBones[boneIndex];
                bone.position += new Vector3(spritePositionDelta.x, spritePositionDelta.y, 0f);
                destinationBones.Add(bone);
            }
            docBotBoneProvider.SetBones(docBotLayer.SpriteId, destinationBones);

            Vertex2DMetaData[] vertices = docBotMeshProvider.GetVertices(docBotLayer.SpriteId);
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++) {
                Vertex2DMetaData vertex = vertices[vertexIndex];
                vertex.boneWeight = new BoneWeight {
                    weight0 = 1f,
                    boneIndex0 = 0
                };
                vertices[vertexIndex] = vertex;
            }
            if (vertices.Length > 0) {
                docBotMeshProvider.SetVertices(docBotLayer.SpriteId, vertices);
            }
        }

        CharacterData transferredCharacter = new CharacterData {
            bones = workerCharacter.bones.ToArray(),
            parts = destinationParts,
            dimension = docBotCharacter.dimension,
            characterGroups = docBotCharacter.characterGroups,
            pivot = new Vector2(0.5f, 0f)
        };
        docBotCharacterProvider.SetCharacterData(transferredCharacter);
        docBotProvider.Apply();
        EditorUtility.SetDirty(docBotImporter);
        AssetDatabase.WriteImportSettingsIfDirty(DocBotPsdPath);
        AssetDatabase.ImportAsset(
            DocBotPsdPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static RuntimeAnimatorController EnsureAnimatorController() {
        AnimatorController existing =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (IsCleanAnimatorController(existing)) {
            return existing;
        }

        // Worker_Animator contains transitions to a missing state. Do not clone its
        // broken YAML: DocBot currently needs a stable rest pose and a compatible
        // parameter contract, while authored movement clips can be added later.
        string existingGuid = AssetDatabase.AssetPathToGUID(AnimatorControllerPath);
        if (!string.IsNullOrEmpty(existingGuid)) {
            Require(AssetDatabase.DeleteAsset(AnimatorControllerPath),
                $"Could not replace invalid Animator Controller '{AnimatorControllerPath}'.");
        }

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
        Require(controller != null,
            $"Could not create DocBot Animator Controller at '{AnimatorControllerPath}'.");
        Require(controller.layers != null && controller.layers.Length == 1,
            "A new Animator Controller must contain exactly one base layer.");

        AnimatorControllerLayer layer = controller.layers[0];
        layer.name = "Base Layer";
        controller.layers = new[] { layer };
        AnimatorStateMachine stateMachine = layer.stateMachine;
        Require(stateMachine != null,
            "The generated DocBot Animator Controller has no state machine.");

        AnimatorState idle = stateMachine.AddState("Idle");
        idle.motion = null;
        idle.writeDefaultValues = true;
        stateMachine.defaultState = idle;

        for (int i = 0; i < AnimatorParameters.Length; i++) {
            AnimatorParameterContract parameter = AnimatorParameters[i];
            controller.AddParameter(parameter.Name, parameter.Type);
        }

        EditorUtility.SetDirty(idle);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            AnimatorControllerPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        AnimatorController generated =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        Require(generated != null,
            "The generated DocBot Animator Controller could not be loaded.");
        ValidateAnimatorController(generated);
        return generated;
    }

    private static bool IsCleanAnimatorController(AnimatorController controller) {
        if (controller == null) {
            return false;
        }

        try {
            ValidateAnimatorController(controller);
            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    private static void ValidateAnimatorController(AnimatorController controller) {
        Require(controller != null, "DocBot Animator Controller is null.");

        AnimatorControllerParameter[] parameters = controller.parameters;
        Require(parameters != null && parameters.Length == AnimatorParameters.Length,
            $"DocBot Animator Controller must expose exactly {AnimatorParameters.Length} "
            + "compatibility parameters.");
        for (int i = 0; i < AnimatorParameters.Length; i++) {
            AnimatorParameterContract expected = AnimatorParameters[i];
            AnimatorControllerParameter[] matches = parameters
                .Where(parameter => parameter.name == expected.Name)
                .ToArray();
            Require(matches.Length == 1,
                $"DocBot Animator Controller must expose '{expected.Name}' exactly once.");
            Require(matches[0].type == expected.Type,
                $"DocBot Animator parameter '{expected.Name}' must be {expected.Type}.");
        }

        AnimatorControllerLayer[] layers = controller.layers;
        Require(layers != null && layers.Length == 1,
            "DocBot Animator Controller must contain exactly one layer.");
        Require(layers[0].name == "Base Layer",
            "DocBot Animator Controller layer must be named 'Base Layer'.");
        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        Require(stateMachine != null,
            "DocBot Animator Controller Base Layer has no state machine.");
        Require(stateMachine.stateMachines.Length == 0,
            "DocBot Animator Controller must not contain nested state machines.");

        ChildAnimatorState[] states = stateMachine.states;
        Require(states != null && states.Length == 1 && states[0].state != null,
            "DocBot Animator Controller must contain exactly one state.");
        AnimatorState idle = states[0].state;
        Require(idle.name == "Idle",
            "DocBot Animator Controller's only state must be named Idle.");
        Require(stateMachine.defaultState == idle,
            "Idle must be the default DocBot Animator state.");
        Require(idle.motion == null,
            "DocBot Idle intentionally has no animation clip yet.");
        Require(idle.transitions.Length == 0
            && stateMachine.anyStateTransitions.Length == 0
            && stateMachine.entryTransitions.Length == 0,
            "DocBot's rest-only Animator Controller must not contain transitions.");
    }

    private static void BuildMasterPrefab(int enemyLayer, RuntimeAnimatorController controller) {
        BuildFromTemplate(
            WorkerMasterPrefabPath,
            MasterPrefabPath,
            root => {
                root.name = DocBotMasterName;
                ResetWorkingRoot(root.transform);
                SetLayerRecursively(root, enemyLayer);
                SwapWorkerSprites(root);
                AssignAnimatorController(root, controller);
                SetRenderersEnabled(root, false);
            });
    }

    private static void BuildPuppetPrefab(int enemyLayer) {
        BuildFromTemplate(
            WorkerPuppetPrefabPath,
            PuppetPrefabPath,
            root => {
                root.name = DocBotPuppetName;
                ResetWorkingRoot(root.transform);
                SetLayerRecursively(root, enemyLayer);
                SwapWorkerSprites(root);
                SetRenderersEnabled(root, true);
            });
    }

    private static void BuildFinalPrefab(
        int enemyLayer,
        RuntimeAnimatorController controllerAsset) {
        BuildFromTemplate(
            WorkerFinalPrefabPath,
            FinalPrefabPath,
            root => {
                root.name = "DocBot";
                root.tag = EnemyTag;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                SetLayerRecursively(root, enemyLayer);

                Transform puppetRoot = FindUniqueDescendant(root.transform, WorkerPuppetName);
                Transform masterRoot = FindUniqueDescendant(root.transform, WorkerMasterName);
                puppetRoot.name = DocBotPuppetName;
                masterRoot.name = DocBotMasterName;

                SwapWorkerSprites(root);
                AssignAnimatorController(root, controllerAsset);
                SetRenderersEnabled(masterRoot.gameObject, false);
                StripWorkerAi(root);
                ConfigureDocBotComponents(root, puppetRoot, masterRoot, enemyLayer);
            });
    }

    private static void BuildFromTemplate(
        string sourcePath,
        string destinationPath,
        Action<GameObject> prepare) {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        Require(source != null, $"Missing source prefab at '{sourcePath}'.");

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try {
            instance = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
            Require(instance != null, $"Could not instantiate source prefab '{sourcePath}'.");
            if (PrefabUtility.IsPartOfPrefabInstance(instance)) {
                PrefabUtility.UnpackPrefabInstance(
                    instance,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            prepare(instance);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, destinationPath);
            Require(saved != null, $"Could not save generated prefab '{destinationPath}'.");
        }
        finally {
            if (instance != null) {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void SwapWorkerSprites(GameObject root) {
        Dictionary<int, ImportedLayer> workerLayers =
            ReadImportedLayers((PSDImporter)AssetImporter.GetAtPath(WorkerPsdPath));
        Dictionary<int, ImportedLayer> docBotLayers =
            ReadImportedLayers((PSDImporter)AssetImporter.GetAtPath(DocBotPsdPath));
        Dictionary<GUID, int> workerLayerBySprite = workerLayers.Values
            .Where(layer => LayerMappings.Any(mapping => mapping.WorkerLayerId == layer.LayerId))
            .ToDictionary(layer => layer.SpriteId, layer => layer.LayerId);
        Dictionary<int, int> docBotLayerByWorkerLayer = LayerMappings
            .ToDictionary(mapping => mapping.WorkerLayerId, mapping => mapping.DocBotLayerId);
        Dictionary<GUID, Sprite> docBotSprites = LoadSpritesById(DocBotPsdPath);

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int swappedCount = 0;
        for (int i = 0; i < renderers.Length; i++) {
            SpriteRenderer renderer = renderers[i];
            if (renderer.sprite == null
                || AssetDatabase.GetAssetPath(renderer.sprite) != WorkerPsdPath) {
                continue;
            }

            GUID sourceSpriteId = renderer.sprite.GetSpriteID();
            Require(workerLayerBySprite.TryGetValue(sourceSpriteId, out int workerLayerId),
                $"Renderer '{renderer.name}' references an unmapped Worker PSD sprite ID.");
            int docBotLayerId = docBotLayerByWorkerLayer[workerLayerId];
            GUID destinationSpriteId = docBotLayers[docBotLayerId].SpriteId;
            Require(docBotSprites.TryGetValue(destinationSpriteId, out Sprite destinationSprite),
                $"No imported DocBot sprite exists for layer {docBotLayerId}.");
            renderer.sprite = destinationSprite;
            EditorUtility.SetDirty(renderer);
            swappedCount++;
        }

        Require(swappedCount == renderers.Length,
            $"Expected to replace all {renderers.Length} Worker sprites on '{root.name}', "
            + $"but replaced {swappedCount}.");
    }

    private static void StripWorkerAi(GameObject root) {
        Transform roomsTriggerZone =
            FindUniqueDescendant(root.transform, WorkerRoomsTriggerZoneName);
        Require(roomsTriggerZone.GetComponent<PositionTriggerZone>() != null,
            $"Worker child '{WorkerRoomsTriggerZoneName}' no longer contains "
            + "PositionTriggerZone; refusing to remove an ambiguous object.");
        UnityEngine.Object.DestroyImmediate(roomsTriggerZone.gameObject, true);

        DestroyAllComponents<FollowEnemyTriggerHandler>(root);
        DestroyAllComponents<EnemyArmTargetController>(root);
        DestroyAllComponents<RobotAttackController>(root);
        DestroyAllComponents<RobotBrainNew>(root);
        DestroyAllComponents<PooledEnemy>(root);
        DestroyAllComponents<RobotHeartNew>(root);
    }

    private static void ConfigureDocBotComponents(
        GameObject root,
        Transform puppetRoot,
        Transform masterRoot,
        int enemyLayer) {
        SimplePuppetBinder binder = RequireSingle<SimplePuppetBinder>(root);
        RebuildBinderPairs(binder, masterRoot, puppetRoot);
        RobotStateController state = RequireSingle<RobotStateController>(root);
        HealthBot health = RequireSingle<HealthBot>(root);
        RobotMemoryNew memory = RequireSingle<RobotMemoryNew>(root);
        JointBreaker breaker = RequireSingle<JointBreaker>(root);
        DamageFeedback feedback = RequireSingle<DamageFeedback>(root);
        EnemyGrabbable grabbable = RequireSingle<EnemyGrabbable>(root);

        state.ConfigureCoreReferences(health, breaker, memory);
        health.ConfigureDamageFeedback(feedback);

        Transform leftHand = FindUniqueDescendantOutside(
            puppetRoot,
            "LHand_Bone",
            masterRoot);
        Transform rightHand = FindUniqueDescendantOutside(
            puppetRoot,
            "RHand_Bone",
            masterRoot);
        Transform leftArmTarget = FindUniqueDescendant(masterRoot, "LArm_Solver_Target");
        Transform rightArmTarget = FindUniqueDescendant(masterRoot, "RArm_Solver_Target");

        DocBotItemHolder holder = root.AddComponent<DocBotItemHolder>();
        holder.Configure(leftHand, rightHand);
        DocBotHandReachController reach = root.AddComponent<DocBotHandReachController>();
        reach.Configure(leftArmTarget, rightArmTarget);
        DocBotController docBot = root.AddComponent<DocBotController>();
        docBot.Configure(state, health, breaker, grabbable, holder, reach);

        GameObject receiverObject = new GameObject(ReceiverTriggerName);
        receiverObject.layer = enemyLayer;
        receiverObject.transform.SetParent(root.transform, false);
        CircleCollider2D receiverCollider = receiverObject.AddComponent<CircleCollider2D>();
        receiverCollider.isTrigger = true;
        ConfigureReceiverBounds(root, receiverObject.transform, receiverCollider);
        DocBotJunkReceiver receiver = receiverObject.AddComponent<DocBotJunkReceiver>();
        receiver.Configure(docBot, holder, reach);

        grabbable.ConfigureExtraBehaviours(receiver, reach, binder);
        root.AddComponent<DeadRobotCollectable>();
    }

    private static void RebuildBinderPairs(
        SimplePuppetBinder binder,
        Transform masterRoot,
        Transform puppetRoot) {
        Require(binder != null, "Cannot rebuild a null DocBot binder.");
        Require(masterRoot != null && puppetRoot != null,
            "DocBot binder roots must be assigned before rebuilding its pairs.");

        List<SimplePuppetBinder.BonePair> pairs =
            new List<SimplePuppetBinder.BonePair>(BinderBoneNames.Length);
        for (int i = 0; i < BinderBoneNames.Length; i++) {
            string boneName = BinderBoneNames[i];
            Transform masterBone = FindUniqueDescendant(masterRoot, boneName);
            Transform puppetBone = FindUniqueDescendantOutside(
                puppetRoot,
                boneName,
                masterRoot);
            Rigidbody2D puppetBody = puppetBone.GetComponent<Rigidbody2D>();
            Require(puppetBody != null,
                $"Physical Puppet bone '{boneName}' must contain a Rigidbody2D.");

            pairs.Add(new SimplePuppetBinder.BonePair {
                Master = masterBone,
                Puppet = puppetBone,
                PuppetBody2D = puppetBody
            });
        }

        binder.MasterRoot = masterRoot;
        binder.PuppetRoot = puppetRoot;
        binder.RotationSharpness = 0f;
        binder.Pairs = pairs;
        EditorUtility.SetDirty(binder);
    }

    private static void ConfigureReceiverBounds(
        GameObject root,
        Transform receiverTransform,
        CircleCollider2D receiverCollider) {
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true)
            .Where(collider => collider != receiverCollider && !collider.isTrigger)
            .ToArray();
        Require(colliders.Length > 0,
            "Cannot size DocBot's receiver trigger without physical Puppet colliders.");

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) {
            bounds.Encapsulate(colliders[i].bounds);
        }

        receiverTransform.position = bounds.center;
        receiverTransform.rotation = root.transform.rotation;
        receiverTransform.localScale = Vector3.one;
        float worldScale = Mathf.Max(
            Mathf.Abs(receiverTransform.lossyScale.x),
            Mathf.Abs(receiverTransform.lossyScale.y));
        Require(worldScale > Mathf.Epsilon, "DocBot receiver trigger has a zero world scale.");
        receiverCollider.radius =
            (Mathf.Max(bounds.extents.x, bounds.extents.y) + ReceiverWorldPadding) / worldScale;
    }

    private static void ConfigureLaboratoryRoom() {
        GameObject docBotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FinalPrefabPath);
        GameObject cubePrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(WhiteCubePrefabPath);
        Require(docBotPrefab != null, "Cannot configure the laboratory without DocBot.prefab.");
        Require(cubePrefabObject != null,
            $"Missing white cube prefab at '{WhiteCubePrefabPath}'.");

        DocBotController docBot = docBotPrefab.GetComponent<DocBotController>();
        CubePickup whiteCube = cubePrefabObject.GetComponent<CubePickup>();
        Require(docBot != null, "Generated DocBot prefab has no DocBotController.");
        Require(whiteCube != null, "CubeNormal prefab has no CubePickup component.");

        GameObject room = PrefabUtility.LoadPrefabContents(LaboratoryRoomPrefabPath);
        Require(room != null,
            $"Could not open laboratory room prefab '{LaboratoryRoomPrefabPath}'.");
        try {
            Transform centerWaypoint = FindUniqueDescendant(room.transform, "CenterWaypoint");
            LaboratoryManager manager = room.GetComponent<LaboratoryManager>();
            if (manager == null) {
                manager = room.AddComponent<LaboratoryManager>();
            }
            manager.Configure(docBot, centerWaypoint, whiteCube);
            EditorUtility.SetDirty(manager);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(room, LaboratoryRoomPrefabPath);
            Require(saved != null, "Could not save LaboratoryManager into ROOM_Laboratory_1.");
        }
        finally {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    private static void ValidateImportedRig() {
        PSDImporter importer = AssetImporter.GetAtPath(DocBotPsdPath) as PSDImporter;
        Require(importer != null, "DocBot.psd must use PSDImporter.");
        Require(importer.textureType == TextureImporterType.Sprite,
            "DocBot.psd must import as Sprite.");
        Require(importer.spriteImportMode == SpriteImportMode.Multiple,
            "DocBot.psd must import in Multiple mode.");
        Require(importer.useMosaicMode && importer.useCharacterMode,
            "DocBot.psd must use Mosaic and Character modes.");

        Dictionary<int, ImportedLayer> layers = ReadImportedLayers(importer);
        ValidateLayerContract(layers, isWorker: false);
        Dictionary<GUID, Sprite> sprites = LoadSpritesById(DocBotPsdPath);
        Require(sprites.Count == LayerMappings.Length,
            $"DocBot PSD must expose exactly 14 sprites; found {sprites.Count}.");

        ISpriteEditorDataProvider provider = importer;
        provider.InitSpriteEditorDataProvider();
        ICharacterDataProvider characterProvider = provider.GetDataProvider<ICharacterDataProvider>();
        Require(characterProvider != null, "DocBot character data provider is unavailable.");
        CharacterData character = characterProvider.GetCharacterData();
        Require(character.bones != null && character.bones.Length == 13,
            $"DocBot character must contain 13 bones; found {character.bones?.Length ?? 0}.");
        Require(character.parts != null && character.parts.Length == LayerMappings.Length,
            $"DocBot character must contain 14 parts; found {character.parts?.Length ?? 0}.");

        Dictionary<string, CharacterPart> parts = character.parts
            .ToDictionary(part => part.spriteId, part => part, StringComparer.Ordinal);
        for (int i = 0; i < LayerMappings.Length; i++) {
            ImportedLayer layer = layers[LayerMappings[i].DocBotLayerId];
            Require(sprites.TryGetValue(layer.SpriteId, out Sprite sprite),
                $"DocBot layer {layer.LayerId} has no generated Sprite.");
            Require(sprite.GetBones().Length == 1,
                $"DocBot layer {layer.LayerId} must have exactly one rigid Sprite bone.");
            Require(sprite.GetVertexCount() > 0 && sprite.GetIndices().Length >= 3,
                $"DocBot layer {layer.LayerId} must expose a renderable skinned mesh.");
            Require(sprite.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.BlendWeight),
                $"DocBot layer {layer.LayerId} must expose skinning weights.");
            Require(parts.TryGetValue(layer.SpriteId.ToString(), out CharacterPart part)
                && part.bones != null
                && part.bones.Length == 1,
                $"DocBot layer {layer.LayerId} must reference one character bone.");
        }
    }

    private static void ValidateLaboratoryRoom(DocBotController expectedDocBot) {
        GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(LaboratoryRoomPrefabPath);
        Require(room != null,
            $"Missing laboratory room prefab at '{LaboratoryRoomPrefabPath}'.");
        LaboratoryManager manager = room.GetComponent<LaboratoryManager>();
        Require(manager != null,
            "ROOM_Laboratory_1 root must contain LaboratoryManager.");
        Transform centerWaypoint = FindUniqueDescendant(room.transform, "CenterWaypoint");
        GameObject cubeObject = AssetDatabase.LoadAssetAtPath<GameObject>(WhiteCubePrefabPath);
        CubePickup whiteCube = cubeObject != null ? cubeObject.GetComponent<CubePickup>() : null;
        ValidateSerializedObjectReference(manager, "docBotPrefab", expectedDocBot);
        ValidateSerializedObjectReference(manager, "docBotSpawnPoint", centerWaypoint);
        ValidateSerializedObjectReference(manager, "whiteCubePrefab", whiteCube);
    }

    private static Dictionary<int, ImportedLayer> ReadImportedLayers(PSDImporter importer) {
        Require(importer != null, "Cannot read PSD layers from a null importer.");
        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty layersProperty = FindProperty(
            serializedImporter,
            "m_PsdLayers",
            "psdLayers");
        Require(layersProperty != null && layersProperty.isArray,
            $"PSDImporter serialization at '{importer.assetPath}' no longer exposes its layer list.");

        Dictionary<int, ImportedLayer> layers = new Dictionary<int, ImportedLayer>();
        for (int i = 0; i < layersProperty.arraySize; i++) {
            SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
            SerializedProperty layerIdProperty = FindRelativeProperty(
                layerProperty,
                "m_LayerID",
                "layerID");
            SerializedProperty nameProperty = FindRelativeProperty(
                layerProperty,
                "m_Name",
                "name");
            SerializedProperty groupProperty = FindRelativeProperty(
                layerProperty,
                "m_IsGroup",
                "isGroup");
            SerializedProperty importedProperty = FindRelativeProperty(
                layerProperty,
                "m_IsImported",
                "isImported");
            SerializedProperty spriteIdProperty = FindRelativeProperty(
                layerProperty,
                "m_SpriteID",
                "spriteID");
            Require(layerIdProperty != null && nameProperty != null && groupProperty != null
                && importedProperty != null && spriteIdProperty != null,
                "PSDImporter layer serialization changed; refusing an ambiguous rig transfer.");

            if (groupProperty.boolValue || !importedProperty.boolValue) {
                continue;
            }

            int layerId = layerIdProperty.intValue;
            string spriteIdText = spriteIdProperty.stringValue;
            Require(!string.IsNullOrWhiteSpace(spriteIdText),
                $"Imported PSD layer {layerId} has no stable sprite ID.");
            Require(!layers.ContainsKey(layerId),
                $"PSD importer contains duplicate imported layer ID {layerId}.");
            layers.Add(layerId, new ImportedLayer(
                layerId,
                nameProperty.stringValue,
                new GUID(spriteIdText)));
        }
        return layers;
    }

    private static void ValidateLayerContract(
        IReadOnlyDictionary<int, ImportedLayer> layers,
        bool isWorker) {
        Require(layers.Count == LayerMappings.Length,
            $"{(isWorker ? "Worker" : "DocBot")} PSD must expose exactly 14 imported layers; "
            + $"found {layers.Count}.");

        for (int i = 0; i < LayerMappings.Length; i++) {
            LayerMapping mapping = LayerMappings[i];
            int layerId = isWorker ? mapping.WorkerLayerId : mapping.DocBotLayerId;
            string expectedName = isWorker
                ? mapping.WorkerLayerName
                : mapping.DocBotLayerName;
            Require(layers.TryGetValue(layerId, out ImportedLayer layer),
                $"{(isWorker ? "Worker" : "DocBot")} PSD is missing imported layer ID {layerId}.");
            Require(string.Equals(layer.Name, expectedName, StringComparison.Ordinal),
                $"{(isWorker ? "Worker" : "DocBot")} PSD layer {layerId} must be named "
                + $"'{EscapeVisible(expectedName)}', but is '{EscapeVisible(layer.Name)}'. "
                + "Trailing spaces are significant for this contract.");
        }
    }

    private static Dictionary<GUID, Sprite> LoadSpritesById(string assetPath) {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
        Dictionary<GUID, Sprite> result = new Dictionary<GUID, Sprite>();
        for (int i = 0; i < sprites.Length; i++) {
            GUID spriteId = sprites[i].GetSpriteID();
            Require(!result.ContainsKey(spriteId),
                $"Asset '{assetPath}' exposes duplicate Sprite ID {spriteId}.");
            result.Add(spriteId, sprites[i]);
        }
        return result;
    }

    private static void AssignAnimatorController(
        GameObject root,
        RuntimeAnimatorController controller) {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        Require(animators.Length == 1,
            $"'{root.name}' must contain exactly one Worker Animator; found {animators.Length}.");
        animators[0].runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animators[0]);
    }

    private static void SetRenderersEnabled(GameObject root, bool enabled) {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++) {
            renderers[i].enabled = enabled;
            EditorUtility.SetDirty(renderers[i]);
        }
    }

    private static void ResetWorkingRoot(Transform root) {
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject root, int layer) {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++) {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static void DestroyAllComponents<T>(GameObject root) where T : Component {
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = components.Length - 1; i >= 0; i--) {
            UnityEngine.Object.DestroyImmediate(components[i], true);
        }
    }

    private static T RequireSingle<T>(GameObject root) where T : Component {
        T[] components = root.GetComponentsInChildren<T>(true);
        Require(components.Length == 1,
            $"'{root.name}' must contain exactly one {typeof(T).Name}; found {components.Length}.");
        return components[0];
    }

    private static Transform FindUniqueDescendant(Transform root, string exactName) {
        Transform[] matches = root.GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate.name == exactName)
            .ToArray();
        Require(matches.Length == 1,
            $"Expected exactly one '{exactName}' below '{root.name}'; found {matches.Length}.");
        return matches[0];
    }

    private static Transform FindUniqueDescendantOutside(
        Transform root,
        string exactName,
        Transform excludedRoot) {
        Transform[] matches = root.GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate.name == exactName
                && (excludedRoot == null || !candidate.IsChildOf(excludedRoot)))
            .ToArray();
        Require(matches.Length == 1,
            $"Expected exactly one physical '{exactName}' below '{root.name}'; "
            + $"found {matches.Length} outside the Master rig.");
        return matches[0];
    }

    private static void ValidateSerializedObjectReference(
        UnityEngine.Object owner,
        string propertyName,
        UnityEngine.Object expected) {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Require(property != null,
            $"{owner.GetType().Name} no longer exposes serialized field '{propertyName}'.");
        Require(property.objectReferenceValue == expected,
            $"{owner.GetType().Name}.{propertyName} has the wrong reference.");
    }

    private static SerializedProperty FindProperty(
        SerializedObject serializedObject,
        params string[] candidates) {
        for (int i = 0; i < candidates.Length; i++) {
            SerializedProperty property = serializedObject.FindProperty(candidates[i]);
            if (property != null) {
                return property;
            }
        }
        return null;
    }

    private static SerializedProperty FindRelativeProperty(
        SerializedProperty parent,
        params string[] candidates) {
        for (int i = 0; i < candidates.Length; i++) {
            SerializedProperty property = parent.FindPropertyRelative(candidates[i]);
            if (property != null) {
                return property;
            }
        }
        return null;
    }

    private static void SetRequiredBool(
        SerializedObject serializedObject,
        bool value,
        params string[] propertyNames) {
        SerializedProperty property = FindProperty(serializedObject, propertyNames);
        Require(property != null,
            $"PSDImporter no longer exposes '{string.Join("/", propertyNames)}'.");
        property.boolValue = value;
    }

    private static void SetRequiredInt(
        SerializedObject serializedObject,
        int value,
        params string[] propertyNames) {
        SerializedProperty property = FindProperty(serializedObject, propertyNames);
        Require(property != null,
            $"PSDImporter no longer exposes '{string.Join("/", propertyNames)}'.");
        property.intValue = value;
    }

    private static void SetRequiredVector2(
        SerializedObject serializedObject,
        Vector2 value,
        params string[] propertyNames) {
        SerializedProperty property = FindProperty(serializedObject, propertyNames);
        Require(property != null,
            $"PSDImporter no longer exposes '{string.Join("/", propertyNames)}'.");
        property.vector2Value = value;
    }

    private static void EnsureAssetFolder(string folderPath) {
        string[] segments = folderPath.Split('/');
        Require(segments.Length > 0 && segments[0] == "Assets",
            $"Asset folder must begin with Assets: '{folderPath}'.");
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++) {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) {
                string guid = AssetDatabase.CreateFolder(current, segments[i]);
                Require(!string.IsNullOrWhiteSpace(guid),
                    $"Could not create asset folder '{next}'.");
            }
            current = next;
        }
    }

    private static string EscapeVisible(string value) {
        return value == null ? "<null>" : value.Replace(" ", "·");
    }

    private static bool Approximately(Vector3 first, Vector3 second) {
        return Vector3.SqrMagnitude(first - second) < 0.000001f;
    }

    private static bool Approximately(Quaternion first, Quaternion second) {
        return Quaternion.Angle(first, second) < 0.001f;
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}

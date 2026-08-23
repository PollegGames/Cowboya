using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies the room-family minimap palette, icon assignments, and explicit icon tiles.
/// </summary>
public static class MinimapRoomPreviewVisualGuideApplier {
    private const string ColorMaterialFolder = "Assets/Materials and Shaders/Materials/ColorMaterials";
    private const string IconMaterialFolder = "Assets/Materials and Shaders/Materials/Icons";
    private const string IconTileMaskPath = IconMaterialFolder + "/MinimapIconTileMask.png";
    private const string RoomPrefabFolder = "Assets/Resources/Prefabs/Map";

    private sealed class RoomStyle {
        public readonly string PrefabName;
        public readonly string BackgroundMaterial;
        public readonly string TileMaterial;
        public readonly string IconMaterial;

        public RoomStyle(string prefabName, string backgroundMaterial, string tileMaterial, string iconMaterial) {
            PrefabName = prefabName;
            BackgroundMaterial = backgroundMaterial;
            TileMaterial = tileMaterial;
            IconMaterial = iconMaterial;
        }
    }

    private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color> {
        { "MinimapGrey", new Color32(119, 124, 130, 255) },
        { "MinimapIndustrialBrown", new Color32(183, 137, 91, 255) },
        { "MinimapRustBrown", new Color32(168, 90, 50, 255) },
        { "MinimapBlue", new Color32(53, 111, 193, 255) },
        { "MinimapCollectBlue", new Color32(78, 145, 190, 255) },
        { "MinimapRed", new Color32(183, 72, 77, 255) },
        { "MinimapTeal", new Color32(62, 157, 155, 255) },
        { "MinimapGreen", new Color32(78, 155, 98, 255) },
        { "MinimapOrange", new Color32(216, 138, 53, 255) },
        { "MinimapGold", new Color32(213, 180, 92, 255) },
        { "MinimapTileGrey", new Color32(60, 66, 72, 255) },
        { "MinimapTileBrown", new Color32(90, 56, 37, 255) },
        { "MinimapTileRust", new Color32(105, 50, 30, 255) },
        { "MinimapTileBlue", new Color32(23, 63, 112, 255) },
        { "MinimapTileRed", new Color32(112, 42, 49, 255) },
        { "MinimapTileTeal", new Color32(29, 93, 96, 255) },
        { "MinimapTileGreen", new Color32(36, 90, 57, 255) },
        { "MinimapTileOrange", new Color32(112, 68, 25, 255) },
        { "MinimapTileGold", new Color32(112, 89, 31, 255) }
    };

    private static readonly RoomStyle[] RoomStyles = {
        new RoomStyle("ROOM_Laboratory_1.prefab", "MinimapBlue", "MinimapTileBlue", "Laboratory"),
        new RoomStyle("ROOM_Laboratory_2.prefab", "MinimapBlue", "MinimapTileBlue", "Laboratory"),
        new RoomStyle("ROOM_CubeCollector.prefab", "MinimapCollectBlue", "MinimapTileBlue", "CubeCollector"),
        new RoomStyle("ROOM_Work.prefab", "MinimapIndustrialBrown", "MinimapTileBrown", "Gears"),
        new RoomStyle("ROOM_Garage.prefab", "MinimapIndustrialBrown", "MinimapTileBrown", "Garage"),
        new RoomStyle("ROOM_Conveyor.prefab", "MinimapIndustrialBrown", "MinimapTileBrown", "Conveyor"),
        new RoomStyle("ROOM_Furnace.prefab", "MinimapRustBrown", "MinimapTileRust", "Furnace"),
        new RoomStyle("ROOM_security.prefab", "MinimapRed", "MinimapTileRed", "Security"),
        new RoomStyle("ROOM_Spawning.prefab", "MinimapRed", "MinimapTileRed", "Spawn"),
        new RoomStyle("ROOM_resting.prefab", "MinimapTeal", "MinimapTileTeal", "BatteryCharging"),
        new RoomStyle("ROOM_Start.prefab", "MinimapGreen", "MinimapTileGreen", "RobotHead"),
        new RoomStyle("ROOM_lift.prefab", "MinimapOrange", "MinimapTileOrange", "Lift"),
        new RoomStyle("ROOM_Reception.prefab", "MinimapOrange", "MinimapTileOrange", "ReceptionDesk"),
        new RoomStyle("ROOM_End.prefab", "MinimapGold", "MinimapTileGold", "TrophyEnd"),
        new RoomStyle("ROOM_Junks.prefab", "MinimapGrey", "MinimapTileGrey", "Trashbin"),
        new RoomStyle("ROOM_Deads.prefab", "MinimapGrey", "MinimapTileGrey", "Graveyard")
    };

    /// <summary>
    /// Applies the complete minimap room preview visual guide to shared materials and room prefabs.
    /// </summary>
    [MenuItem("Cowboya/Apply Minimap Room Preview Visual Guide")]
    public static void Apply() {
        EnsureIconImportSettings();
        EnsureRoundedTileMask();
        EnsurePaletteMaterials();
        EnsureSpawnMaterial();

        foreach (RoomStyle style in RoomStyles) {
            ApplyRoomStyle(style);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Applied minimap room preview visual guide to {RoomStyles.Length} room prefabs.");
    }

    private static void EnsureRoundedTileMask() {
        if (!File.Exists(IconTileMaskPath)) {
            const int size = 256;
            const float extent = 120f;
            const float radius = 30f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f) - center;
                    Vector2 distance = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) -
                        new Vector2(extent - radius, extent - radius);
                    float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude;
                    float signedDistance = outside + Mathf.Min(Mathf.Max(distance.x, distance.y), 0f) - radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - signedDistance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(IconTileMaskPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconTileMaskPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(IconTileMaskPath) as TextureImporter;
        if (importer == null) {
            throw new InvalidOperationException($"Texture importer was not found: {IconTileMaskPath}");
        }

        bool changed = importer.maxTextureSize != 256 ||
            !importer.alphaIsTransparency ||
            importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Clamp;
        importer.maxTextureSize = 256;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        if (changed) {
            importer.SaveAndReimport();
        }
    }

    private static void EnsureIconImportSettings() {
        foreach (string iconName in RoomStyles.Select(style => style.IconMaterial).Distinct()) {
            string path = $"{IconMaterialFolder}/{iconName}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) {
                throw new InvalidOperationException($"Texture importer was not found: {path}");
            }

            bool changed = importer.maxTextureSize != 1024 ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled;
            importer.maxTextureSize = 1024;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (changed) {
                importer.SaveAndReimport();
            }
        }
    }

    private static void EnsurePaletteMaterials() {
        Shader backgroundShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader tileShader = Shader.Find("Sprites/Default");
        Texture2D tileMask = AssetDatabase.LoadAssetAtPath<Texture2D>(IconTileMaskPath);
        if (backgroundShader == null) {
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
        }
        if (tileShader == null || tileMask == null) {
            throw new InvalidOperationException("The minimap icon tile shader or mask was not found.");
        }

        foreach (KeyValuePair<string, Color> entry in Palette) {
            bool isTile = entry.Key.StartsWith("MinimapTile", StringComparison.Ordinal);
            Shader shader = isTile ? tileShader : backgroundShader;
            string path = $"{ColorMaterialFolder}/{entry.Key}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(shader) { name = entry.Key };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = entry.Value;
            material.mainTexture = isTile ? tileMask : null;
            if (material.HasProperty("_BaseColor")) {
                material.SetColor("_BaseColor", entry.Value);
            }

            if (material.HasProperty("_Smoothness")) {
                material.SetFloat("_Smoothness", 0f);
            }

            EditorUtility.SetDirty(material);
        }
    }

    private static void EnsureSpawnMaterial() {
        string materialPath = $"{IconMaterialFolder}/Spawn.mat";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconMaterialFolder}/Spawn.png");
        if (texture == null) {
            throw new InvalidOperationException("Spawn.png could not be loaded.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) {
            Shader shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Spawn" };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.mainTexture = texture;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
    }

    private static void ApplyRoomStyle(RoomStyle style) {
        string prefabPath = $"{RoomPrefabFolder}/{style.PrefabName}";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try {
            Transform preview = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name.EndsWith("PreviewMiniMap", StringComparison.Ordinal));
            if (preview == null) {
                throw new InvalidOperationException($"No minimap preview was found in {style.PrefabName}.");
            }

            Transform icon = preview.Find("Icon");
            if (icon == null) {
                throw new InvalidOperationException($"No Icon child was found in {style.PrefabName}.");
            }

            Material background = LoadMaterial(ColorMaterialFolder, style.BackgroundMaterial);
            Material tile = LoadMaterial(ColorMaterialFolder, style.TileMaterial);
            Material iconMaterial = LoadMaterial(IconMaterialFolder, style.IconMaterial);

            MeshRenderer backgroundRenderer = preview.GetComponent<MeshRenderer>();
            MeshRenderer iconRenderer = icon.GetComponent<MeshRenderer>();
            MeshFilter iconFilter = icon.GetComponent<MeshFilter>();
            if (backgroundRenderer == null || iconRenderer == null || iconFilter == null) {
                throw new InvalidOperationException($"The minimap preview renderers are incomplete in {style.PrefabName}.");
            }

            backgroundRenderer.sharedMaterial = background;
            iconRenderer.sharedMaterial = iconMaterial;
            iconRenderer.shadowCastingMode = ShadowCastingMode.Off;
            iconRenderer.receiveShadows = false;

            Transform tileTransform = preview.Find("IconTile");
            GameObject tileObject;
            if (tileTransform == null) {
                tileObject = new GameObject("IconTile");
                tileTransform = tileObject.transform;
                tileTransform.SetParent(preview, false);
                MeshFilter tileFilter = tileObject.AddComponent<MeshFilter>();
                tileFilter.sharedMesh = iconFilter.sharedMesh;
                tileObject.AddComponent<MeshRenderer>();
            } else {
                tileObject = tileTransform.gameObject;
            }

            tileObject.layer = icon.gameObject.layer;
            tileTransform.localRotation = icon.localRotation;
            tileTransform.localScale = icon.localScale * 1.12f;
            Vector3 iconPosition = icon.localPosition;
            tileTransform.localPosition = new Vector3(iconPosition.x, 0.01f, iconPosition.z);
            icon.localPosition = new Vector3(iconPosition.x, 0.02f, iconPosition.z);
            tileTransform.SetSiblingIndex(icon.GetSiblingIndex());

            MeshRenderer tileRenderer = tileObject.GetComponent<MeshRenderer>();
            tileRenderer.sharedMaterial = tile;
            tileRenderer.shadowCastingMode = ShadowCastingMode.Off;
            tileRenderer.receiveShadows = false;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Material LoadMaterial(string folder, string name) {
        string path = $"{folder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) {
            throw new InvalidOperationException($"Material was not found: {path}");
        }

        return material;
    }
}

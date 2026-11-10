using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class TwilightThicketTilemapEditor
{
    public const string AtlasPath = "Assets/Art/TwilightThicket/TwilightThicket_Flat3Atlas.png";
    public const string FrameProfileFolder = "Assets/Resources/Battlefield/TwilightThicket/Frames";
    public const string VinePath = "Assets/Art/TwilightThicket/BreachVine_v1.png";
    public const string EyeMaterialPath = "Assets/Art/TwilightThicket/ForestEyeGlow.mat";
    public const string OpaqueFrameShaderPath = "Assets/Art/TwilightThicket/OpaqueForestFrame.shader";
    public const string OpaqueFrameMaterialPath = "Assets/Art/TwilightThicket/OpaqueForestFrame.mat";
    public const string PalettePath = "Assets/Resources/Battlefield/TwilightThicket/TwilightThicketPalette.asset";
    public const string TileFolder = "Assets/Resources/Battlefield/TwilightThicket/Tiles";
    private const string GameplayScenePath = "Assets/Asset.unity";
    private const int AtlasColumns = 4;
    private const int AtlasRows = 4;

    public static string FramePathForLine(int line)
    {
        Vector2Int size = Stage.SizeForLine(line);
        return "Assets/Art/TwilightThicket/Frames/ForestFrameOrange_Line" + line + "_" + size.x + "x" + size.y + ".png";
    }

    public static string FrameProfilePath(int line)
    {
        Vector2Int size = Stage.SizeForLine(line);
        return FrameProfileFolder + "/Frame_Line" + line + "_" + size.x + "x" + size.y + ".asset";
    }

    [MenuItem("Forest Animal War/Art/Rebuild Twilight Thicket tilemap")]
    public static void RebuildFromMenu()
    {
        BattlefieldTilePalette palette = RebuildAssets();
        ApplyToLoadedScene(palette, true);
        Debug.Log("Twilight Thicket tilemap rebuilt for " + UnityEngine.Object.FindObjectsOfType<Stage>(true).Length + " rooms.");
    }

    public static BattlefieldTilePalette RebuildAssets()
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (atlas == null) throw new FileNotFoundException("Twilight Thicket atlas was not found.", AtlasPath);
        if (atlas.width != atlas.height)
            throw new InvalidOperationException("The tile atlas must be square.");

        ConfigureImporter(atlas.width, atlas.height);
        for (int line = 1; line <= 4; line++)
            ConfigureDecorationImporter(FramePathForLine(line), 128f, 2048);
        ConfigureDecorationImporter(VinePath, 256f, 4096);
        EnsureFrameProfiles();
        EnsureEyeMaterial();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        if (sprites.Length != AtlasColumns * AtlasRows)
            throw new InvalidOperationException("Expected 16 atlas sprites, found " + sprites.Length + ".");

        EnsureFolder(TileFolder);
        var tiles = new List<TileBase>(sprites.Length);
        foreach (Sprite sprite in sprites)
        {
            string path = TileFolder + "/" + sprite.name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.name = sprite.name;
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.gameObject = null;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles.Add(tile);
        }

        EnsureFolder(Path.GetDirectoryName(PalettePath)?.Replace('\\', '/'));
        BattlefieldTilePalette palette = AssetDatabase.LoadAssetAtPath<BattlefieldTilePalette>(PalettePath);
        if (palette == null)
        {
            palette = ScriptableObject.CreateInstance<BattlefieldTilePalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
        }
        palette.ConfigureFromAtlas(tiles);
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        return palette;
    }

    public static void ApplyToLoadedScene(BattlefieldTilePalette palette, bool useUndo)
    {
        if (palette == null || !palette.IsConfigured)
            throw new InvalidOperationException("A configured battlefield tile palette is required ("
                + (palette != null ? palette.ConfigurationSummary : "null") + ").");
        Sprite vine = AssetDatabase.LoadAssetAtPath<Sprite>(VinePath);
        Material eyeMaterial = EnsureEyeMaterial();
        Material opaqueFrame = EnsureOpaqueFrameMaterial();
        if (vine == null)
            throw new InvalidOperationException("The breach vine must import as a sprite.");
        Stage[] rooms = UnityEngine.Object.FindObjectsOfType<Stage>(true);
        foreach (Stage room in rooms)
        {
            if (room.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (useUndo) Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Apply Twilight Thicket tilemap");
            room.SetBattlefieldTilePalette(palette);
            BattlefieldForestStyle style = room.GetComponent<BattlefieldForestStyle>();
            if (style == null) style = room.gameObject.AddComponent<BattlefieldForestStyle>();
            BattlefieldFrameProfile frame = AssetDatabase.LoadAssetAtPath<BattlefieldFrameProfile>(FrameProfilePath(room.DefenseLine));
            if (frame == null || frame.Artwork == null) throw new InvalidOperationException("Missing frame for line " + room.DefenseLine);
            style.Configure(frame.Artwork, vine, eyeMaterial, frame);
            style.SetOpaqueFrameMaterial(opaqueFrame);
            room.ConfigureBattlefield(room.DefenseLine);
            EditorUtility.SetDirty(room);
            EditorUtility.SetDirty(style);
        }
        Stage visibleRoom = rooms.FirstOrDefault(room => room.isActiveAndEnabled);
        Camera camera = Camera.main;
        if (visibleRoom != null && camera != null)
        {
            if (useUndo) Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Frame exterior forest");
            camera.GetComponent<CameraRenderer>()?.FrameRoom(visibleRoom);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Run in an isolated project: -executeMethod TwilightThicketTilemapEditor.BakeBatch
    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        BattlefieldTilePalette palette = RebuildAssets();
        ApplyToLoadedScene(palette, false);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Saved the Twilight Thicket tilemap into all authored rooms.");
    }

    // Replaces only the authored forest decoration, leaving painted battlefield cells intact.
    public static void BakeOrangeFrameBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        for (int line = 1; line <= 4; line++)
            ConfigureDecorationImporter(FramePathForLine(line), 128f, 2048);
        EnsureFrameProfiles();
        Sprite vine = AssetDatabase.LoadAssetAtPath<Sprite>(VinePath);
        Material eyes = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
        Material opaqueFrame = EnsureOpaqueFrameMaterial();
        if (vine == null || eyes == null)
            throw new InvalidOperationException("Forest vine or eye material is missing.");

        int count = 0;
        foreach (Stage room in UnityEngine.Object.FindObjectsOfType<Stage>(true))
        {
            if (room.gameObject.scene != scene) continue;
            BattlefieldFrameProfile profile = AssetDatabase.LoadAssetAtPath<BattlefieldFrameProfile>(
                FrameProfilePath(room.DefenseLine));
            BattlefieldForestStyle style = room.GetComponent<BattlefieldForestStyle>();
            if (profile == null || profile.Artwork == null || style == null)
                throw new InvalidOperationException("Forest frame is missing on " + room.name);
            style.Configure(profile.Artwork, vine, eyes, profile);
            style.SetOpaqueFrameMaterial(opaqueFrame);
            style.Refresh(room);
            EditorUtility.SetDirty(style);
            count++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Orange forest frame baked into " + count + " editable rooms.");
    }

    [MenuItem("Forest Animal War/Art/Refresh lower field in open scene")]
    public static void RefreshLowerFieldInOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameplayBackdropUI backdrop = UnityEngine.Object.FindObjectOfType<GameplayBackdropUI>(true);
        if (backdrop != null)
        {
            backdrop.Build();
            EditorUtility.SetDirty(backdrop.GetComponent<Canvas>());
        }
        Material opaqueFrame = EnsureOpaqueFrameMaterial();
        int count = 0;
        foreach (Stage room in UnityEngine.Object.FindObjectsOfType<Stage>(true))
        {
            if (room.gameObject.scene != scene) continue;
            room.RefreshBattlefieldTiles();
            BattlefieldForestStyle style = room.GetComponent<BattlefieldForestStyle>();
            if (style == null) throw new InvalidOperationException("Missing forest frame on " + room.name);
            style.SetOpaqueFrameMaterial(opaqueFrame);
            style.Refresh(room);
            EditorUtility.SetDirty(style);
            EditorUtility.SetDirty(room);
            count++;
        }
        if (count != 16)
            throw new InvalidOperationException("Expected 16 editable rooms, found " + count + ".");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Refreshed the lower field in " + count + " editable rooms.");
    }

    public static void BakeLowerFieldBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        RefreshLowerFieldInOpenScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Saved the dark lower field and opaque foreground frame.");
    }

    [MenuItem("Forest Animal War/Art/Use pumpkin gold frame colors")]
    public static void UsePumpkinGoldFrameColors()
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (Stage room in UnityEngine.Object.FindObjectsOfType<Stage>(true))
        {
            if (room.gameObject.scene != scene) continue;
            BattlefieldForestStyle style = room.GetComponent<BattlefieldForestStyle>();
            DrownedBogVisuals visuals = room.GetComponent<DrownedBogVisuals>();
            if (style == null || visuals == null) continue;
            // The source artwork is already pumpkin gold; a red multiply tint
            // was suppressing its green/yellow highlights in every location.
            SerializedObject styleData = new SerializedObject(style);
            styleData.FindProperty("frameTint").colorValue = Color.white;
            styleData.FindProperty("boardEdgeTint").colorValue = Color.white;
            styleData.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject locationData = new SerializedObject(visuals);
            SerializedProperty tints = locationData.FindProperty("locationFrameTints");
            for (int i = 0; i < tints.arraySize; i++)
                tints.GetArrayElementAtIndex(i).colorValue = Color.white;
            locationData.ApplyModifiedPropertiesWithoutUndo();
            style.Refresh(room);
            EditorUtility.SetDirty(style);
            EditorUtility.SetDirty(visuals);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void BakePumpkinGoldFrameBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        UsePumpkinGoldFrameColors();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Saved the natural pumpkin-gold frame palette in the editable rooms.");
    }

    private static Material EnsureOpaqueFrameMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(OpaqueFrameShaderPath);
        if (shader == null) throw new InvalidOperationException("Opaque frame shader was not imported.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OpaqueFrameMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "OpaqueForestFrame" };
            AssetDatabase.CreateAsset(material, OpaqueFrameMaterialPath);
        }
        else material.shader = shader;
        material.SetFloat("_Cutoff", 0.18f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void ConfigureImporter(int width, int height)
    {
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("The atlas does not have a TextureImporter.");
        float tileWidth = width / (float)AtlasColumns;
        float tileHeight = height / (float)AtlasRows;
        var metadata = new SpriteMetaData[AtlasColumns * AtlasRows];
        for (int row = 0; row < AtlasRows; row++)
        for (int column = 0; column < AtlasColumns; column++)
        {
            int index = row * AtlasColumns + column;
            metadata[index] = new SpriteMetaData {
                name = "TwilightThicket_" + index.ToString("00"),
                rect = new Rect(column * tileWidth, (AtlasRows - row - 1) * tileHeight, tileWidth, tileHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero
            };
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = tileWidth;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
#pragma warning disable 618
        importer.spritesheet = metadata;
#pragma warning restore 618
        importer.SaveAndReimport();
    }

    private static void EnsureFrameProfiles()
    {
        EnsureFolder(FrameProfileFolder);
        for (int line = 1; line <= 4; line++)
        {
            string path = FrameProfilePath(line);
            Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(FramePathForLine(line));
            if (artwork == null) throw new InvalidOperationException("Frame must import as a sprite: " + FramePathForLine(line));
            BattlefieldFrameProfile profile = AssetDatabase.LoadAssetAtPath<BattlefieldFrameProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BattlefieldFrameProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Initialize(line, Stage.SizeForLine(line), artwork);
            profile.SetArtwork(artwork, OrangeOpeningPixels(line));
            EditorUtility.SetDirty(profile);
        }
    }

    private static Rect OrangeOpeningPixels(int line)
    {
        // Measured against the transparent opening of each dedicated sprite.
        // A five-pixel overlap keeps the artwork touching the cell-edge vines.
        switch (line)
        {
            case 1: return new Rect(95f, 185f, 915f, 1105f);
            case 2: return new Rect(96f, 185f, 897f, 1119f);
            case 3: return new Rect(94f, 183f, 890f, 1144f);
            case 4: return new Rect(93f, 184f, 863f, 1173f);
            default: throw new ArgumentOutOfRangeException(nameof(line));
        }
    }

    private static void ConfigureDecorationImporter(string path, float pixelsPerUnit, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Decoration artwork was not found.", path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureShape = TextureImporterShape.Texture2D;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        settings.spriteBorder = Vector4.zero;
        importer.SetTextureSettings(settings);
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();
    }

    private static Material EnsureEyeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Sprites/Default shader was not found.");
        if (material == null)
        {
            material = new Material(shader) { name = "Forest Eye Glow" };
            AssetDatabase.CreateAsset(material, EyeMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Keeps authored floor artwork linked to editable Tile assets.
// Historical Flat3 filenames are retained so scene references stay stable.
public static class FlatLocationTilesAuthoring
{
    private static readonly string[] ArtworkPaths = {
        TwilightThicketTilemapEditor.AtlasPath,
        "Assets/LocationArt/GloomyHollow/GloomyHollowFlat3Tile.png",
        "Assets/LocationArt/DrownedBog/DrownedBogFlat3Tile.png",
        "Assets/LocationArt/ForgottenBay/ForgottenBayFlat3Tile.png",
        "Assets/LocationArt/SnowyHaze/SnowyHazeFlat3Tile.png",
        "Assets/LocationArt/FieryGehenna/GehennaFlat3Tile.png"
    };
    private static readonly string[] TilePaths = {
        null,
        "Assets/Resources/Battlefield/GloomyHollow/EarthFloorTile.asset",
        "Assets/Resources/Battlefield/DrownedBog/MudFloorTile.asset",
        "Assets/Resources/Battlefield/ForgottenBay/SeaFloorTile.asset",
        "Assets/Resources/Battlefield/SnowyHaze/IceFloorTile.asset",
        "Assets/Resources/Battlefield/FieryGehenna/BasaltFloorTile.asset"
    };
    [MenuItem("Tools/Forest Animal War/Locations/Link softened location tiles")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before linking tiles.");
        AssetDatabase.Refresh();
        for (int i = 1; i < ArtworkPaths.Length; i++)
            ConfigureImporter(ArtworkPaths[i]);

        // Preserve the sixteen forest Tile assets and all scene references.
        TwilightThicketTilemapEditor.RebuildAssets();
        for (int i = 1; i < ArtworkPaths.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPaths[i]);
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePaths[i]);
            if (sprite == null || tile == null)
                throw new InvalidOperationException("Could not link tile " + ArtworkPaths[i]);
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            EditorUtility.SetDirty(tile);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Six softened floor themes linked without overwriting the artwork.");
    }

    private static void ConfigureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("No texture importer: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
        if (sourceWidth <= 0 || sourceWidth != sourceHeight)
            throw new InvalidOperationException("A floor cell must use a square source texture: " + path);
        // maxTextureSize only changes GPU resolution; it does not make the
        // sprite's source rectangle 256 pixels wide. Keep each tile one cell.
        importer.spritePixelsPerUnit = sourceWidth;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

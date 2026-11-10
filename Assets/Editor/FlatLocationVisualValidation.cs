using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class FlatLocationVisualValidation
{
    private static readonly string[] TexturePaths = {
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

    public static void RunBatch()
    {
        try
        {
            Check();
            Debug.Log("Location visuals validation passed: six opaque textured "
                + "checkerboard floor themes and six authored exterior themes.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Check()
    {
        for (int i = 0; i < TexturePaths.Length; i++)
        {
            string path = TexturePaths[i];
            Require(File.Exists(path), "Missing flat texture " + path);
            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(decoded.LoadImage(File.ReadAllBytes(path)),
                    "Cannot decode " + path);
                var colours = new HashSet<uint>();
                foreach (Color32 pixel in decoded.GetPixels32())
                {
                    Require(pixel.a == 255, "Flat cells must be opaque: " + path);
                    colours.Add(((uint)pixel.r << 24) | ((uint)pixel.g << 16)
                        | ((uint)pixel.b << 8) | pixel.a);
                }
                Require(colours.Count > 16,
                    "Floor texture has too little tonal variation: " + path);
            }
            finally { Object.DestroyImmediate(decoded); }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureCompression
                == TextureImporterCompression.Uncompressed
                && importer.filterMode == FilterMode.Bilinear,
                "Floor texture must import smoothly without compression grain: " + path);
            if (i == 0) continue;
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePaths[i]);
            Require(tile != null && tile.sprite != null
                && AssetDatabase.GetAssetPath(tile.sprite) == path,
                "Location tile asset is not linked to its softened sprite: " + path);
            Require(Mathf.Abs(tile.sprite.bounds.size.x - 1f) < 0.001f
                && Mathf.Abs(tile.sprite.bounds.size.y - 1f) < 0.001f,
                "Location floor sprite must occupy one world-space cell, regardless of source resolution: " + path);
        }

        EditorSceneManager.OpenScene("Assets/Asset.unity");
        GameplayBackdropUI backdrop = Object.FindObjectOfType<GameplayBackdropUI>(true);
        LocationBackdropGraphic exterior = backdrop != null
            ? backdrop.GetComponentInChildren<LocationBackdropGraphic>(true) : null;
        PumpkinBackdropGraphic ornament = backdrop != null
            ? backdrop.GetComponentInChildren<PumpkinBackdropGraphic>(true) : null;
        Require(exterior != null && ornament != null,
            "The gameplay exterior and ornaments must be authored in Asset.unity.");
        SerializedObject exteriorData = new SerializedObject(exterior);
        SerializedProperty artwork = exteriorData.FindProperty("locationArtwork");
        Require(artwork.arraySize == 6, "Expected six exterior images.");
        for (int i = 0; i < artwork.arraySize; i++)
            Require(artwork.GetArrayElementAtIndex(i).objectReferenceValue != null,
                "Exterior image " + (i + 1) + " is missing.");
        SerializedObject ornamentData = new SerializedObject(ornament);
        Require(ornamentData.FindProperty("locationPalettes").arraySize == 6,
            "Expected six editable ornament palettes.");
        int stages = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            Require(visuals != null, stage.name + " has no location visuals.");
            SerializedObject data = new SerializedObject(visuals);
            Require(data.FindProperty("locationFrameTints").arraySize == 6,
                stage.name + " has no editable location frame tints.");
            foreach (string field in new[] { "ordinaryTerrainTint", "gloomyTerrainTint",
                "bogTerrainTint", "bayTerrainTint", "snowTerrainTint",
                "gehennaTerrainTint" })
                Require(Mathf.Approximately(data.FindProperty(field).colorValue.a, 1f),
                    stage.name + " still allows noisy scenery through " + field);
            Vector2Int size = stage.BattlefieldSize;
            Vector3Int first = new Vector3Int(-size.x / 2, -size.y / 2, 0);
            Vector3Int next = first + Vector3Int.right;
            Vector3Int playable = first + Vector3Int.up;
            Vector3Int playableNext = playable + Vector3Int.right;
            foreach (Tilemap tiles in stage.GetComponentsInChildren<Tilemap>(true))
            {
                Require(tiles.GetTile(first) != null && tiles.GetTile(next) != null,
                    stage.name + "/" + tiles.name + " is missing floor tiles.");
                Require(tiles.GetColor(first) == tiles.GetColor(next),
                    stage.name + "/" + tiles.name + " has no uniform dark pumpkin strip.");
                Require(tiles.GetColor(playable) != tiles.GetColor(playableNext),
                    stage.name + "/" + tiles.name + " has no two-tone enemy-field checkerboard.");
            }
            stages++;
        }
        Require(stages == 16, "Expected all sixteen authored battlefield stages.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

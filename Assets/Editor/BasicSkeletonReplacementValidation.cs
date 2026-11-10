using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BasicSkeletonReplacementValidation
{
    private const string PrefabPath = "Assets/Enemy/Brick.prefab";
    private const string UpperArtPath = "Assets/MonsterSprite/ForestSkeletonUpperJaw.png";
    private const string LowerArtPath = "Assets/MonsterSprite/ForestSkeletonLowerJaw.png";

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run only in an isolated Unity project copy.");
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Sprite upper = AssetDatabase.LoadAssetAtPath<Sprite>(UpperArtPath);
            Sprite lower = AssetDatabase.LoadAssetAtPath<Sprite>(LowerArtPath);
            TextureImporter upperImporter = AssetImporter.GetAtPath(UpperArtPath) as TextureImporter;
            TextureImporter lowerImporter = AssetImporter.GetAtPath(LowerArtPath) as TextureImporter;
            Require(prefab != null && upper != null && lower != null
                && upperImporter != null && lowerImporter != null,
                "Both skeleton appearances and the base prefab are importable");
            Require(Mathf.Approximately(upperImporter.spritePixelsPerUnit, 1254f)
                && Mathf.Approximately(lowerImporter.spritePixelsPerUnit, 1254f),
                "Both skeleton sprites retain their one-unit import scale");

            Brick enemy = prefab.GetComponent<Brick>();
            Transform visual = prefab.transform.Find("Visual");
            SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            EnemyAppearanceVariants appearances = visual != null
                ? visual.GetComponent<EnemyAppearanceVariants>() : null;
            Require(enemy != null && renderer != null && renderer.sprite == upper
                && visual.GetComponent<Animator>() == null,
                "Editable base prefab previews the upper-jaw skeleton without old animation");
            Require(appearances != null && appearances.AppearanceCount == 2
                && appearances.GetAppearanceSprite(0) == upper
                && appearances.GetAppearanceSprite(1) == lower
                && appearances.GetAppearanceScale(0) == Vector3.one * 1.45f
                && appearances.GetAppearanceScale(1) == Vector3.one * 1.45f
                && visual.localScale == Vector3.one * 1.45f,
                "One enemy prefab contains two editable skeleton appearances");
            EnemyFirstEncounterProfile profile = prefab.GetComponent<EnemyFirstEncounterProfile>();
            Require(profile != null && profile.Portrait == upper,
                "Encounter card previews the upper-jaw skeleton appearance");
            Require(Mathf.Approximately(enemy.hp, 100f)
                && Mathf.Approximately(enemy.dmg, 1f)
                && enemy.ExperienceReward == 10
                && enemy.Footprint == Vector2Int.one,
                "Base enemy balance and one-cell footprint are unchanged");
            Require(prefab.GetComponent<BoxCollider2D>() != null
                && visual.GetComponent<EnemyVisualHover>() != null,
                "Skeleton retains its physical collider and hovering motion");

            EditorSceneManager.OpenScene("Assets/Asset.unity");
            LevelGenerator[] rooms = Object.FindObjectsOfType<LevelGenerator>(true);
            Require(rooms.Length > 0 && rooms.All(room => room.brickPrefab == prefab),
                "Every gameplay room references the updated basic enemy prefab");
            Debug.Log("PASS: Both base skeleton appearances, prefab and scene references validated.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogError("FAIL: " + error);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("PASS: " + message);
    }
}

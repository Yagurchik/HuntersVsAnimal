using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArmoredSkeletonValidation
{
    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        var report = new List<string>();
        try
        {
            TextureImporter importer = AssetImporter.GetAtPath(ArmoredSkeletonAuthoring.ArtPath)
                as TextureImporter;
            Check(importer != null && importer.textureType == TextureImporterType.Sprite
                && importer.alphaIsTransparency && !importer.mipmapEnabled,
                "Artwork is imported as a transparent, mobile-friendly sprite", report);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ArmoredSkeletonAuthoring.PrefabPath);
            Check(prefab != null, "Armored skeleton prefab exists in Resources/Enemies", report);
            Brick brick = prefab.GetComponent<Brick>();
            ArmoredSkeletonArmor armor = prefab.GetComponent<ArmoredSkeletonArmor>();
            SpriteRenderer renderer = prefab.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            Check(brick != null && armor != null && renderer != null && renderer.sprite != null,
                "Prefab contains combat, armor and authored visual components", report);
            Check(brick.Footprint == Vector2Int.one && Mathf.Approximately(brick.hp, 100f)
                && Mathf.Approximately(brick.HealthCoefficient, 1f)
                && brick.ExperienceReward == 20,
                "Enemy uses ordinary health while keeping its own experience reward", report);
            Check(Mathf.Approximately(armor.TopDamageMultiplier, 0.5f)
                && Mathf.Approximately(armor.ProtectedZoneStart, 0.5f),
                "Upper half armor is configured to retain exactly 50 percent damage", report);

            EditorSceneManager.OpenScene(GameUIPreviewWindow.GameplayPath);
            LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
            Check(progression != null
                && progression.GetArmoredSkeletonCountPerWave(5, 1, 1) == 0
                && progression.GetArmoredSkeletonCountPerWave(6, 1, 1) == 1
                && progression.GetArmoredSkeletonCountPerWave(10, 4, 7) == 1,
                "Ordinary Gloomy Hollow waves replace one base enemy with an armored skeleton",
                report);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.position = Vector3.zero;
                ArmoredSkeletonArmor liveArmor = instance.GetComponent<ArmoredSkeletonArmor>();
                Collider2D collider = instance.GetComponent<Collider2D>();
                Vector2 top = new Vector2(collider.bounds.center.x, collider.bounds.max.y - 0.01f);
                Vector2 bottom = new Vector2(collider.bounds.center.x, collider.bounds.min.y + 0.01f);
                float topDamage = liveArmor.ModifyDamage(instance.GetComponent<Brick>(), null, top, 100f);
                Check(Mathf.Approximately(topDamage, 50f) && liveArmor.LastHitWasBlocked,
                    "A direct upper hit is reduced from 100 to 50 damage", report);
                float bottomDamage = liveArmor.ModifyDamage(instance.GetComponent<Brick>(), null, bottom, 100f);
                Check(Mathf.Approximately(bottomDamage, 100f) && !liveArmor.LastHitWasBlocked,
                    "A lower hit keeps its full 100 damage", report);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            report.Add("PASS: " + report.Count + " armored skeleton checks.");
            File.WriteAllLines("Logs/ArmoredSkeletonValidation.txt", report);
            Debug.Log(report[report.Count - 1]);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            report.Add("FAIL: " + error);
            File.WriteAllLines("Logs/ArmoredSkeletonValidation.txt", report);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void Check(bool condition, string description, List<string> report)
    {
        if (!condition) throw new Exception(description);
        report.Add("PASS " + description);
    }
}

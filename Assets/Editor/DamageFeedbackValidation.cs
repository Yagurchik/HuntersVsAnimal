using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DamageFeedbackValidation
{
    public static void RunBatch()
    {
        CheckOutlineProportions();
        CheckColors();
        CheckAggregatedNumbers();
        CheckSavedOutlines("Assets/Scenes/UIscene.unity");
        CheckSavedOutlines("Assets/Asset.unity");
        CheckSavedPrefabOutlines();
        Debug.Log("Damage color, end-turn aggregation and typography validation passed.");
    }

    private static void CheckOutlineProportions()
    {
        GameObject objectSmall = new GameObject("Small type", typeof(RectTransform), typeof(Text));
        GameObject objectLarge = new GameObject("Large type", typeof(RectTransform), typeof(Text));
        try
        {
            Text small = objectSmall.GetComponent<Text>();
            Text large = objectLarge.GetComponent<Text>();
            small.fontSize = 12;
            large.fontSize = 36;
            GameTypography.Apply(small);
            GameTypography.Apply(large);
            float smallOutline = small.GetComponent<Outline>().effectDistance.x;
            float largeOutline = large.GetComponent<Outline>().effectDistance.x;
            Require(Mathf.Approximately(smallOutline / small.fontSize,
                    largeOutline / large.fontSize)
                && Mathf.Approximately(smallOutline / small.fontSize,
                    GameTypography.LegacyOutlineRatio),
                "UI Text outlines must retain the same proportion at different sizes");
            large.resizeTextForBestFit = true;
            large.resizeTextMinSize = 18;
            GameTypography.Apply(large);
            Require(Mathf.Approximately(large.GetComponent<Outline>().effectDistance.x,
                18f * GameTypography.LegacyOutlineRatio),
                "Best-fit text outline must protect the smallest rendered face");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(objectSmall);
            UnityEngine.Object.DestroyImmediate(objectLarge);
        }
    }

    private static void CheckColors()
    {
        int[] indices = {
            AnimalProjectileQueue.FireflyIndex, AnimalProjectileQueue.RabbitIndex,
            AnimalProjectileQueue.HedgehogIndex, AnimalProjectileQueue.HoneyBadgerIndex,
            AnimalProjectileQueue.BoarIndex, AnimalProjectileQueue.BeaverIndex
        };
        CombatDamageSource[] sources = {
            CombatDamageSource.Firefly, CombatDamageSource.Rabbit,
            CombatDamageSource.Hedgehog, CombatDamageSource.HoneyBadger,
            CombatDamageSource.Boar, CombatDamageSource.Beaver
        };
        Color32[] colors = {
            new Color32(255, 135, 42, 255), new Color32(255, 255, 255, 255),
            new Color32(166, 104, 53, 255), new Color32(112, 205, 75, 255),
            new Color32(255, 222, 67, 255), new Color32(216, 154, 91, 255)
        };
        for (int i = 0; i < indices.Length; i++)
        {
            Require(CombatDamagePalette.FromAnimalIndex(indices[i]) == sources[i],
                "Wrong projectile damage type at index " + indices[i]);
            Require(((Color32)CombatDamagePalette.ColorFor(sources[i])).Equals(colors[i]),
                "Wrong projectile damage color at index " + indices[i]);
        }
        Require(CombatDamagePalette.ColorFor(CombatDamageSource.Fire)
            == CombatDamagePalette.ColorFor(CombatDamageSource.Firefly),
            "Fire uses firefly orange");
        Require(CombatDamagePalette.ColorFor(CombatDamageSource.Poison)
            == CombatDamagePalette.ColorFor(CombatDamageSource.HoneyBadger),
            "Poison uses honey badger green");
    }

    private static void CheckAggregatedNumbers()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        GameObject enemyObject = new GameObject("Damage target", typeof(Brick));
        Brick enemy = enemyObject.GetComponent<Brick>();
        EnemyCombatTextUI ui = EnemyCombatTextUI.Ensure();
        Canvas.ForceUpdateCanvases();
        ui.BeginAfterTurnBatch();
        ui.QueueAfterTurnDamage(enemy, 40f, CombatDamageSource.Fire);
        ui.QueueAfterTurnDamage(enemy, 60f, CombatDamageSource.Fire);
        ui.QueueAfterTurnDamage(enemy, 15f, CombatDamageSource.Poison);
        Require(ui.ActiveDamageNumberCount == 0,
            "After-turn numbers wait until all effects have resolved");
        ui.FinishAfterTurnBatch();
        Require(ui.ActiveDamageNumberCount == 2,
            "One number per damage type is shown for an enemy");
        TextMeshProUGUI[] labels = ui.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Where(label => label.gameObject.activeSelf).ToArray();
        Require(labels.Length == 2
            && labels.Any(label => label.text == "ОГОНЬ -100"
                && label.color == CombatDamagePalette.ColorFor(CombatDamageSource.Fire))
            && labels.Any(label => label.text == "ЯД -15"
                && label.color == CombatDamagePalette.ColorFor(CombatDamageSource.Poison)),
            "Fire and poison totals keep their separate colors and labels");
    }

    private static void CheckSavedOutlines(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath);
        Text[] labels = UnityEngine.Object.FindObjectsOfType<Text>(true);
        Require(labels.Length > 0 && labels.All(HasProportionalOutline),
            "Saved UI Text lacks a proportional black outline in " + scenePath);
        TMP_FontAsset font = GameTypography.TmpFont;
        Require(font != null && font.material != null
            && Mathf.Approximately(font.material.GetFloat(ShaderUtilities.ID_OutlineWidth),
                GameTypography.TmpOutlineWidth)
            && Mathf.Approximately(font.material.GetFloat(ShaderUtilities.ID_FaceDilate),
                GameTypography.TmpFaceDilate)
            && font.material.GetColor(ShaderUtilities.ID_OutlineColor) == Color.black
            && font.material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline),
            "Shared TMP font material lacks its balanced black outline");
        TMP_Text[] tmpLabels = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
        Require(tmpLabels.All(label => label.fontSharedMaterial == font.material),
            "Saved TMP labels do not all use the outlined font material in " + scenePath);
    }

    private static void CheckSavedPrefabOutlines()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)
                || path.StartsWith("Assets/_Heathen Engineering/", StringComparison.Ordinal)) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            Require(prefab.GetComponentsInChildren<Text>(true).All(HasProportionalOutline),
                "Saved prefab Text lacks a proportional black outline in " + path);
            Require(prefab.GetComponentsInChildren<TMP_Text>(true)
                    .All(label => label.fontSharedMaterial == GameTypography.TmpFont.material),
                "Saved prefab TMP label uses another outline material in " + path);
        }
    }

    private static bool HasProportionalOutline(Text label)
    {
        Outline outline = label.GetComponent<Outline>();
        float distance = GameTypography.LegacyOutlineDistanceFor(label);
        return outline != null
            && outline.effectColor == Color.black
            && outline.effectDistance == new Vector2(distance, -distance);
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
}

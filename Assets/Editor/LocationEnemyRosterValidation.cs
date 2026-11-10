using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Read-only asset check for the complete ordinary enemy roster.
public static class LocationEnemyRosterValidation
{
    public static void RunBatch()
    {
        try
        {
            string[,] paths = (string[,])typeof(LevelGenerator)
                .GetField("LocationEnemyPaths", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            if (paths.GetLength(0) != LevelProgression.LocationCount || paths.GetLength(1) != 5)
                throw new Exception("Expected five ordinary enemies in each of six locations.");

            int checkedPrefabs = 0;
            for (int location = 0; location < paths.GetLength(0); location++)
            for (int slot = 0; slot < paths.GetLength(1); slot++)
            {
                string name = paths[location, slot];
                string assetPath = location == 0 && slot == 0
                    ? "Assets/Enemy/Brick.prefab"
                    : "Assets/Resources/Enemies/" + name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) throw new Exception("Missing enemy prefab: " + assetPath);
                if (location != 0 || slot != 0)
                {
                    GameObject resource = Resources.Load<GameObject>("Enemies/" + name);
                    if (resource != prefab)
                        throw new Exception("Runtime enemy path cannot resolve: " + name);
                }
                Brick enemy = prefab.GetComponent<Brick>();
                if (enemy == null || enemy.UsesBossHealth || enemy.Footprint != Vector2Int.one)
                    throw new Exception("Invalid one-cell ordinary enemy: " + assetPath);
                Transform visual = prefab.transform.Find("Visual");
                SpriteRenderer head = visual != null
                    ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
                if (head == null || head.sprite == null || !head.enabled
                    || !head.gameObject.activeSelf)
                    throw new Exception("Enemy head is missing or disabled: " + assetPath);
                if (prefab.GetComponent<EnemyDarknessVisibility>() == null)
                    throw new Exception("Enemy cannot restore visibility: " + assetPath);
                if (location == 0 && slot == 0)
                {
                    EnemyAppearanceVariants variants = visual.GetComponent<EnemyAppearanceVariants>();
                    if (variants == null || variants.AppearanceCount != 2
                        || variants.GetAppearanceSprite(0) == null
                        || variants.GetAppearanceSprite(1) == null
                        || visual.GetComponent<Animator>() != null)
                        throw new Exception("Basic skeleton has a missing variant or stale animator.");
                }
                if (location == 4)
                {
                    WinterEnemyForm winter = prefab.GetComponent<WinterEnemyForm>();
                    SerializedObject settings = winter != null ? new SerializedObject(winter) : null;
                    if (settings == null
                        || settings.FindProperty("frozenSprite").objectReferenceValue == null
                        || settings.FindProperty("thawedSprite").objectReferenceValue == null)
                        throw new Exception("Missing frozen or thawed enemy form: " + assetPath);
                }
                checkedPrefabs++;
            }

            ValidateWaveComposition();

            GameObject sample = new GameObject("Visibility restore validation");
            try
            {
                SpriteRenderer renderer = sample.AddComponent<SpriteRenderer>();
                EnemyDarknessVisibility visibility = sample.AddComponent<EnemyDarknessVisibility>();
                renderer.forceRenderingOff = true;
                visibility.RefreshVisibility();
                if (renderer.forceRenderingOff)
                    throw new Exception("A visible enemy kept a stale forceRenderingOff flag.");
            }
            finally { Object.DestroyImmediate(sample); }

            GameObject fog = new GameObject("Shadow visibility validation",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RoomDarknessGraphic));
            try
            {
                RoomDarknessGraphic graphic = fog.GetComponent<RoomDarknessGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(8f, 8f);
                var shadows = new[] {
                    new RoomDarknessGraphic.ShadowDisc(Vector2.zero, 1f, 0.2f)
                };
                var lights = new[] {
                    new RoomDarknessGraphic.LightDisc(Vector2.zero, 1f, 0.2f)
                };
                graphic.Configure(1f, 0.5f, new Color(0f, 0f, 0f, 0.55f), null, shadows);
                if (graphic.GetAlphaAt(Vector2.zero) < 0.4f
                    || graphic.GetAlphaAt(new Vector2(3f, 3f)) > 0.001f)
                    throw new Exception("Shadow fails to dim only its visible area.");
                graphic.Configure(1f, 0.5f, new Color(0f, 0f, 0f, 0.55f), lights, shadows);
                if (graphic.GetAlphaAt(Vector2.zero) > 0.001f)
                    throw new Exception("Firefly light fails to reveal the shadowed area.");
            }
            finally { Object.DestroyImmediate(fog); }

            Debug.Log("LocationEnemyRosterValidation passed: " + checkedPrefabs
                + " editable enemy prefabs load with visible head sprites; all five types spawn"
                + " in each location; stale concealment clears.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateWaveComposition()
    {
        FieldInfo instanceField = typeof(LevelProgression).GetField(
            "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo currentLevelField = typeof(LevelProgression).GetField(
            "currentLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo compositionField = typeof(LevelGenerator).GetField(
            "waveComposition", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo buildMethod = typeof(LevelGenerator).GetMethod(
            "BuildWaveComposition", BindingFlags.Instance | BindingFlags.NonPublic);
        if (instanceField == null || currentLevelField == null || compositionField == null
            || buildMethod == null)
            throw new Exception("Cannot inspect enemy wave composition.");

        object previousInstance = instanceField.GetValue(null);
        GameObject progressionObject = new GameObject("Roster progression validation");
        GameObject generatorObject = new GameObject("Roster wave validation");
        try
        {
            LevelProgression progression = progressionObject.AddComponent<LevelProgression>();
            LevelGenerator generator = generatorObject.AddComponent<LevelGenerator>();
            generator.brickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Brick.prefab");
            instanceField.SetValue(null, progression);
            for (int location = 1; location <= LevelProgression.LocationCount; location++)
            {
                var encountered = new HashSet<GameObject>();
                var signatures = new HashSet<string>();
                for (int local = 1; local <= LevelProgression.LevelsPerLocation; local++)
                {
                    int level = LevelProgression.GlobalLevelFor(location, local);
                    currentLevelField.SetValue(progression, level);
                    var levelTypes = new HashSet<string>();
                    for (int line = 1; line <= 4; line++)
                    for (int room = 1; room <= line * 2 - 1; room++)
                    for (int wave = 1; wave <= LevelProgression.CampaignWavesPerRoom; wave++)
                    {
                        generator.ConfigureRoomIdentity(line, room);
                        int count = progression.GetCampaignWaveEnemyCount(level, line, room, wave);
                        buildMethod.Invoke(generator, new object[] { count, wave });
                        var composition = (List<GameObject>)compositionField.GetValue(generator);
                        if (composition.Count != count || composition.Exists(prefab => prefab == null))
                            throw new Exception("Wrong enemy count in level " + level + " wave " + wave);
                        var distinct = new HashSet<GameObject>(composition);
                        if (distinct.Count > progression.GetMaximumEnemyTypesForWave(line, wave))
                            throw new Exception("Wave type limit exceeded in level " + level);
                        foreach (GameObject prefab in composition) { encountered.Add(prefab); levelTypes.Add(prefab.name); }
                    }
                    var sorted = new List<string>(levelTypes); sorted.Sort();
                    if (!signatures.Add(string.Join(",", sorted)))
                        throw new Exception("Repeated ordinary roster within location " + location);
                }
                if (encountered.Count != 5)
                    throw new Exception("Location " + location + " spawns only "
                        + encountered.Count + " of its five enemy types.");
            }
        }
        finally
        {
            instanceField.SetValue(null, previousInstance);
            Object.DestroyImmediate(generatorObject);
            Object.DestroyImmediate(progressionObject);
        }
    }
}

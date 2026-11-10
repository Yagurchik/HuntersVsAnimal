using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GloomyHollowAuthoring
{
    public const string RegularLichPath = "Assets/Resources/Enemies/LichRegular.prefab";
    private const string MenuScenePath = "Assets/Scenes/UIscene.unity";

    [MenuItem("Tools/Forest Animal War/Locations/Prepare Gloomy Hollow")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before preparing Gloomy Hollow.");

        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.PrefabPath) == null)
            LichBossAuthoring.Install();
        CreateOrUpdateRegularLich();
        AddLocationControlsToMenu();
        ExpandGameplaySceneProgression();
        AssetDatabase.SaveAssets();
        Debug.Log("Gloomy Hollow ready: levels 6-10, editable location selector, and independent 1x1 Lich.");
    }

    public static void InstallBatch()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CreateOrUpdateRegularLich()
    {
        Directory.CreateDirectory("Assets/Resources/Enemies");
        GameObject source = PrefabUtility.LoadPrefabContents(LichBossAuthoring.PrefabPath);
        try
        {
            GameObject regular = Object.Instantiate(source);
            regular.name = "LichRegular";
            regular.transform.localScale = Vector3.one;
            regular.transform.localRotation = Quaternion.identity;

            BoxCollider2D collider = regular.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = new Vector2(0.9f, 0.9f);
                collider.offset = Vector2.zero;
            }

            Brick brick = regular.GetComponent<Brick>();
            GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.SkeletonPath);
            Brick skeletonBrick = skeleton != null ? skeleton.GetComponent<Brick>() : null;
            brick.hp = skeletonBrick != null ? skeletonBrick.hp : 100f;
            if (skeletonBrick != null) brick.dmg = skeletonBrick.dmg;
            var brickSettings = new SerializedObject(brick);
            brickSettings.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            brickSettings.FindProperty("experienceReward").intValue = 30;
            GameObject previousPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RegularLichPath);
            brickSettings.FindProperty("healthCoefficient").floatValue = previousPrefab != null
                ? previousPrefab.GetComponent<Brick>().HealthCoefficient : 1f;
            brickSettings.FindProperty("bossHealth").boolValue = false;
            brickSettings.FindProperty("stepJumpHeight").floatValue = 0f;
            brickSettings.FindProperty("stepStretch").floatValue = 0f;
            brickSettings.FindProperty("landingSquash").floatValue = 0f;
            brickSettings.ApplyModifiedPropertiesWithoutUndo();

            Transform visual = regular.transform.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
                visual.localScale = Vector3.one * 0.55f;
            }

            LichSummoner summoner = regular.GetComponent<LichSummoner>();
            if (summoner == null) summoner = regular.AddComponent<LichSummoner>();
            var summonSettings = new SerializedObject(summoner);
            summonSettings.FindProperty("skeletonPrefab").objectReferenceValue = skeleton;
            summonSettings.FindProperty("minimumSummons").intValue = 1;
            summonSettings.FindProperty("maximumSummons").intValue = 1;
            summonSettings.FindProperty("maximumLivingSummons").intValue = 3;
            summonSettings.FindProperty("summonedSkeletonTint").colorValue =
                new Color(0.78f, 1f, 0.72f, 1f);
            summonSettings.ApplyModifiedPropertiesWithoutUndo();

            regular.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(regular, RegularLichPath);
            Object.DestroyImmediate(regular);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }
    }

    private static void AddLocationControlsToMenu()
    {
        if (!File.Exists(MenuScenePath)) return;
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null || hub.tabs.Length == 0 || hub.tabs[0].page == null)
            throw new InvalidOperationException("Editable MenuHub campaign page was not found in UIscene.");

        Transform caption = hub.tabs[0].page.transform.Find("Viewport/Content/Forest Hero/Hero Caption");
        if (caption == null) throw new InvalidOperationException("Forest Hero caption was not found in UIscene.");

        Transform titleTransform = caption.Find("Hero Title");
        Text title = titleTransform != null ? titleTransform.GetComponent<Text>() : null;
        if (title == null)
            title = MenuHubBuilder.Label("Hero Title", caption, "СУМЕРЕЧНАЯ ЧАЩА", 32,
                new Color(0.94f, 0.66f, 0.27f), 96, 12, 800, 51, true, TextAnchor.MiddleCenter);
        MenuHubBuilder.Top(title.rectTransform, 96, 12, 800, 51);
        title.alignment = TextAnchor.MiddleCenter;
        hub.locationText = title;

        Transform detailTransform = caption.Find("Hero Detail");
        if (detailTransform != null && detailTransform.TryGetComponent(out Text detail))
        {
            MenuHubBuilder.Top(detail.rectTransform, 96, 69, 800, 42);
            detail.alignment = TextAnchor.MiddleCenter;
        }

        hub.previousLocationButton = EnsureLocationButton(caption, "Previous Location", "‹", 20);
        hub.nextLocationButton = EnsureLocationButton(caption, "Next Location", "›", 914);
        if (hub.previousLocationButton.onClick.GetPersistentEventCount() == 0)
            UnityEventTools.AddPersistentListener(hub.previousLocationButton.onClick, hub.ShowPreviousLocation);
        if (hub.nextLocationButton.onClick.GetPersistentEventCount() == 0)
            UnityEventTools.AddPersistentListener(hub.nextLocationButton.onClick, hub.ShowNextLocation);

        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ExpandGameplaySceneProgression()
    {
        const string gameplayScenePath = "Assets/Asset.unity";
        if (!File.Exists(gameplayScenePath)) return;
        Scene scene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Single);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null)
            throw new InvalidOperationException("LevelProgression was not found in the gameplay scene.");

        progression.SendMessage("NormalizeLevelData", SendMessageOptions.RequireReceiver);
        var data = new SerializedObject(progression);
        SerializedProperty rules = data.FindProperty("zombieSpawnRules");
        for (int i = 0; rules != null && i < rules.arraySize; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            SerializedProperty lastLevel = rule.FindPropertyRelative("lastLevel");
            if (lastLevel != null && lastLevel.intValue == LevelProgression.LevelsPerLocation)
                lastLevel.intValue = LevelProgression.MaxLevel;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progression);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Button EnsureLocationButton(Transform parent, string name, string label, float x)
    {
        Transform existing = parent.Find(name);
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
            button = MenuHubBuilder.Button(name, parent, label, 42,
                new Color(0.15f, 0.24f, 0.2f), x, 28, 58, 72);
        MenuHubBuilder.Top(button.GetComponent<RectTransform>(), x, 28, 58, 72);
        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null) text.text = label;
        return button;
    }
}

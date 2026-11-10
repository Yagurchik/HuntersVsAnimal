using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UpgradeChoiceUIBuilder
{
    private const string GameplayScene = "Assets/Asset.unity";
    private static readonly string[] PortraitPaths = {
        "Assets/MonsterSprite/ProjectileArt/firefly-v1.png",
        "Assets/MonsterSprite/ProjectileArt/hedgehog-v1.png",
        "Assets/MonsterSprite/ProjectileArt/rabbit-v1.png",
        "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-v1.png",
        "Assets/MonsterSprite/ProjectileArt/Boar/boar-projectile.png"
    };

    [MenuItem("Forest Animal War/UI/Пересобрать красочный выбор улучшений", false, 12)]
    public static void Install()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != GameplayScene)
            throw new InvalidOperationException("Открой Assets/Asset.unity вне Play Mode.");
        ExperienceAndUpgradeUI ui = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
        AnimalProjectileQueue queue = GameUIPreviewWindow.Find<AnimalProjectileQueue>();
        if (ui == null || queue == null)
            throw new InvalidOperationException("В сцене отсутствует система опыта или очередь снарядов.");

        Undo.RegisterFullObjectHierarchyUndo(ui.gameObject, "Rebuild colorful upgrade choice");
        Build(ui, queue);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Canvas.ForceUpdateCanvases();
        Selection.activeGameObject = ui.UpgradeModal;
    }

    private static void Build(ExperienceAndUpgradeUI ui, AnimalProjectileQueue queue)
    {
        var portraits = new Sprite[PortraitPaths.Length];
        for (int i = 0; i < portraits.Length; i++)
        {
            portraits[i] = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPaths[i]);
            if (portraits[i] == null)
                throw new InvalidOperationException("Не найден портрет: " + PortraitPaths[i]);
        }
        ui.RebuildUpgradeModal(portraits);
        ui.Preview(queue, true);
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            EditorSceneManager.OpenScene(GameplayScene);
            ExperienceAndUpgradeUI ui = GameUIPreviewWindow.Find<ExperienceAndUpgradeUI>();
            AnimalProjectileQueue queue = GameUIPreviewWindow.Find<AnimalProjectileQueue>();
            Build(ui, queue);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Colorful three-card projectile upgrade UI saved.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}

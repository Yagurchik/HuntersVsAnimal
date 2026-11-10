using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyFirstEncounterAuthoring
{
    private const string GameplayScene = "Assets/Asset.unity";

    private struct ProfileSetup
    {
        public string Path;
        public string Id;
        public string Title;
        public string Summary;
        public Color Accent;

        public ProfileSetup(string path, string id, string title, string summary, Color accent)
        {
            Path = path;
            Id = id;
            Title = title;
            Summary = summary;
            Accent = accent;
        }
    }

    [MenuItem("Forest Animal War/UI/Собрать карточку первой встречи", false, 15)]
    public static void Install()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Карточку первой встречи можно собирать только вне Play Mode.");

        ConfigureEnemyProfiles();
        if (SceneManager.GetActiveScene().path != GameplayScene)
            EditorSceneManager.OpenScene(GameplayScene);

        EnemyFirstEncounterUI ui = UnityEngine.Object.FindObjectOfType<EnemyFirstEncounterUI>(true);
        if (ui == null)
        {
            GameObject root = new GameObject("Enemy First Encounter UI",
                typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
                typeof(EnemyFirstEncounterUI));
            Undo.RegisterCreatedObjectUndo(root, "Create enemy first encounter UI");
            ui = root.GetComponent<EnemyFirstEncounterUI>();
        }
        ui.BuildEditableUI();
        ui.PreviewInEditor();
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = ui.gameObject;
        Debug.Log("Enemy first encounter card and enemy profiles installed.");
    }

    private static void ConfigureEnemyProfiles()
    {
        ProfileSetup[] setups =
        {
            new ProfileSetup("Assets/Enemy/Brick.prefab", "forest_skeleton", "Лесной скелет",
                "Шагает к линии поражения на одну клетку за ход. Останови его до прорыва.",
                new Color(0.92f, 0.49f, 0.16f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/Zombie.prefab", "zombie", "Зомби",
                "После первой смерти оставляет надгробие с полным запасом здоровья. Разбей его за ход, иначе зомби восстанет с половиной здоровья.",
                new Color(0.58f, 0.72f, 0.25f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/ArmoredSkeleton.prefab", "armored_skeleton",
                "Скелет в доспехах",
                "Атаки сверху наносят ему только половину урона. Ищи боковой или нижний угол.",
                new Color(0.72f, 0.76f, 0.68f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/Ghost.prefab", "ghost", "Призрак",
                "Имеет 80% здоровья обычного врага. Каждый снаряд с шансом 20% проходит сквозь него; уже наложенный огонь он не избегает.",
                new Color(0.6f, 0.85f, 0.78f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/SwampDemon.prefab", "swamp_demon",
                "Болотный демон",
                "Рядовой враг Топей. Невосприимчив к яду; при попадании разбрызгивает болотную жижу.",
                new Color(0.58f, 0.67f, 0.17f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/BogPustulant.prefab", "bog_pustulant",
                "Болотный гноевик",
                "При смерти взрывается: снаряды теряют 5 здоровья, враги в радиусе двух клеток получают яд на величину его максимального здоровья. Если горит от светлячка, взрыв наносит 130% максимального здоровья огнём.",
                new Color(0.72f, 0.72f, 0.27f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/LichRegular.prefab", "lich", "Лич",
                "Каждый ход призывает зелёного скелета с 25% своего текущего здоровья. Может поддерживать не больше трёх призывов.",
                new Color(0.52f, 0.82f, 0.38f, 1f)),
            new ProfileSetup("Assets/Resources/Enemies/LichBoss.prefab", "lich_boss", "Лич-хранитель",
                "Занимает четыре клетки. Призывы получают 25% его текущего здоровья; одновременно их может быть не больше шести.",
                new Color(0.52f, 0.82f, 0.38f, 1f))
        };

        for (int i = 0; i < setups.Length; i++) ConfigureProfile(setups[i]);
    }

    private static void ConfigureProfile(ProfileSetup setup)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(setup.Path);
        try
        {
            Brick enemy = root.GetComponent<Brick>();
            if (enemy == null) throw new InvalidOperationException("Prefab has no Brick: " + setup.Path);
            EnemyFirstEncounterProfile profile = root.GetComponent<EnemyFirstEncounterProfile>();
            if (profile == null) profile = root.AddComponent<EnemyFirstEncounterProfile>();
            profile.Configure(setup.Id, setup.Title, setup.Summary, FindPortrait(root), setup.Accent);
            EditorUtility.SetDirty(profile);
            PrefabUtility.SaveAsPrefabAsset(root, setup.Path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite FindPortrait(GameObject root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        Sprite fallback = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.sprite == null) continue;
            if (fallback == null) fallback = renderer.sprite;
            if (renderer.enabled && renderer.gameObject.activeSelf) return renderer.sprite;
        }
        return fallback;
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        try
        {
            EditorSceneManager.OpenScene(GameplayScene);
            Install();
            Debug.Log("Enemy first encounter authoring completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}

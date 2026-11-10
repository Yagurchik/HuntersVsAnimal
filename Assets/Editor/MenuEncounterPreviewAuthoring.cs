using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Reads authored campaign encounters and saves their prefab references in the
// editable menu scene. Runtime swaps art in the location illustration overlay.
public static class MenuEncounterPreviewAuthoring
{
    private const string GameScene = "Assets/Asset.unity";
    private const string MenuScene = "Assets/Scenes/UIscene.unity";
    private const string CardPrefab = "Assets/UI/MenuPrefabs/LevelCard.prefab";

    [MenuItem("Forest Animal War/UI/Bake level enemy portraits")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before baking the menu.");
        Scene gameplay = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        if (progression == null) throw new InvalidOperationException("Campaign progression is missing.");
        progression.ApplyCampaignWaveRules();
        EditorUtility.SetDirty(progression);
        EditorSceneManager.MarkSceneDirty(gameplay);
        EditorSceneManager.SaveScene(gameplay);
        var previews = new MenuHubController.LevelEncounterPreview[LevelProgression.MaxLevel];
        for (int level = 1; level <= previews.Length; level++)
        {
            int location = LevelProgression.LocationIndexForLevel(level);
            var enemies = new List<GameObject>(8);
            for (int slot = 0; slot < 5; slot++)
            {
                if (slot > 0 && !ContainsSlot(progression.GetSpecialistSlots(level), slot)) continue;
                string resource = LevelGenerator.GetLocationEnemyResourceName(location, slot);
                string path = location == 1 && slot == 0
                    ? "Assets/Enemy/Brick.prefab"
                    : "Assets/Resources/Enemies/" + resource + ".prefab";
                AddEnemy(enemies, path);
            }
            for (int line = 2; line <= 4; line++)
            {
                if (!progression.IsRoomActive(level, line, line)) continue;
                string resource = BossResource(progression.GetBossKind(level, line, line));
                if (resource != null)
                    AddEnemy(enemies, "Assets/Resources/Enemies/" + resource + ".prefab");
            }
            if (enemies.Count != progression.GetSpecialistSlots(level).Count + 4)
                throw new InvalidOperationException("Level " + level
                    + " needs its ordinary roster and three different bosses.");
            previews[level - 1] = new MenuHubController.LevelEncounterPreview {
                enemies = enemies.ToArray()
            };
        }
        EditorSceneManager.CloseScene(gameplay, true);

        Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.levels == null
            || hub.levels.Length < LevelProgression.LevelsPerLocation)
            throw new InvalidOperationException("Five editable level cards are missing from UIscene.");
        hub.encounterPreviews = previews;
        int visibleCards = 0;
        foreach (MenuLevelCard card in hub.levels)
        {
            if (card == null) throw new InvalidOperationException("A level card is missing.");
            if (card.levelNumber > LevelProgression.LevelsPerLocation) continue;
            RestoreLevelCard(card);
            card.Refresh(1);
            EditorUtility.SetDirty(card);
            visibleCards++;
        }
        if (visibleCards != LevelProgression.LevelsPerLocation)
            throw new InvalidOperationException("The five visible level cards are incomplete.");
        Transform content = hub.tabs[0].page.transform.Find("Viewport/Content");
        if (content == null) throw new InvalidOperationException("Campaign content is missing.");
        Transform hero = content.Find("Forest Hero");
        if (hero == null) throw new InvalidOperationException("Location illustration is missing.");
        EnsureLocationEncounterSlots(hub, hero);
        hub.RefreshEncounterPreview(1);
        SetTop(content.Find("Play Selected Level") as RectTransform, 44, 1060, 992, 114);
        SetTop(content.Find("Loadout Hint") as RectTransform, 44, 1201, 992, 72);
        SetTop(content.Find("Survival Mode Card") as RectTransform, 44, 1292, 992, 235);
        RectTransform contentRect = (RectTransform)content;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 1570f);
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);

        GameObject template = PrefabUtility.LoadPrefabContents(CardPrefab);
        try
        {
            MenuLevelCard card = template.GetComponent<MenuLevelCard>();
            if (card == null) throw new InvalidOperationException("LevelCard prefab is missing its component.");
            RestoreLevelCard(card);
            PrefabUtility.SaveAsPrefabAsset(template, CardPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(template); }
        AssetDatabase.SaveAssets();
        Debug.Log("Editable location-screen enemy portraits baked for all 30 campaign levels.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated project copy.");
        try { Bake(); LocationEnemyRosterValidation.ValidateWaveComposition(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void EnsureLocationEncounterSlots(MenuHubController hub, Transform hero)
    {
        Transform overlay = hero.Find("Level Enemy Preview");
        if (overlay == null)
        {
            var root = new GameObject("Level Enemy Preview", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(hero, false);
            overlay = root.transform;
        }
        SetTop((RectTransform)overlay, 0, 235, 992, 178);
        overlay.SetAsLastSibling();
        Image background = overlay.GetComponent<Image>();
        background.color = new Color(0.035f, 0.085f, 0.065f, 0.93f);
        background.raycastTarget = false;
        hub.encounterTitle = EnsureText(overlay, "Title", 24,
            new Color(1f, 0.91f, 0.64f), TextAnchor.MiddleLeft);
        SetTop(hub.encounterTitle.rectTransform, 20, 2, 470, 42);
        Text legend = EnsureText(overlay, "Boss Legend", 16,
            new Color(0.94f, 0.75f, 0.41f), TextAnchor.MiddleRight);
        legend.text = "ЗОЛОТАЯ РАМКА — БОСС";
        SetTop(legend.rectTransform, 601, 5, 370, 32);
        hub.encounterFrames = new Image[8];
        hub.encounterPortraits = new Image[8];
        hub.encounterNames = new Text[8];
        for (int i = 0; i < 8; i++)
        {
            string name = "Enemy " + (i + 1);
            Transform frame = overlay.Find(name);
            if (frame == null)
            {
                var child = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(overlay, false);
                frame = child.transform;
            }
            SetTop((RectTransform)frame, 18 + i * 120, 42, 110, 128);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.raycastTarget = false;
            Transform portrait = frame.Find("Portrait");
            if (portrait == null)
            {
                var child = new GameObject("Portrait", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(frame, false);
                portrait = child.transform;
            }
            SetTop((RectTransform)portrait, 15, 4, 80, 80);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            Text label = EnsureText(frame, "Name", 15, Color.white,
                TextAnchor.MiddleCenter);
            SetTop(label.rectTransform, 3, 88, 104, 36);
            hub.encounterFrames[i] = frameImage;
            hub.encounterPortraits[i] = portraitImage;
            hub.encounterNames[i] = label;
        }
        hub.encounterTitle.transform.SetAsLastSibling();
        legend.transform.SetAsLastSibling();
    }

    private static Text EnsureText(Transform parent, string name, int size,
        Color color, TextAnchor alignment)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var element = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            element.transform.SetParent(parent, false);
            child = element.transform;
        }
        Text text = child.GetComponent<Text>();
        text.font = GameTypography.Font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private static void RestoreLevelCard(MenuLevelCard card)
    {
        Transform oldPortraits = card.transform.Find("Enemy Portraits");
        if (oldPortraits != null) Object.DestroyImmediate(oldPortraits.gameObject);
        SetTop((RectTransform)card.transform, 44 + (card.levelNumber - 1) * 201,
            886, 188, 119);
        SetTop(card.numberText != null ? card.numberText.rectTransform : null,
            0, 12, 188, 60);
        SetTop(card.stateText != null ? card.stateText.rectTransform : null,
            0, 77, 188, 28);
    }

    private static void AddEnemy(List<GameObject> enemies, string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Enemy prefab is missing: " + path);
        EnemyFirstEncounterProfile profile = prefab.GetComponent<EnemyFirstEncounterProfile>();
        Transform visual = prefab.transform.Find("Visual");
        SpriteRenderer renderer = visual != null
            ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
        if ((profile == null || profile.Portrait == null)
            && (renderer == null || renderer.sprite == null))
            throw new InvalidOperationException("Enemy has no portrait: " + path);
        if (!enemies.Contains(prefab)) enemies.Add(prefab);
    }

    private static bool ContainsSlot(IReadOnlyList<int> slots, int slot)
    {
        for (int i = 0; i < slots.Count; i++) if (slots[i] == slot) return true;
        return false;
    }

    public static string BossResource(LevelProgression.BossKind kind)
    {
        switch (kind)
        {
            case LevelProgression.BossKind.Lich: return "LichBoss";
            case LevelProgression.BossKind.BoneDragonMini: return "BoneDragonMiniBoss";
            case LevelProgression.BossKind.DrownedMini: return "DrownedMiniBoss";
            case LevelProgression.BossKind.Mirage: return "MirageBoss";
            case LevelProgression.BossKind.BoneDragon: return "BoneDragonBoss";
            case LevelProgression.BossKind.GhostlySorrow: return "GhostlySorrowBoss";
            case LevelProgression.BossKind.PoisonousGloom: return "PoisonousGloomBoss";
            case LevelProgression.BossKind.HiveHollow: return "HiveHollowMiniBoss";
            case LevelProgression.BossKind.GreenJudge: return "GreenJudgeMiniBoss";
            case LevelProgression.BossKind.BellRinger: return "BellRingerBoss";
            case LevelProgression.BossKind.KnotBinder: return "KnotBinderBoss";
            case LevelProgression.BossKind.VolleyEater: return "VolleyEaterBoss";
            case LevelProgression.BossKind.ThreeSealIdol: return "ThreeSealIdolBoss";
            case LevelProgression.BossKind.Impostor: return "ImpostorBoss";
            default: return null;
        }
    }

    private static void SetTop(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) throw new InvalidOperationException("An editable menu element is missing.");
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}

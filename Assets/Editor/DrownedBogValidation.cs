using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DrownedBogValidation
{
    private const string RuntimeKey = "ForestAnimalWar.DrownedBogValidation.Running";
    private static IEnumerator checks;
    private static double deadline;

    static DrownedBogValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(RuntimeKey, false)) return;
            checks = CheckRuntime();
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update += Tick;
        };
    }

    public static void RunPlayBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        SessionState.SetBool(RuntimeKey, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(15);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        LevelProgression progression = LevelProgression.Instance;
        Require(manager != null && progression != null && manager.Front != null
            && progression.CurrentLevel == 15, "Level 15 campaign is ready.");
        manager.enabled = false;
        manager.isPaused = false;
        Time.timeScale = 1f;
        manager.Front.ActivateLine(2);
        manager.Front.ActivateLine(3);
        manager.Front.ActivateLine(4);
        yield return null;
        LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        Require(room != null && room.IsInitialized && room.IsCampaignBossRoom
            && room.EnemyCollection.Count == 6,
            "Poisonous Gloom opens with five ordinary enemies.");
        Brick dragon = room.EnemyCollection.Select(enemy => enemy.GetComponent<Brick>())
            .Single(enemy => enemy.UsesBossHealth);
        Require(dragon != null && dragon.Footprint == new Vector2Int(3, 3)
            && dragon.GetComponent<DragonBossIdleAnimator>() != null
            && dragon.GetComponent<BossEnemyTurnController>() == null
            && dragon.GetComponent<GhostPhasing>() == null
            && dragon.GetComponent<DrownedKingSplit>() == null
            && Mathf.Approximately(dragon.MaxHp, room.OrdinaryEnemyBaseHealth * progression.EnemyHealthMultiplier * progression.GetBossHealthMultiplier(4)),
            "Level-scaled Poisonous Gloom has no special abilities.");
        foreach (int line in new[] { 2, 3 })
        {
            LevelGenerator side = manager.Front.GetGenerator(DefenseFront.IdFor(line, line));
            Require(side != null && side.IsInitialized && side.IsCampaignBossRoom
                && side.EnemyCollection.Count == 6,
                "A drowned mini-boss starts in central room " + line + "-" + line + ".");
            Brick drowned = side.EnemyCollection.Select(enemy => enemy.GetComponent<Brick>())
                .Single(enemy => enemy.UsesBossHealth);
            Require(drowned != null && drowned.Footprint == new Vector2Int(2, 2)
                && drowned.GetComponent<GhostPhasing>() == null
                && drowned.GetComponent<DrownedKingSplit>() == null
                && Mathf.Approximately(drowned.MaxHp,
                    side.OrdinaryEnemyBaseHealth * progression.EnemyHealthMultiplier * progression.GetBossHealthMultiplier(line)),
                "Drowned mini-boss is 2x2 and has no special abilities.");
        }
        DrownedBogVisuals visuals = room.GetComponentInParent<DrownedBogVisuals>();
        Require(visuals != null && room.GetComponentInParent<Stage>()
            .transform.Find("Drowned Bog Visual Fog").gameObject.activeSelf,
            "Visual-only swamp fog is active in level 15.");
        Stage stage = room.GetComponentInParent<Stage>();
        Tilemap terrain = null;
        foreach (Tilemap candidate in stage.GetComponentsInChildren<Tilemap>(true))
            if (candidate.name == Stage.DrownedBogTilemapName) terrain = candidate;
        Require(terrain != null && terrain.gameObject.activeSelf
            && Mathf.Approximately(terrain.color.a, 1f)
            && stage.transform.Find("Drowned Bog Battlefield Art").gameObject.activeSelf,
            "Battlefield uses authored swamp artwork and dark-olive mud cells.");
        dragon.TakeDamage(dragon.MaxHp + 1f);
        Require(!room.HasLivingCampaignBoss,
            "Poisonous Gloom dies without splitting or summoning.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Drowned Bog runtime validation timed out.");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RuntimeKey, false);
        if (error == null) Debug.Log("Drowned Bog runtime validation passed.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    public static void RunBatch()
    {
        try
        {
            Validate();
            Debug.Log("Drowned Bog validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void Validate()
    {
        GameObject dragon = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/PoisonousGloomBoss.prefab");
        GameObject drowned = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/DrownedMiniBoss.prefab");
        Require(dragon != null && drowned != null,
            "Both editable Drowned Bog boss prefabs must exist.");
        Brick dragonBrick = dragon.GetComponent<Brick>();
        Brick drownedBrick = drowned.GetComponent<Brick>();
        Require(dragonBrick != null && dragonBrick.Footprint == new Vector2Int(3, 3)
            && Mathf.Approximately(dragonBrick.hp, 2400f),
            "Poisonous Gloom's size or base HP is wrong.");
        Require(drownedBrick != null && drownedBrick.Footprint == new Vector2Int(2, 2)
            && Mathf.Approximately(drownedBrick.hp, 600f),
            "Drowned mini-boss size or base HP is wrong.");
        Require(dragon.GetComponent<BossEnemyTurnController>() == null
            && dragon.GetComponent<GhostPhasing>() == null
            && dragon.GetComponent<DrownedKingSplit>() == null
            && drowned.GetComponent<BossEnemyTurnController>() == null
            && drowned.GetComponent<GhostPhasing>() == null
            && drowned.GetComponent<DrownedKingSplit>() == null,
            "New bosses have no special abilities.");
        Require(dragon.GetComponent<PolygonCollider2D>() != null
            && drowned.GetComponent<PolygonCollider2D>() != null,
            "Both bosses need physical octagonal colliders.");

        Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity", OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.drownedBogIllustration != null
            && hub.forestIllustration != null
            && hub.drownedBogIllustration.GetComponent<UnityEngine.UI.RawImage>().texture != null,
            "Editable menu artwork references are missing.");
        Require(menu.isLoaded, "Menu scene failed to load.");

        Scene gameplay = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        LevelProgression progression = Object.FindObjectOfType<LevelProgression>(true);
        Require(progression != null, "Gameplay progression is missing.");
        SerializedObject data = new SerializedObject(progression);
        Require(data.FindProperty("levels").arraySize == LevelProgression.MaxLevel,
            "All campaign levels must be serialized in Asset.unity.");
        Require(progression.IsBossRoom(15, 4, 4),
            "Poisonous Gloom's room is the fifth level's center fourth-line room.");
        Stage[] stages = Object.FindObjectsOfType<Stage>(true);
        Require(stages.Length > 0, "No battlefield stages found.");
        foreach (Stage stage in stages)
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            Require(visuals != null && stage.transform.Find("Drowned Bog Visual Fog") != null,
                "Every stage needs authored visual fog.");
        }
        Require(gameplay.isLoaded, "Gameplay scene failed to load.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

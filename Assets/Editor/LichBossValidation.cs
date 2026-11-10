using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// RunBatch uses an isolated project copy and restores the shared player preferences.
[InitializeOnLoad]
public static class LichBossValidation
{
    private const string Running = "ForestAnimalWar.LichValidation.Running";
    private static readonly string[] Keys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static LichBossValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        Directory.CreateDirectory("Logs");
        SessionState.SetString(Running + ".Prefs", string.Join("|", Keys.Select(key =>
            PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "-")));
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        Time.timeScale = 1f;
        Time.maximumDeltaTime = 0.05f;
        GameManager manager = GameManager.Instance;
        manager.enabled = false;
        manager.isPaused = false;
        manager.stepGame = manager.stepPlayer = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        for (int line = 2; line <= 4; line++) manager.Front.ActivateLine(line);
        yield return null;
        LevelGenerator room = manager.Front.GetGenerator(DefenseFront.IdFor(4, 4));
        LevelGenerator[] outerRooms = manager.rooms.Where(r => r.DefenseLine == 4).ToArray();
        Check(outerRooms.Length == 7 && outerRooms.All(r => r.RoomIndex == 4
                ? r.IsCampaignBossRoom && r.HasLivingCampaignBoss && Bosses(r).Count == 1
                : !r.IsCampaignBossRoom && Bosses(r).Count == 0),
            "All seven fourth-line rooms are active with one living Lich boss only in 4-4");
        Brick boss = Bosses(room)[0];
        Check(boss.Footprint == new Vector2Int(2, 2), "Lich occupies a 2x2 footprint");
        PolygonCollider2D octagon = boss.GetComponent<PolygonCollider2D>();
        Check(octagon != null && octagon.GetPath(0).Length == 8
            && Vector2.Distance(octagon.bounds.size, new Vector2(1.9f, 1.9f)) < 0.01f
            && boss.GetComponent<BoxCollider2D>() == null,
            "Lich has an editable eight-sided collider covering four cells");
        CheckNoOverlap(room, room.Rows, "Initial generation reserves all four boss cells");
        int initial = room.EnemyCollection.Count;
        Check(initial == 6, "The boss and five ordinary enemies form its first wave");
        room.InitializeRoom();
        Check(room.EnemyCollection.Count == initial, "Reinitialization does not duplicate enemies");
        room.SpawnNextWave();
        int reinforcementCount = LevelProgression.Instance.GetCampaignWaveEnemyCount(
            LevelProgression.Instance.CurrentLevel, room.DefenseLine, room.RoomIndex, 2);
        Check(Bosses(room).Count == 1
            && room.EnemyCollection.Count == initial + reinforcementCount,
            "A living boss receives density-based ordinary reinforcements");
        CheckNoOverlap(room, room.Rows, "Additional waves avoid the complete boss footprint");
        LevelGenerator cancelledRoom = manager.rooms.First(r => r.IsCampaignBossRoom && r != room);
        Brick cancelledBoss = Bosses(cancelledRoom).Single();
        cancelledBoss.TakeDamage(cancelledBoss.hp + 1f);
        yield return null;
        int cancelledCount = cancelledRoom.EnemyCollection.Count;
        cancelledRoom.SpawnNextWave();
        Check(!cancelledRoom.HasLivingCampaignBoss && manager.HasLivingCampaignBoss
                && !manager.HasLivingCampaignBossOnLine(cancelledRoom.DefenseLine)
                && cancelledRoom.RemainingWaves == 0
                && cancelledRoom.EnemyCollection.Count == cancelledCount
                && room.RemainingWaves == 1,
            "Killing one boss stops its line without stopping other boss lines");
        Brick comparisonSkeleton = room.EnemyCollection.Select(o => o.GetComponent<Brick>())
            .First(b => b != boss && b.GetComponent<ZombieResurrection>() == null);
        float bossSourceHp = Resources.Load<GameObject>("Enemies/LichBoss").GetComponent<Brick>().hp;
        Check(boss.UsesBossHealth && Mathf.Approximately(boss.MaxHp,
                boss.levelGenerator.OrdinaryEnemyBaseHealth * LevelProgression.Instance.EnemyHealthMultiplier
                    * LevelProgression.Instance.GetBossHealthMultiplier(boss.levelGenerator.DefenseLine))
            && comparisonSkeleton.MaxHp < boss.MaxHp,
            "Lich keeps independent boss HP while reinforcements use ordinary HP");
        var animator = boss.GetComponentInChildren<Animator>();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LichBossAuthoring.ArtPath + "/LichLive.anim");
        Check(animator.runtimeAnimatorController.animationClips.Contains(clip)
            && AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite")).Length == 16,
            "Lich uses the original LichLive asset with all fifteen frames and a loop closure");
        SpriteRenderer visual = boss.GetComponentInChildren<SpriteRenderer>();
        Sprite firstFrame = visual.sprite;
        bool animated = false;
        float until = Time.time + 0.3f;
        while (Time.time < until) { yield return null; animated |= visual.sprite != firstFrame; }
        Check(animated, "Live animation advances in Play Mode");
        yield return CheckHit(boss, true);
        Brick skeleton = room.EnemyCollection.Select(o => o.GetComponent<Brick>())
            .First(b => b != boss && b.GetComponent<ZombieResurrection>() == null);
        Check(skeleton.ExperienceReward > 0 && !skeleton.IsSummonedVariant,
            "Ordinary wave skeletons keep their normal experience reward");
        yield return CheckHit(skeleton, false);
        manager.Front.FocusRoom(room.RoomId, true);
        while (manager.Front.IsCameraTransitioning) yield return null;
        var darknessSettings = new SerializedObject(room.Darkness);
        darknessSettings.FindProperty("useLineLighting").boolValue = false;
        darknessSettings.FindProperty("litFraction").floatValue = 0f;
        darknessSettings.ApplyModifiedPropertiesWithoutUndo();
        room.Darkness.Refresh();
        yield return null;
        int damageLabels = EnemyCombatTextUI.Instance.ActiveDamageNumberCount;
        boss.TakeDamage(1f);
        yield return null;
        Check(boss.GetComponent<EnemyDarknessVisibility>().IsHidden && visual.forceRenderingOff
            && EnemyCombatTextUI.Instance.ActiveDamageNumberCount > damageLabels,
            "Damage pose stays concealed in darkness, while damage numbers remain visible");
        boss.GetComponent<EnemyIllumination>().Illuminate(3f);
        yield return null;
        while (boss.GetComponent<EnemyHitReaction>().IsReacting) yield return null;
        Capture(room.GetComponentInParent<Stage>());

        Clear(room, boss);
        boss.transform.position = room.transform.position + new Vector3(1.5f, -0.5f, 0f);
        Vector3 beforeStep = boss.transform.position;
        room.StepEnemy();
        while (boss.IsStepping) yield return null;
        Check(Vector3.Distance(boss.transform.position, beforeStep + Vector3.down) < 0.001f,
            "Lich moves exactly one cell in an enemy phase");
        LichSummoner summoner = boss.GetComponent<LichSummoner>();
        room.CompleteEnemyTurn(1000);
        int summoned = summoner.LastSummonCount;
        Check(summoned >= 1 && summoned <= 3 && room.EnemyCollection.Count == summoned + 1,
            "Lich summons one to three skeletons after movement");
        Check(room.EnemyCollection.Where(o => o != boss.gameObject).All(o => !o.GetComponent<Brick>().IsStepping),
            "New skeletons do not move on their birth turn");
        Brick[] createdSkeletons = room.EnemyCollection.Where(o => o != boss.gameObject)
            .Select(o => o.GetComponent<Brick>()).ToArray();
        Check(createdSkeletons.All(enemy => enemy.IsSummonedVariant && enemy.ExperienceReward == 0),
            "Every Lich-summoned skeleton is marked as summoned and awards zero experience");
        Check(createdSkeletons.All(enemy => Mathf.Approximately(enemy.MaxHp,
                room.OrdinaryEnemyBaseHealth * enemy.HealthCoefficient
                * LevelProgression.Instance.EnemyHealthMultiplier
                * LevelProgression.GetCampaignWaveHealthMultiplier(room.SpawnedWaveCount))),
            "Lich-summoned skeletons use ordinary non-boss HP");
        Check(createdSkeletons.All(enemy => {
            Color color = enemy.GetComponentInChildren<SpriteRenderer>().color;
            return color.g > color.r && color.g > color.b;
        }), "Lich-summoned skeletons have a subtle green visual tint");
        summoner.CompleteEnemyTurn(1000);
        Check(room.EnemyCollection.Count == summoned + 1, "One Lich cannot summon twice in the same turn");
        CheckNoOverlap(room, room.Rows, "Summons do not overlap the Lich or one another");

        Clear(room, boss);
        var grid = new EnemyRoomGrid(room, room.Rows - 1);
        Vector2Int anchor = grid.Anchor(boss.transform.position, boss.Footprint);
        foreach (Vector2Int cell in grid.FreeAnchors(Vector2Int.one))
        {
            int dx = Mathf.Max(anchor.x - cell.x, cell.x - anchor.x - 1, 0);
            int dy = Mathf.Max(anchor.y - cell.y, cell.y - anchor.y - 1, 0);
            if (Mathf.Max(dx, dy) == 1) AddSkeleton(room, grid.Center(cell, Vector2Int.one));
        }
        var oldEnemies = new HashSet<GameObject>(room.EnemyCollection);
        summoner.CompleteEnemyTurn(1001);
        Check(summoner.LastSummonCount >= 1 && room.EnemyCollection.Where(o => !oldEnemies.Contains(o))
            .All(o => Mathf.Max(Mathf.Abs(o.transform.position.x - boss.transform.position.x),
                Mathf.Abs(o.transform.position.y - boss.transform.position.y)) > 1.5f),
            "When the adjacent ring is full, summons use the next nearest free cells");
        grid = new EnemyRoomGrid(room, room.Rows - 1);
        foreach (Vector2Int cell in grid.FreeAnchors(Vector2Int.one)) AddSkeleton(room, grid.Center(cell, Vector2Int.one));
        summoner.CompleteEnemyTurn(1002);
        Check(summoner.LastSummonCount == 0, "A full room skips summons without overlap or a loop");

        // A real branch breach transfers the 2x2 enemy immediately and preserves its HP.
        foreach (LevelGenerator generator in manager.rooms.ToArray()) Clear(generator, generator == room ? boss : null);
        LevelGenerator destination = manager.Front.GetGenerator(DefenseFront.IdFor(3, 3));
        boss.transform.position = new Vector3(room.transform.position.x + 1.5f,
            room.DefenseBoundaryY + 0.5f, room.transform.position.z);
        float hpBeforeBreach = boss.hp;
        room.StepEnemy();
        while (boss.IsStepping) yield return null;
        room.ResolveRequestedBreach();
        manager.Front.DeliverPressure();
        CheckNoOverlap(destination, destination.PressureRows, "Transferred boss fits its whole footprint in inner rows");
        room.CompleteEnemyTurn(1003);
        Check(!room.IsLost && manager.Front.Model.GetRoom(room.RoomId).CanSelect
                && boss.gameObject.activeInHierarchy && boss.levelGenerator == destination,
            "Bottom occupied cell triggers immediate 4-4 to 3-3 breach delivery; 4-4 stays playable"
            + " (lost=" + room.IsLost + ", active=" + boss.gameObject.activeInHierarchy
            + ", room=" + (boss.levelGenerator != null ? boss.levelGenerator.RoomId.ToString() : "none")
            + ", pending=" + manager.Front.PendingTransferEnemies + ")");
        Check(Mathf.Approximately(boss.hp, hpBeforeBreach), "Pressure transfer preserves boss health");
        Check(summoner.LastSummonCount >= 1, "A transferred Lich summons in its destination, not its source room");
        CheckNoOverlap(destination, destination.Rows, "Post-transfer summons remain within the destination field");

        // Exercise an atomic mixed group at the new 15-enemy cap and with fragmented free space.
        LevelGenerator packing = MakeRoom(new Vector3(500, 500), 3);
        var incoming = new List<Brick>();
        for (int i = 0; i < 2; i++) incoming.Add(Object.Instantiate(Resources.Load<GameObject>("Enemies/LichBoss")).GetComponent<Brick>());
        for (int i = 0; i < 13; i++) incoming.Add(Object.Instantiate(room.brickPrefab).GetComponent<Brick>());
        foreach (Brick enemy in incoming) enemy.gameObject.SetActive(false);
        Check(packing.TryReceivePressureGroup(incoming), "Two bosses and 13 skeletons fit the 15-enemy cap");
        CheckNoOverlap(packing, packing.PressureRows, "Capped pressure placement has no overlaps");
        Brick overflow = Object.Instantiate(room.brickPrefab).GetComponent<Brick>();
        overflow.gameObject.SetActive(false);
        Check(!packing.TryReceiveEnemy(overflow), "A sixteenth enemy cannot enter the room");
        Object.DestroyImmediate(overflow.gameObject);
        Clear(packing);
        grid = new EnemyRoomGrid(packing, packing.PressureRows);
        int fragments = 0;
        for (int y = 0; y < packing.PressureRows; y++)
        for (int x = 0; x < packing.Columns; x++)
            if ((x + y) % 2 == 0 && fragments < LevelGenerator.BossPressureEnemyLimit - 1)
            {
                AddSkeleton(packing, grid.Center(new Vector2Int(x, y), Vector2Int.one));
                fragments++;
            }
        Brick blockedBoss = Object.Instantiate(Resources.Load<GameObject>("Enemies/LichBoss")).GetComponent<Brick>();
        blockedBoss.gameObject.SetActive(false);
        Vector3 originalPosition = blockedBoss.transform.position;
        Check(!packing.TryReceiveEnemy(blockedBoss) && !blockedBoss.gameObject.activeSelf
            && blockedBoss.levelGenerator == null && blockedBoss.transform.position == originalPosition,
            "Fragmented single cells cannot accept a 2x2 boss; failed placement is atomic");
        Check(packing.TryReceivePressureGroup(new[] { blockedBoss }, true),
            "Survival can fit a large enemy deeper in the room when entry rows are fragmented");
        CheckNoOverlap(packing, packing.SafeReceivingRows,
            "Overflow respects the large footprint without entering the breach row");
        Clear(packing);
        Object.DestroyImmediate(packing.gameObject);

        // Real GameManager ordering, not only direct summoner calls.
        Clear(destination, boss);
        boss.transform.position = destination.transform.position + new Vector3(1.5f, -0.5f, 0f);
        typeof(GameManager).GetMethod("ResolveEnemyTurn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        while (manager.IsResolvingEnemyTurn) yield return null;
        Check(summoner.LastSummonCount >= 1 && summoner.LastSummonCount <= 3,
            "GameManager's complete enemy turn invokes the summon phase");
        CheckNoOverlap(destination, destination.Rows, "GameManager waves, movement and summons share occupancy rules");
        Brick noRewardSkeleton = destination.EnemyCollection.Select(o => o.GetComponent<Brick>())
            .First(enemy => enemy != boss && enemy.IsSummonedVariant);
        int experienceBefore = manager.CurrentExperience;
        noRewardSkeleton.TakeDamage(noRewardSkeleton.hp + 1f);
        yield return null;
        Check(manager.CurrentExperience == experienceBefore,
            "Defeating a Lich-summoned skeleton grants no player experience");
    }

    private static IEnumerator CheckHit(Brick enemy, bool damagePose)
    {
        while (enemy.IsVisualTransitioning) yield return null;
        yield return null;
        EnemyHitReaction reaction = enemy.GetComponent<EnemyHitReaction>();
        SpriteRenderer renderer = reaction.VisualRoot.GetComponent<SpriteRenderer>();
        Vector3 rootPosition = enemy.transform.position;
        Vector3 visualPosition = reaction.VisualRoot.localPosition;
        enemy.TakeDamage(1f);
        bool shook = false, displayedPose = false;
        while (reaction.IsReacting)
        {
            yield return null;
            shook |= Vector3.Distance(visualPosition, reaction.VisualRoot.localPosition) > 0.001f;
            displayedPose |= renderer.sprite == reaction.DamageSprite && reaction.DamageSprite != null;
        }
        yield return null;
        yield return null;
        float visualDrift = Vector3.Distance(reaction.VisualRoot.localPosition, visualPosition);
        bool hovering = reaction.VisualRoot.GetComponent<EnemyVisualHover>() != null;
        Check(shook && enemy.transform.position == rootPosition
            && visualDrift <= (hovering ? 0.12f : 0.001f),
            enemy.name + " shakes its artwork without moving the physics root, then returns to idle motion");
        if (damagePose) Check(displayedPose && renderer.sprite != reaction.DamageSprite,
            "LichDamage appears briefly and returns to live animation");
    }

    private static void Capture(Stage room)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        var target = new RenderTexture(720, 1280, 24);
        var image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        float oldAspect = camera.aspect;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 9f / 16f;
            camera.GetComponent<CameraRenderer>().FrameRoom(room);
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/LichBattlefield.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive;
            camera.targetTexture = oldTarget;
            camera.aspect = oldAspect;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    private static List<Brick> Bosses(LevelGenerator room) => room.EnemyCollection
        .Where(o => o != null && o.GetComponent<LichSummoner>() != null).Select(o => o.GetComponent<Brick>()).ToList();

    private static Brick AddSkeleton(LevelGenerator room, Vector3 position)
    {
        GameObject enemy = Object.Instantiate(room.brickPrefab, position, Quaternion.identity, room.transform);
        Brick brick = enemy.GetComponent<Brick>();
        brick.levelGenerator = room;
        room.EnemyCollection.Add(enemy);
        return brick;
    }

    private static LevelGenerator MakeRoom(Vector3 position, int line)
    {
        var root = new GameObject("Lich packing test room");
        root.transform.position = position;
        LevelGenerator room = root.AddComponent<LevelGenerator>();
        room.ConfigureRoomIdentity(line, 1);
        room.InitializeRoom();
        room.brickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LichBossAuthoring.SkeletonPath);
        return room;
    }

    private static void Clear(LevelGenerator room, Brick keep = null)
    {
        foreach (GameObject enemy in room.EnemyCollection.ToArray())
        {
            if (enemy == null || enemy.GetComponent<Brick>() == keep) continue;
            room.EnemyCollection.Remove(enemy);
            enemy.GetComponent<Brick>().levelGenerator = null;
            Object.DestroyImmediate(enemy);
        }
    }

    private static void CheckNoOverlap(LevelGenerator room, int rows, string description)
    {
        var taken = new HashSet<Vector2Int>();
        var grid = new EnemyRoomGrid(room, rows);
        foreach (GameObject enemy in room.EnemyCollection)
        {
            Brick brick = enemy.GetComponent<Brick>();
            Vector2Int cell = grid.Anchor(enemy.transform.position, brick.Footprint);
            CheckSilent(cell.x >= 0 && cell.y >= 0 && cell.x + brick.Footprint.x <= room.Columns
                && cell.y + brick.Footprint.y <= rows, description + ": outside the grid");
            for (int y = 0; y < brick.Footprint.y; y++)
            for (int x = 0; x < brick.Footprint.x; x++)
                CheckSilent(taken.Add(cell + new Vector2Int(x, y)), description + ": overlap");
        }
        Check(true, description);
    }

    private static void CheckSilent(bool value, string description)
    {
        if (!value) throw new Exception(description);
    }

    private static void Check(bool value, string description)
    {
        CheckSilent(value, description);
        report.Add("PASS " + description);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Lich validation timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        string[] saved = SessionState.GetString(Running + ".Prefs", "").Split('|');
        for (int i = 0; i < Keys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetInt(Keys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string result = error == null ? "PASS: " + report.Count + " Lich integration checks." : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/LichBossValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

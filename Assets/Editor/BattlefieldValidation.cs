using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Runs against the actual generated rooms in Play Mode, then restores the run.
[InitializeOnLoad]
public static class BattlefieldValidation
{
    private const string BatchKey = "ForestAnimalWar.BattlefieldValidation.Running";
    private const string RequestedLevelKey = "ForestAnimalWar.RequestedLevel";
    private const string SavedLevelKey = "ForestAnimalWar.BattlefieldValidation.SavedRequest";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> report = new List<string>();
    private static Stage[] rooms;
    private static Dictionary<GameObject, bool> roots;
    private static Dictionary<LevelGenerator, bool> generators;
    private static Dictionary<GameObject, bool> players;
    private static Dictionary<Player, bool> playerComponents;
    private static Dictionary<BallSpawner, bool> aimObjects;
    private static CameraBehaviour roomCamera;
    private static GameManager manager;
    private static Stage originalRoom;
    private static bool originalPaused;
    private static bool originalStepPlayer;
    private static int roomIndex;
    private static int frames;
    private static int lastFrame;
    private static int poolSize;
    private static float roomStartedAt;
    private static bool running;
    private static double deadline;
    private static int startupFrame;

    static BattlefieldValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BatchKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            startupFrame = Time.frameCount;
            EditorApplication.update += StartBatchWhenReady;
        };
    }

    // Run in an isolated project with -batchmode -executeMethod BattlefieldValidation.RunBatch.
    // Do not pass -quit: the asynchronous checks exit Unity themselves when complete.
    public static void RunBatch()
    {
        RunBatch(false);
    }

    public static void RunGeneratedRoomsBatch()
    {
        RunBatch(true);
    }

    private static void RunBatch(bool generateOuterRooms)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        if (SessionState.GetBool(BatchKey, false)) throw new InvalidOperationException("Battlefield validation is already running.");
        SessionState.SetString(SavedLevelKey, PlayerPrefs.HasKey(RequestedLevelKey)
            ? PlayerPrefs.GetInt(RequestedLevelKey).ToString() : "-");
        SessionState.SetBool(BatchKey, true);
        try
        {
            LevelProgression.PrepareLevel(1);
            EditorSceneManager.OpenScene("Assets/Asset.unity");
            if (generateOuterRooms)
            {
                // Exercise the older four-room scene format without saving the test setup.
                foreach (Stage room in Object.FindObjectsOfType<Stage>(true))
                    if (room.DefenseLine >= 3) Object.DestroyImmediate(room.gameObject);
            }
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void StartBatchWhenReady()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Finish(new InvalidOperationException("Timed out waiting for the generated battlefield."));
            return;
        }
        if (Time.frameCount < startupFrame + 2 || GameManager.Instance == null
            || GameManager.Instance.Front == null || !GameManager.Instance.Front.IsReady) return;
        EditorApplication.update -= StartBatchWhenReady;
        try { Run(); }
        catch (Exception error) { Finish(error); }
    }

    [MenuItem("Forest Animal War/Gameplay/Validate portrait battlefields and all room aims")]
    public static void Run()
    {
        if (!Application.isPlaying || running)
        {
            Debug.LogWarning("Start the gameplay scene in Play Mode before running battlefield validation.");
            return;
        }
        manager = GameManager.Instance;
        roomCamera = Object.FindObjectOfType<CameraBehaviour>();
        if (manager == null || roomCamera == null || Player.HasActiveProjectiles)
        {
            if (SessionState.GetBool(BatchKey, false))
                throw new InvalidOperationException("Gameplay camera or manager is missing, or projectiles are already active.");
            Debug.LogWarning("Wait for all projectiles to disappear before validating.");
            return;
        }

        report.Clear();
        ValidateProjectilePrefabs();
        roots = null;
        generators = null;
        players = null;
        playerComponents = null;
        aimObjects = null;
        originalRoom = null;
        rooms = Object.FindObjectsOfType<Stage>(true)
            .OrderBy(r => r.DefenseLine).ThenBy(r => r.name).ToArray();
        Check(rooms.Length == 16, "All 16 rooms from four defense lines are present");
        for (int line = 1; line <= 4; line++)
            Check(rooms.Count(r => r.DefenseLine == line) == line * 2 - 1,
                "Line " + line + " has " + (line * 2 - 1) + " rooms");
        originalRoom = roomCamera.CurrentRoom != null ? roomCamera.CurrentRoom.GetComponent<Stage>() : rooms[0];
        roots = new Dictionary<GameObject, bool>();
        generators = Object.FindObjectsOfType<LevelGenerator>(true).ToDictionary(g => g, g => g.enabled);
        players = rooms.ToDictionary(r => r.PlayerObject, r => r.PlayerObject.activeSelf);
        playerComponents = rooms.Select(r => r.PlayerObject.GetComponent<Player>()).ToDictionary(p => p, p => p.enabled);
        aimObjects = rooms.Select(r => r.PlayerObject.GetComponentInChildren<BallSpawner>(true))
            .ToDictionary(a => a, a => a.gameObject.activeSelf);
        foreach (Stage room in rooms)
        {
            if (room.transform.parent != null)
            {
                GameObject root = room.transform.parent.gameObject;
                roots[root] = root.activeSelf;
            }
            roots[room.gameObject] = room.gameObject.activeSelf;
        }
        originalPaused = manager.isPaused;
        originalStepPlayer = manager.stepPlayer;
        manager.isPaused = true;
        foreach (var item in generators) item.Key.enabled = false;
        foreach (var root in roots.Keys) root.SetActive(true);
        foreach (var item in playerComponents) item.Key.enabled = false;
        roomIndex = -1;
        deadline = EditorApplication.timeSinceStartup + 120;
        running = true;
        EditorApplication.update += Tick;
        NextRoom();
    }

    private static void NextRoom()
    {
        roomIndex++;
        if (roomIndex >= rooms.Length)
        {
            Finish(null);
            return;
        }

        Stage room = rooms[roomIndex];
        room.PlayerObject.SetActive(false);
        Player player = room.PlayerObject.GetComponent<Player>();
        player.enabled = true;
        BallSpawner aim = room.PlayerObject.GetComponentInChildren<BallSpawner>(true);
        aim.gameObject.SetActive(false); // Reproduce the old disabled-template bug.
        roomCamera.ForceFocusRoom(room);
        player.enabled = false;
        Physics2D.SyncTransforms();
        frames = 0;
        lastFrame = -1;
        poolSize = -1;
        roomStartedAt = Time.realtimeSinceStartup;
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended during validation.");
            if (EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Battlefield validation timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Stage room = rooms[roomIndex];
            Player player = room.PlayerObject.GetComponent<Player>();
            BallSpawner aim = player.GetComponentInChildren<BallSpawner>(true);
            Dots dots = Dots.instance;

            if (frames >= 6 && Time.realtimeSinceStartup - roomStartedAt >= 0.2f)
            {
                Check(aim.isActiveAndEnabled, room.name + ": disabled aim is reactivated on room focus");
                RoomDarkness darkness = room.GetComponent<RoomDarkness>();
                int minimumDots = darkness != null && darkness.HasDarkness ? 2 : 6;
                Check(dots != null && dots.VisibleDotCount >= minimumDots, room.name + ": dotted preview is visible");
                Check(dots.transform.childCount == poolSize, room.name + ": dot pool stays stable while aiming");
                var sprites = dots.GetComponentsInChildren<SpriteRenderer>();
                Check(sprites.Count(s => s.enabled && s.color.a > 0.01f) >= minimumDots, room.name + ": visible renderers have nonzero opacity");
                Check(darkness == null || sprites.Where(s => s.enabled).All(s => darkness.IsVisible(s.transform.position)),
                    room.name + ": preview stays inside the illuminated region");
                Check(sprites.Where(s => s.enabled).All(s => Mathf.Abs(s.transform.position.z - player.transform.position.z) < 0.001f),
                    room.name + ": preview uses the room's depth");
                Check(sprites.Where(s => s.enabled).All(s => IsVisibleDepth(Camera.main, s.transform.position)),
                    room.name + ": aim dots are inside the camera depth range");
                ValidateGeometry(room, player);
                ValidateFraming(room);
                if (SessionState.GetBool(BatchKey, false)
                    && room.GetComponentInChildren<LevelGenerator>(true).RoomIndex == room.DefenseLine)
                    CaptureRoom(room);
                ValidateLaunch(player);
                aim.HideAim(true);
                NextRoom();
                return;
            }

            Vector2 target = (Vector2)player.transform.position
                + new Vector2(frames % 2 == 0 ? 1f : -1f, room.BattlefieldSize.y - 1f);
            aim.ShowAim(target);
            // Hiding another room must not cancel this request.
            rooms[(roomIndex + 1) % rooms.Length].PlayerObject.GetComponentInChildren<BallSpawner>(true).HideAim(true);
            if (frames == 2) poolSize = dots.transform.childCount;
            frames++;
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static void ValidateGeometry(Stage room, Player player)
    {
        int columns = 9 - room.DefenseLine;
        int rows = 11 - room.DefenseLine;
        LevelGenerator generator = room.GetComponentInChildren<LevelGenerator>(true);
        Check(room.BattlefieldSize == new Vector2Int(columns, rows), room.name + ": line uses " + columns + "x" + rows + " cells");
        Check(generator != null && generator.DefenseLine == room.DefenseLine,
            room.name + ": generator identity agrees with the room's defense line");
        Check(generator.Columns == columns && generator.Rows == rows,
            room.name + ": generator uses every column and row of this line");
        Check(generator.SpawnRows == 3, room.name + ": waves enter through the upper three rows");
        Check(Mathf.Abs(generator.transform.localPosition.x + (columns - 1) * 0.5f) < 0.001f
            && Mathf.Abs(generator.transform.localPosition.y - (rows - 1) * 0.5f) < 0.001f,
            room.name + ": spawn origin centers odd and even grid dimensions");
        Check(Quaternion.Angle(room.transform.rotation, Quaternion.identity) < 0.001f,
            room.name + ": room geometry is upright");
        Check(Mathf.Abs(player.transform.localPosition.x) < 0.001f
            && Mathf.Abs(player.transform.localPosition.y - (-rows * 0.5f + 0.25f)) < 0.001f,
            room.name + ": launch point is centered inside the bottom boundary");
        EdgeCollider2D wall = room.GetComponentInChildren<EdgeCollider2D>();
        Check(wall != null && Mathf.Abs(wall.bounds.size.x - columns) < 0.01f
            && Mathf.Abs(wall.bounds.max.y - (room.transform.position.y + rows * 0.5f)) < 0.01f
            && wall.bounds.min.y <= room.FieldBottomY - Stage.ReturnZoneDepth - Stage.WallExitExtension + 0.01f,
            room.name + ": side walls continue through the full projectile return zone");
        DestroyBallCollider[] returnZones = room.GetComponentsInChildren<DestroyBallCollider>(true);
        Check(returnZones.Length == 1, room.name + ": has exactly one projectile return zone");
        BoxCollider2D exit = returnZones[0].GetComponent<BoxCollider2D>();
        Check(exit != null && exit.isTrigger
            && Mathf.Abs(exit.size.x * exit.transform.lossyScale.x - (columns + 2)) < 0.01f
            && Mathf.Abs(exit.size.y * exit.transform.lossyScale.y - Stage.ReturnZoneDepth) < 0.01f
            && Mathf.Abs(exit.bounds.max.y - room.FieldBottomY) < 0.01f,
            room.name + ": return trigger covers the bottom exit without intruding into the field");
        Tilemap tilemap = room.GetComponentInChildren<Tilemap>();
        Check(tilemap != null && tilemap.cellBounds.size.x == columns
            && tilemap.cellBounds.size.y == rows,
            room.name + ": tilemap matches the playable field without an outer cell layer");
        Bounds viewBounds = room.GetViewBounds();
        Check(Vector2.Distance(viewBounds.center, room.transform.position) < 0.01f
            && Mathf.Abs(viewBounds.size.x - columns) < 0.01f
            && Mathf.Abs(viewBounds.size.y - rows) < 0.01f,
            room.name + ": view bounds fit the centered playable field (bounds=" + viewBounds
                + ", room=" + room.transform.position + ", tilemap=" + tilemap.transform.position + ")");
        Check(Mathf.Abs(generator.DefenseBoundaryY - room.DefenseBoundaryY) < 0.001f,
            room.name + ": room and generator use the same breach boundary");
        Check(Mathf.Abs(room.DefenseBoundaryY
            - (room.transform.position.y - rows * 0.5f + Stage.BreachBoundaryInset)) < 0.001f,
            room.name + ": breach boundary is raised exactly one cell");
        Check(generator.transform.position.y - (rows - 2) > generator.DefenseBoundaryY
            && generator.transform.position.y - (rows - 1) <= generator.DefenseBoundaryY,
            room.name + ": entering the final row now breaches the room");
        MethodInfo contains = typeof(Player).GetMethod("IsInsideTouchArea", PrivateInstance);
        MethodInfo overUI = typeof(Player).GetMethod("IsOverInterface", PrivateInstance);
        Check(contains != null && overUI != null, room.name + ": actual player input methods are available");
        for (int column = 0; column < columns; column++)
        for (int row = 0; row < rows; row++)
        {
            Vector3 target = generator.transform.position + new Vector3(column, -row, 0f);
            Check((bool)contains.Invoke(player, new object[] { (Vector2)target }),
                room.name + ": cell " + column + "," + row + " accepts aim input");
            Vector3 screen = Camera.main.WorldToScreenPoint(target);
            Vector2 restored = Camera.main.ScreenToWorldPoint(screen);
            Check(Vector2.Distance(restored, target) < 0.001f,
                room.name + ": upright camera maps cell " + column + "," + row + " correctly");
        }
        Vector2 center = (Vector2)room.transform.position;
        foreach (Vector2 outside in new[] { new Vector2(columns * 0.5f + 0.1f, 0f),
            new Vector2(-columns * 0.5f - 0.1f, 0f), new Vector2(0f, rows * 0.5f + 0.1f),
            new Vector2(0f, -rows * 0.5f - 0.1f) })
            Check(!(bool)contains.Invoke(player, new object[] { center + outside }),
                room.name + ": input outside field edge " + outside + " is rejected");
        Check((bool)contains.Invoke(player, new object[] { (Vector2)player.transform.position }),
            room.name + ": launch point is inside its own touch area");
        Vector3 emptyScreen = Camera.main.WorldToScreenPoint(center);
        Check(!(bool)overUI.Invoke(player, new object[] { (Vector2)emptyScreen }), room.name + ": room center is not blocked by HUD");
        Check(generator.EnemyCollection.All(e => e == null || e.transform.localPosition.x >= -0.01f
            && e.transform.localPosition.x <= columns - 1 + 0.01f),
            room.name + ": existing enemies fit inside this line's columns");
        Check(generator.EnemyCollection.All(e => e == null || e.transform.position.y > generator.DefenseBoundaryY),
            room.name + ": existing enemies have not crossed the breach boundary");
        ValidateEntryCells(generator);
    }

    private static void ValidateEntryCells(LevelGenerator generator)
    {
        MethodInfo findCells = typeof(LevelGenerator).GetMethod("FindFreeEntryCells", PrivateInstance);
        MethodInfo findPressureCells = typeof(LevelGenerator).GetMethod("FindFreePressureCells", PrivateInstance);
        FieldInfo cellsField = typeof(LevelGenerator).GetField("freeCells", PrivateInstance);
        Check(findCells != null && findPressureCells != null && cellsField != null,
            generator.name + ": normal and pressure entry allocators are available");
        findCells.Invoke(generator, null);
        List<Vector3> freeCells = (List<Vector3>)cellsField.GetValue(generator);
        List<Vector3> expected = ExpectedFreeCells(generator, generator.SpawnRows);
        Check(freeCells.Count == expected.Count && freeCells.Distinct().Count() == expected.Count
            && expected.All(point => freeCells.Contains(point)),
            generator.name + ": normal waves use all free cells exactly once in the upper three rows");

        if (freeCells.Count > 0)
        {
            Vector3 occupiedCell = freeCells[0];
            GameObject marker = new GameObject("Validation occupied entry cell");
            try
            {
                marker.transform.position = occupiedCell;
                generator.EnemyCollection.Add(marker);
                findCells.Invoke(generator, null);
                Check(freeCells.Count == expected.Count - 1 && !freeCells.Contains(occupiedCell),
                    generator.name + ": occupied entry cell cannot receive another enemy");
            }
            finally
            {
                generator.EnemyCollection.Remove(marker);
                Object.DestroyImmediate(marker);
            }
        }

        int expectedPressureRows = generator.DefenseLine <= 3 ? 8 - generator.DefenseLine : generator.SpawnRows;
        Check(generator.PressureRows == expectedPressureRows,
            generator.name + ": line " + generator.DefenseLine + " has the correct pressure depth");
        findPressureCells.Invoke(generator, null);
        List<Vector3> expectedPressure = ExpectedFreeCells(generator, expectedPressureRows);
        Check(freeCells.Count == expectedPressure.Count && freeCells.Distinct().Count() == expectedPressure.Count
            && expectedPressure.All(point => freeCells.Contains(point)),
            generator.name + ": pressure can use every free cell in its expanded upper zone");
    }

    private static List<Vector3> ExpectedFreeCells(LevelGenerator generator, int rows)
    {
        List<Vector3> expected = new List<Vector3>();
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < generator.Columns; column++)
        {
            Vector3 point = generator.transform.position + new Vector3(column, -row, 0f);
            if (!generator.EnemyCollection.Any(e => e != null && (e.transform.position - point).sqrMagnitude < 0.25f))
                expected.Add(point);
        }
        return expected;
    }

    private static void ValidateFraming(Stage room)
    {
        Camera camera = Camera.main;
        CameraRenderer framing = camera.GetComponent<CameraRenderer>();
        Check(camera.orthographic && framing != null, room.name + ": orthographic room framing exists");
        Check(Quaternion.Angle(camera.transform.rotation, Quaternion.identity) < 0.001f,
            room.name + ": camera stays upright when entering any branch");
        Check(IsVisibleDepth(camera, room.PlayerObject.transform.position), room.name + ": player is inside the camera depth range");
        Check(IsVisibleDepth(camera, room.GetComponentInChildren<LevelGenerator>(true).transform.position),
            room.name + ": enemy grid is inside the camera depth range");
        float originalAspect = camera.aspect;
        try
        {
            foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f, 9f / 20f })
            {
                camera.aspect = aspect;
                framing.FrameRoom(room);
                Check(Quaternion.Angle(camera.transform.rotation, Quaternion.identity) < 0.001f,
                    room.name + ": portrait framing preserves upright camera at aspect " + aspect);
                Bounds bounds = room.GetViewBounds();
                BattlefieldForestStyle forest = room.GetComponent<BattlefieldForestStyle>();
                if (forest != null) bounds = forest.GetFramingBounds(bounds);
                Vector2 min = Vector2.one * float.PositiveInfinity;
                Vector2 max = Vector2.one * float.NegativeInfinity;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                {
                    Vector3 corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
                    Vector2 viewport = camera.WorldToViewportPoint(corner);
                    min = Vector2.Min(min, viewport);
                    max = Vector2.Max(max, viewport);
                }
                Check(min.x >= 0.01f && min.x <= 0.065f && max.x >= 0.935f && max.x <= 0.99f,
                    room.name + ": exterior forest frame fits portrait aspect " + aspect);
                Check(min.y >= 0.149f && max.y <= 0.851f,
                    room.name + ": field and spectators fit between HUD areas at portrait aspect " + aspect);
            }
        }
        finally
        {
            camera.aspect = originalAspect;
            framing.FrameRoom(room);
        }
    }

    private static bool IsVisibleDepth(Camera camera, Vector3 point)
    {
        float depth = camera.WorldToViewportPoint(point).z;
        return depth > camera.nearClipPlane && depth < camera.farClipPlane;
    }

    private static void CaptureRoom(Stage room)
    {
        Camera camera = Camera.main;
        RenderTexture target = new RenderTexture(1080, 1920, 24);
        Texture2D image = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        float oldAspect = camera.aspect;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 9f / 16f;
            camera.GetComponent<CameraRenderer>().FrameRoom(room);
            // Capture the actual battlefield. Overlay HUDs require a painted Game window.
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/Battlefield-Line" + room.DefenseLine + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive;
            camera.targetTexture = oldTarget;
            camera.aspect = oldAspect;
            camera.GetComponent<CameraRenderer>().FrameRoom(room);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    private static void ValidateLaunch(Player player)
    {
        GameObject prefab = new GameObject("Validation projectile", typeof(Rigidbody2D));
        GameObject shot = null;
        try
        {
            prefab.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Vector2 direction = new Vector2(1f, 2f).normalized;
            BallSpawner aim = player.GetComponentInChildren<BallSpawner>(true);
            aim.ShowAim((Vector2)player.transform.position + direction * 3f);
            SpriteRenderer[] visibleDots = Dots.instance.GetComponentsInChildren<SpriteRenderer>()
                .Where(renderer => renderer.enabled).ToArray();
            Check(visibleDots.Length >= 2
                && Vector3.Distance(visibleDots[0].transform.position, player.transform.position) < 0.001f
                && Vector2.Dot(((Vector2)(visibleDots[1].transform.position - visibleDots[0].transform.position)).normalized, direction) > 0.999f,
                player.name + ": preview starts at the muzzle and points toward the same target as the shot");
            typeof(Player).GetMethod("LaunchProjectile", PrivateInstance).Invoke(player, new object[] { direction, prefab, -1 });
            shot = GameObject.Find("Validation projectile(Clone)");
            Check(shot != null && Vector3.Distance(shot.transform.position, player.transform.position) < 0.001f,
                player.name + ": projectile starts at this room's launch point and depth");
            Vector2 velocity = shot.GetComponent<Rigidbody2D>().velocity;
            float speed = (float)typeof(Player).GetField("projectileSpeed", PrivateInstance).GetValue(player);
            Check(Vector2.Distance(velocity, direction * speed) < 0.001f, player.name + ": shot direction and full speed match the aim at this room depth");
            Rigidbody2D body = shot.GetComponent<Rigidbody2D>();
            ProjectilePhysics2D physics = shot.GetComponent<ProjectilePhysics2D>();
            Check(physics != null && physics.IsFlying && Mathf.Approximately(physics.TargetSpeed, speed),
                player.name + ": every launched projectile receives stable-flight physics");
            Check(body.gravityScale == 0f && body.drag == 0f && body.angularDrag == 0f
                && body.interpolation == RigidbodyInterpolation2D.Interpolate
                && body.collisionDetectionMode == CollisionDetectionMode2D.Continuous
                && (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                player.name + ": projectile body is configured for smooth continuous movement");
            body.velocity = direction * (speed * 0.1f);
            typeof(ProjectilePhysics2D).GetMethod("FixedUpdate", PrivateInstance).Invoke(physics, null);
            Check(Mathf.Abs(body.velocity.magnitude - speed) < 0.001f,
                player.name + ": lost speed is restored without changing the flight direction");
            Vector2 scriptedCornerExit = new Vector2(0.98f, -0.21f).normalized;
            physics.Redirect(scriptedCornerExit);
            body.velocity = Vector2.up * speed;
            typeof(ProjectilePhysics2D).GetField("restoreScriptedReflectionNextStep", PrivateInstance)
                .SetValue(physics, true);
            typeof(ProjectilePhysics2D).GetMethod("FixedUpdate", PrivateInstance).Invoke(physics, null);
            Check(Vector2.Dot(body.velocity.normalized, scriptedCornerExit) > 0.999f,
                player.name + ": a boss corner exit survives the physics solver's upward response");
            Vector2 reflection = ProjectilePhysics2D.ReflectDirection(new Vector2(1f, -1f), Vector2.up);
            Check(Vector2.Dot(reflection, new Vector2(1f, 1f).normalized) > 0.999f,
                player.name + ": ricochet follows the collision normal");
            Vector2 verticalCornerNormal = ProjectilePhysics2D.StabilizeEnemyReflectionNormal(
                new Vector2(0.4f, 0.9f), new Vector2(-0.65f, -0.76f));
            Vector2 horizontalCornerNormal = ProjectilePhysics2D.StabilizeEnemyReflectionNormal(
                new Vector2(0.9f, 0.4f), new Vector2(-0.65f, -0.76f));
            Vector2 tiedCornerNormal = ProjectilePhysics2D.StabilizeEnemyReflectionNormal(
                Vector2.one, new Vector2(-0.65f, -0.76f));
            Check(verticalCornerNormal == Vector2.down
                && horizontalCornerNormal == Vector2.left
                && tiedCornerNormal == Vector2.down,
                player.name + ": enemy corner ricochets choose a deterministic grid face");
            Vector2 angledMirageIncoming = new Vector2(0.25f, 1f).normalized;
            Check(ProjectilePhysics2D.ReturnToSenderDirection(Vector2.up) == Vector2.down
                && Vector2.Dot(ProjectilePhysics2D.ReturnToSenderDirection(angledMirageIncoming),
                    -angledMirageIncoming) > 0.999f,
                player.name + ": Mirage returns straight and angled shots along their incoming path");
            Vector2 shallowAim = player.ConstrainLaunchDirection(new Vector2(1f, 0.05f));
            float shallowAngle = Mathf.Atan2(shallowAim.y, Mathf.Abs(shallowAim.x)) * Mathf.Rad2Deg;
            Check(Mathf.Abs(shallowAngle - player.MinimumLaunchAngle) < 0.01f,
                player.name + ": a shallow pointer drag is raised to the configured launch-angle limit");
            Vector2 stableRicochet = ProjectilePhysics2D.KeepVerticalMotion(Vector2.right, 5f, -1f);
            float ricochetAngle = Mathf.Atan2(Mathf.Abs(stableRicochet.y), Mathf.Abs(stableRicochet.x)) * Mathf.Rad2Deg;
            Check(Mathf.Abs(ricochetAngle - 5f) < 0.01f && stableRicochet.y < 0f,
                player.name + ": horizontal wall-to-wall loops receive a small vertical correction");
            Check(IsVisibleDepth(Camera.main, shot.transform.position), player.name + ": projectile is inside the camera depth range");
        }
        finally
        {
            if (shot != null) Object.DestroyImmediate(shot);
            Object.DestroyImmediate(prefab);
        }
    }

    private static void ValidateProjectilePrefabs()
    {
        foreach (string path in new[] {
            "Assets/AnimalsBullet/BaseBullet.prefab",
            "Assets/AnimalsBullet/Hedgehog.prefab",
            "Assets/AnimalsBullet/Rabbit.prefab" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string name = Path.GetFileNameWithoutExtension(path);
            Check(prefab != null, name + ": projectile prefab can be loaded");
            Check(prefab.GetComponent<Animal>() != null && prefab.GetComponent<Rigidbody2D>() != null
                && prefab.GetComponent<Collider2D>() != null,
                name + ": projectile prefab has its gameplay and collision components");
            Check(prefab.GetComponent<ProjectilePhysics2D>() != null,
                name + ": projectile prefab exposes editable stable-flight settings");
        }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.Add("PASS " + label);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        EditorApplication.update -= StartBatchWhenReady;
        running = false;
        try
        {
            if (aimObjects != null) foreach (var item in aimObjects) if (item.Key != null) item.Key.HideAim(true);
            if (roots != null) foreach (var item in roots) if (item.Key != null) item.Key.SetActive(item.Value);
            if (roomCamera != null && originalRoom != null) roomCamera.ForceFocusRoom(originalRoom);
            if (players != null) foreach (var item in players) if (item.Key != null) item.Key.SetActive(item.Value);
            if (playerComponents != null) foreach (var item in playerComponents) if (item.Key != null) item.Key.enabled = item.Value;
            if (aimObjects != null) foreach (var item in aimObjects) if (item.Key != null) item.Key.gameObject.SetActive(item.Value);
            if (generators != null) foreach (var item in generators) if (item.Key != null) item.Key.enabled = item.Value;
            if (manager != null && players != null)
            {
                manager.isPaused = originalPaused;
                manager.stepPlayer = originalStepPlayer;
            }
        }
        catch (Exception restoreError)
        {
            if (error == null) error = restoreError;
            else report.Add("Restore error: " + restoreError);
        }
        bool batch = SessionState.GetBool(BatchKey, false);
        if (batch)
        {
            SessionState.SetBool(BatchKey, false);
            string saved = SessionState.GetString(SavedLevelKey, "-");
            if (saved == "-") PlayerPrefs.DeleteKey(RequestedLevelKey);
            else PlayerPrefs.SetInt(RequestedLevelKey, int.Parse(saved));
            PlayerPrefs.Save();
            SessionState.EraseString(SavedLevelKey);
        }
        string summary = error == null ? "PASS: " + report.Count + " checks across " + (rooms != null ? rooms.Length : 0) + " rooms."
            : "FAIL: " + error;
        report.Add(summary);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/BattlefieldValidation.txt", report);
        if (error == null) Debug.Log(summary);
        else Debug.LogError(summary);
        if (batch) EditorApplication.Exit(error == null ? 0 : 1);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Checks actual scene rendering and gameplay in an isolated batch-mode project.
[InitializeOnLoad]
public static class RoomDarknessValidation
{
    private const string RunningKey = "ForestAnimalWar.DarknessValidation.Running";
    private const string SavedRequestKey = "ForestAnimalWar.DarknessValidation.SavedRequest";
    private static readonly string[] PrefsKeys = {
        "ForestAnimalWar.Meta.Gold", "ForestAnimalWar.Meta.Acorns", "ForestAnimalWar.Meta.Victories",
        "ForestAnimalWar.CurrentLevel", "ForestAnimalWar.HighestUnlockedLevel",
        "ForestAnimalWar.MenuSelectedLevel", "ForestAnimalWar.RequestedLevel"
    };
    private static readonly List<string> report = new List<string>();
    private static IEnumerator routine;
    private static int lastFrame = -1;
    private static double deadline;

    static RoomDarknessValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
            routine = Checks();
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        SessionState.SetString(SavedRequestKey, string.Join("|", PrefsKeys.Select(k =>
            PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "-")));
        SessionState.SetBool(RunningKey, true);
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        Stage[] rooms = Object.FindObjectsOfType<Stage>(true);
        if (rooms.Length != 16 || rooms.Any(r => r.GetComponent<RoomDarkness>() == null))
        {
            Finish(new Exception("All 16 authored rooms must have editable darkness settings."));
            return;
        }
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Darkness validation timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;
        GameManager manager = GameManager.Instance;
        manager.isPaused = true;
        CameraBehaviour camera = Object.FindObjectOfType<CameraBehaviour>();
        Stage[] rooms = Object.FindObjectsOfType<Stage>(true).OrderBy(r => r.DefenseLine).ThenBy(r => r.name).ToArray();
        foreach (Stage room in rooms)
        {
            if (room.transform.parent != null) room.transform.parent.gameObject.SetActive(true);
            room.gameObject.SetActive(true);
            room.PlayerObject.GetComponent<Player>().enabled = false;
        }
        EnemyCombatTextUI ui = EnemyCombatTextUI.Ensure();
        foreach (Stage room in rooms)
        {
            RoomDarkness darkness = room.GetComponent<RoomDarkness>();
            camera.ForceFocusRoom(room);
            darkness.Refresh();
            yield return null;
            Check(darkness.IsVisible(room.PlayerObject.transform.position), room.name + ": launch point is lit");
            Check(darkness.HasDarkness == (room.DefenseLine > 1), room.name + ": darkness follows defense line");
            if (room.DefenseLine > 1)
            {
                Check(Mathf.Approximately(darkness.EnvironmentOpacity,
                        RoomDarknessTuningAuthoring.OpacityForLine(room.DefenseLine)),
                    room.name + ": uses the stronger authored darkness for its line");
                Check(darkness.Overlay != null && darkness.Overlay.isActiveAndEnabled,
                    room.name + ": overlay is active after switching rooms");
                Check(darkness.Overlay.rectTransform.sizeDelta == (Vector2)room.BattlefieldSize,
                    room.name + ": overlay fits the actual board");
                Check(Camera.main.WorldToViewportPoint(darkness.Overlay.transform.position).z > Camera.main.nearClipPlane,
                    room.name + ": overlay uses battlefield depth, not the legacy room root depth");
                Check(!darkness.Overlay.raycastTarget && darkness.Overlay.GetComponent<GraphicRaycaster>() == null,
                    room.name + ": fog does not intercept aiming input");
                Check(darkness.Overlay.color.a > 0f && darkness.Overlay.color.a < 1f,
                    room.name + ": scenery and projectiles receive translucent dimming");
                Check(darkness.Overlay.GetComponent<Canvas>().sortingOrder < 100,
                    room.name + ": darkness is below HUD and modal canvases");
                BattlefieldForestStyle frame = room.GetComponent<BattlefieldForestStyle>();
                Check(frame != null && frame.FrameRenderer != null
                    && frame.FrameRenderer.sortingOrder > RoomDarkness.WorldOverlayOrder
                    && frame.BoardEdgeRenderers.All(edge => edge != null
                        && edge.sortingOrder > RoomDarkness.WorldOverlayOrder)
                    && frame.VineRenderer != null
                    && frame.VineRenderer.sortingOrder > RoomDarkness.WorldOverlayOrder,
                    room.name + ": frame and border remain in front of the darkness");
            }
            // Drive the real preview. A firefly is the intentional exception to
            // fog clipping because its aiming guide is allowed to enter darkness.
            BallSpawner aim = room.PlayerObject.GetComponentInChildren<BallSpawner>(true);
            Vector2 aimTarget = (Vector2)room.transform.position + Vector2.up * room.BattlefieldSize.y;
            aim.ShowAim(aimTarget);
            yield return null;
            SpriteRenderer[] dots = Dots.instance.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled).ToArray();
            Check(dots.Length >= 2, room.name + ": aim remains visible even in a short lit zone");
            Check(dots.All(r => r.transform.lossyScale.x >= r.transform.lossyScale.y * 4f
                    && r.color.r < 0.15f && r.color.g < 0.15f && r.color.b < 0.15f),
                room.name + ": trajectory uses thin dark dashes");
            if (darkness.HasDarkness && room.PlayerObject.GetComponent<Player>().IsAimingWithFirefly)
                Check(aim.LastAimEnteredDarkness && dots.Any(r => !darkness.IsVisible(r.transform.position)),
                    room.name + ": firefly aiming remains visible inside darkness");
            else
                Check(dots.All(r => darkness.IsVisible(r.transform.position)),
                    room.name + ": non-firefly preview remains clipped by darkness");
            Check(aim.LastAimHadImpact && Dots.instance.ImpactMarkerVisible
                    && Dots.instance.ImpactMarkerSideCount == 6
                    && Vector2.Distance(Dots.instance.ImpactMarkerPosition, aim.LastAimHitPoint) < 0.001f,
                room.name + ": an orange hexagon marks the first wall or enemy impact");
            Quaternion markerRotation = Dots.instance.GetComponentInChildren<LineRenderer>().transform.rotation;
            bool markerRotated = false;
            float rotationDeadline = Time.realtimeSinceStartup + 0.1f;
            while (Time.realtimeSinceStartup < rotationDeadline)
            {
                aim.ShowAim(aimTarget);
                yield return null;
                markerRotated |= Quaternion.Angle(markerRotation,
                    Dots.instance.GetComponentInChildren<LineRenderer>().transform.rotation) > 0.1f;
            }
            Check(markerRotated,
                room.name + ": impact hexagon rotates while aiming");
            aim.HideAim(true);
            if (room.GetComponentInChildren<LevelGenerator>(true).RoomIndex != 1) continue;
            LevelGenerator generator = room.GetComponentInChildren<LevelGenerator>(true);
            generator.InitializeRoom();
            Brick sample = generator.EnemyCollection[0].GetComponent<Brick>();
            Vector3 savedPosition = sample.transform.position;
            float visibleTestY = room.PlayerObject.transform.position.y + 2f;
            if (darkness.HasDarkness)
                visibleTestY = Mathf.Min(visibleTestY, darkness.LightBoundaryY - 0.1f);
            sample.transform.position = new Vector3(room.PlayerObject.transform.position.x,
                visibleTestY, savedPosition.z);
            Physics2D.SyncTransforms();
            aim.ShowAim((Vector2)room.PlayerObject.transform.position + Vector2.up * room.BattlefieldSize.y);
            yield return null;
            Collider2D sampleCollider = sample.GetComponent<Collider2D>();
            Check(aim.LastAimHitEnemy == sample && aim.LastAimHitPoint.y < sample.transform.position.y
                    && Dots.instance.ImpactMarkerVisible,
                room.name + ": impact marker stops on the nearest enemy instead of the wall behind it");
            Check(aim.LastAimProjectileRadius > 0.01f && sampleCollider != null
                    && Mathf.Abs(aim.LastAimHitPoint.y
                        - (sampleCollider.bounds.min.y - aim.LastAimProjectileRadius)) < 0.03f,
                room.name + ": aim impact uses the selected projectile's real collision radius");
            BoxCollider2D sampleBox = sampleCollider as BoxCollider2D;
            if (sample.Footprint == Vector2Int.one && sampleBox != null)
                Check(sampleBox.size == new Vector2(0.9f, 0.9f),
                    room.name + ": one-cell enemies keep clearance instead of overlapping adjacent cells");
            aim.HideAim(true);
            sample.transform.position = savedPosition;
            Physics2D.SyncTransforms();
            float hiddenTestY = darkness.HasDarkness
                ? Mathf.Min(room.transform.position.y + room.BattlefieldSize.y * 0.5f - 0.5f,
                    darkness.LightBoundaryY + 0.55f)
                : room.transform.position.y + room.BattlefieldSize.y * 0.5f - 0.5f;
            sample.transform.position = new Vector3(room.PlayerObject.transform.position.x,
                hiddenTestY, savedPosition.z);
            yield return null;
            yield return null;
            if (room.DefenseLine > 1)
            {
                Check(sample.GetComponent<EnemyDarknessVisibility>().IsHidden
                    && sample.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff),
                    room.name + ": enemy sprites are fully concealed independently of the translucent overlay");
                Check(sample.GetComponentsInChildren<CanvasRenderer>(true).All(r => r.GetAlpha() == 0f),
                    room.name + ": enemy's prefab HP bar is concealed as well");
                Physics2D.SyncTransforms();
                aim.ShowAim(sample.transform.position);
                yield return null;
                Check(aim.LastAimHitEnemy != sample,
                    room.name + ": firefly guide enters darkness without revealing a concealed enemy");
                aim.HideAim(true);
                int visibleBefore = ui.VisibleHealthLabelCount;
                int numbersBefore = ui.ActiveDamageNumberCount;
                float healthBefore = sample.hp;
                sample.TakeDamage(1f);
                Check(sample.hp == healthBefore - 1f, room.name + ": hidden enemies still take damage");
                Check(ui.ActiveDamageNumberCount > numbersBefore,
                    room.name + ": damage numbers remain visible while the enemy itself stays hidden");
                Check(sample.GetComponent<Collider2D>().enabled && sample.GetComponent<Rigidbody2D>().simulated,
                    room.name + ": fog leaves collision physics active");
                // Place the enemy just beyond the border and advance it into the light.
                sample.transform.position = new Vector3(room.transform.position.x, darkness.LightBoundaryY + 0.45f, savedPosition.z);
                Vector3 scaleBeforeStep = sample.transform.localScale;
                sample.Step();
                Check(sample.IsStepping, room.name + ": enemy uses a visible jump instead of teleporting between cells");
                float stepTimeout = Time.realtimeSinceStartup + sample.StepAnimationDuration + 1f;
                while (sample != null && sample.IsStepping && Time.realtimeSinceStartup < stepTimeout) yield return null;
                Check(sample != null && !sample.IsStepping
                    && Vector3.Distance(sample.transform.localScale, scaleBeforeStep) < 0.001f,
                    room.name + ": enemy restores its original scale after landing");
                Check(darkness.IsVisible(sample.transform.position) && ui.VisibleHealthLabelCount == visibleBefore + 1,
                    room.name + ": advancing into light reveals numeric HP (visible=" + darkness.IsVisible(sample.transform.position)
                    + ", labels=" + ui.VisibleHealthLabelCount + ", before=" + visibleBefore + ", y=" + sample.transform.position.y
                    + ", boundary=" + darkness.LightBoundaryY + ")");
                Check(!sample.GetComponent<EnemyDarknessVisibility>().IsHidden
                    && sample.GetComponentsInChildren<Renderer>(true).All(r => !r.forceRenderingOff),
                    room.name + ": entering light restores every enemy sprite");
                CanvasRenderer[] prefabHealth = sample.GetComponentsInChildren<CanvasRenderer>(true);
                Check(prefabHealth.Length == 0 || prefabHealth.Any(r => r.GetAlpha() > 0f),
                    room.name + ": entering light restores the optional prefab HP bar");
                sample.TakeDamage(1f);
                Check(ui.ActiveDamageNumberCount > numbersBefore, room.name + ": visible hits display damage numbers");
                sample.transform.position = savedPosition;
            }
            Canvas.ForceUpdateCanvases();
            yield return null;
            Capture(room, Camera.main);
        }

        Stage outer = rooms.First(r => r.DefenseLine == 4);
        RoomDarkness fog = outer.GetComponent<RoomDarkness>();
        camera.ForceFocusRoom(outer);
        fog.enabled = false;
        yield return null;
        Check(!fog.Overlay.gameObject.activeSelf && fog.IsVisible(outer.transform.position + Vector3.up * 3f),
            "Disabling darkness reveals the room without disabling gameplay");
        fog.enabled = true;
        yield return null;
        yield return null;
        Check(fog.Overlay.gameObject.activeSelf && !fog.IsVisible(outer.transform.position + Vector3.up * 3f),
            "Re-enabling darkness restores both visuals and information hiding");
        int children = outer.transform.childCount;
        for (int i = 0; i < 10; i++) fog.Refresh();
        Check(outer.transform.childCount == children, "Refreshing darkness reuses its single overlay");
        ValidateProjectileRendering(outer, Camera.main, fog);

        LevelTransition transition = Object.FindObjectOfType<LevelTransition>();
        transition.PauseButtonPressed();
        Canvas pauseCanvas = transition.PausePanel.GetComponent<Canvas>();
        Check(pauseCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            && pauseCanvas.overrideSorting && pauseCanvas.sortingOrder > fog.Overlay.GetComponent<Canvas>().sortingOrder,
            "Real pause panel is a screen overlay above darkness in the fourth line");
        CapturePause(transition, Camera.main, fog);
        transition.ContinueButtonPressed();
        manager.isPaused = true;

        // Verify that concealment and world dimming remain independent Inspector settings.
        var settings = new SerializedObject(fog);
        settings.FindProperty("useLineLighting").boolValue = false;
        settings.FindProperty("litFraction").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        fog.Refresh();
        Check(!fog.IsVisible(outer.PlayerObject.transform.position) && fog.Overlay.color.a < 1f,
            "Zero lighting conceals enemies everywhere but does not black out the board");
        settings.FindProperty("environmentOpacity").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        fog.Refresh();
        Check(!fog.IsVisible(outer.PlayerObject.transform.position) && fog.Overlay.color.a == 0f,
            "Enemy concealment does not depend on overlay opacity");
        settings.FindProperty("litFraction").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        fog.Refresh();
        Check(!fog.HasDarkness && !fog.Overlay.gameObject.activeSelf, "Full manual lighting removes the overlay");
    }

    private static void Capture(Stage room, Camera camera)
    {
        var target = new RenderTexture(720, 1280, 24);
        var image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        var unobscured = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        GameObject probe = null;
        Sprite probeSprite = null;
        RoomDarkness fog = room.GetComponent<RoomDarkness>();
        try
        {
            // A deterministic white patch tests actual alpha blending, not just component settings.
            probe = new GameObject("Darkness world rendering probe", typeof(SpriteRenderer));
            probeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            probe.GetComponent<SpriteRenderer>().sprite = probeSprite;
            probe.GetComponent<SpriteRenderer>().sortingOrder = 50;
            probe.transform.position = new Vector3(room.transform.position.x,
                room.transform.position.y + room.BattlefieldSize.y * 0.5f - 1f,
                room.GetComponentInChildren<LevelGenerator>(true).transform.position.z);
            probe.transform.localScale = Vector3.one * 0.3f;
            camera.targetTexture = target;
            camera.GetComponent<CameraRenderer>().FrameRoom(room);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            image.Apply();
            if (room.DefenseLine > 1)
            {
                Vector3 pixel = camera.WorldToScreenPoint(probe.transform.position);
                Color dimmed = image.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
                fog.Overlay.gameObject.SetActive(false);
                camera.Render();
                unobscured.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                unobscured.Apply();
                Color lit = unobscured.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
                fog.Overlay.gameObject.SetActive(true);
                Check(lit.maxColorComponent > 0.9f && dimmed.maxColorComponent > 0.04f
                    && dimmed.maxColorComponent < lit.maxColorComponent - 0.35f,
                    room.name + ": far-row scenery is visibly dimmed, not obscured (" + dimmed + ")");
            }
            File.WriteAllBytes("Logs/Darkness-Line" + room.DefenseLine + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            Object.Destroy(image);
            Object.Destroy(unobscured);
            Object.Destroy(target);
            if (probe != null) Object.Destroy(probe);
            if (probeSprite != null) Object.Destroy(probeSprite);
        }
    }

    private static void ValidateProjectileRendering(Stage room, Camera camera, RoomDarkness fog)
    {
        var target = new RenderTexture(720, 1280, 24);
        var image = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        var queue = AnimalProjectileQueue.Instance;
        GameObject projectile = null;
        try
        {
            camera.targetTexture = target;
            for (int animalIndex = 0; animalIndex < queue.Animals.Count; animalIndex++)
            {
                Vector3 position = new Vector3(room.transform.position.x,
                    room.transform.position.y + room.BattlefieldSize.y * 0.5f - 1.5f,
                    room.GetComponentInChildren<LevelGenerator>(true).transform.position.z);
                projectile = Object.Instantiate(queue.Animals[animalIndex].Prefab, position, Quaternion.identity);
                projectile.GetComponent<Animal>().enabled = false;
                projectile.GetComponent<Rigidbody2D>().simulated = false;
                SpriteRenderer[] sprites = projectile.GetComponentsInChildren<SpriteRenderer>();
                bool[] enabledBefore = sprites.Select(s => s.enabled).ToArray();
                Color32[][] shots = new Color32[4][];
                for (int pass = 0; pass < 4; pass++)
                {
                    fog.Overlay.gameObject.SetActive(pass >= 2);
                    for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = pass % 2 == 0 && enabledBefore[i];
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                    image.Apply();
                    shots[pass] = image.GetPixels32();
                }
                float plain = MaximumPixelDifference(shots[0], shots[1]);
                float dim = MaximumPixelDifference(shots[2], shots[3]);
                Check(dim > 0.015f && dim < plain * 0.9f,
                    queue.Animals[animalIndex].DisplayName + ": projectile remains rendered but dimmed in darkness ("
                    + dim.ToString("F3") + " vs " + plain.ToString("F3") + ")");
                projectile.SetActive(false);
                Object.Destroy(projectile);
                projectile = null;
            }
        }
        finally
        {
            if (projectile != null) { projectile.SetActive(false); Object.Destroy(projectile); }
            fog.Overlay.gameObject.SetActive(true);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            Object.Destroy(image);
            Object.Destroy(target);
        }
    }

    private static float MaximumPixelDifference(Color32[] a, Color32[] b)
    {
        int difference = 0;
        for (int i = 0; i < a.Length; i++)
            difference = Mathf.Max(difference, Mathf.Max(Mathf.Abs(a[i].r - b[i].r),
                Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
        return difference / 255f;
    }

    private static void CapturePause(LevelTransition transition, Camera camera, RoomDarkness fog)
    {
        Canvas[] roots = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas
            && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] previousCameras = roots.Select(c => c.worldCamera).ToArray();
        float[] previousDistances = roots.Select(c => c.planeDistance).ToArray();
        var target = new RenderTexture(720, 1280, 24);
        var withFog = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        var withoutFog = new Texture2D(720, 1280, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            // Route screen UI into the capture camera, preserving its real sorting orders.
            camera.targetTexture = target;
            foreach (Canvas root in roots)
            {
                root.renderMode = RenderMode.ScreenSpaceCamera;
                root.worldCamera = camera;
                root.planeDistance = 2f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            withFog.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            withFog.Apply();
            fog.Overlay.gameObject.SetActive(false);
            camera.Render();
            withoutFog.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            withoutFog.Apply();
            Image button = transition.PausePanel.GetComponentsInChildren<Button>()
                .Select(b => b.GetComponent<Image>()).First(i => i != null && i.color.a > 0.99f);
            Vector3 pixel = camera.WorldToScreenPoint(button.rectTransform.TransformPoint(button.rectTransform.rect.center));
            Color a = withFog.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
            Color b = withoutFog.GetPixel(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y));
            Check(a.maxColorComponent > 0.1f && ColorDistance(a, b) < 0.02f,
                "Rendered pause button stays visible and its color is unchanged by room darkness");
            File.WriteAllBytes("Logs/Darkness-Pause.png", withFog.EncodeToPNG());
        }
        finally
        {
            fog.Overlay.gameObject.SetActive(true);
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].renderMode = RenderMode.ScreenSpaceOverlay;
                roots[i].worldCamera = previousCameras[i];
                roots[i].planeDistance = previousDistances[i];
            }
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            Object.Destroy(withFog);
            Object.Destroy(withoutFog);
            Object.Destroy(target);
        }
    }

    private static float ColorDistance(Color a, Color b) =>
        Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

    private static void Check(bool success, string description)
    {
        if (!success) throw new Exception(description);
        report.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunningKey, false);
        string[] saved = SessionState.GetString(SavedRequestKey, "").Split('|');
        for (int i = 0; i < PrefsKeys.Length && i < saved.Length; i++)
            if (saved[i] == "-") PlayerPrefs.DeleteKey(PrefsKeys[i]); else PlayerPrefs.SetInt(PrefsKeys[i], int.Parse(saved[i]));
        PlayerPrefs.Save();
        string result = error == null ? "PASS: " + report.Count + " darkness checks." : "FAIL: " + error;
        report.Add(result);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/RoomDarknessValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

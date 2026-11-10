using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BogImpactSizeValidation
{
    private const string Running = "ForestAnimalWar.BogImpactSizeValidation";
    private static IEnumerator checks;
    private static double deadline;
    private static Camera camera;

    static BogImpactSizeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = Checks();
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        new GameObject("Player").tag = "Player";
        camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(20, 28, 24, 255);
        var room = new GameObject("Room", typeof(Stage), typeof(LevelGenerator)).GetComponent<LevelGenerator>();
        Brick[] enemies = new[] { "SwampDemon", "BogPustulant" }.Select((name, i) =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/" + name + ".prefab");
            Require(prefab != null, "Missing actual enemy prefab: " + name);
            var enemy = Object.Instantiate(prefab, new Vector3(i * 2 - 1, 0, 0), Quaternion.identity).GetComponent<Brick>();
            enemy.levelGenerator = room;
            room.EnemyCollection.Add(enemy.gameObject);
            enemy.hp = 10000f;
            return enemy;
        }).ToArray();
        var projectile = new GameObject("Normal hit", typeof(Rigidbody2D), typeof(Animal)).GetComponent<Animal>();
        projectile.enabled = false;
        projectile.GetComponent<Rigidbody2D>().simulated = false;
        projectile.dmg = 1f;
        yield return null;

        for (int cycle = 0; cycle < 36; cycle++)
        {
            // Reuse the same effect pool across fire, white dust, slime explosion,
            // then the real ordinary-hit callback of both authored swamp enemies.
            if (cycle % 3 == 0) EnemyImpactEffectPool.PlayBogExplosion(Vector3.zero, true, 2f);
            else if (cycle % 3 == 1) EnemyImpactEffectPool.PlayWhiteDust(Vector3.zero, Vector2.up, Color.white, 16, 1f);
            else EnemyImpactEffectPool.PlayBogExplosion(Vector3.zero, false, 2f);
            float end = Time.time + 0.7f;
            while (Time.time < end) { CheckDropBounds(false); yield return null; }
            foreach (Brick enemy in enemies)
            {
                Require(enemy.ApplyProjectileHit(projectile, enemy.transform.position, Vector2.up), "Ordinary hit rejected");
                EnemyImpactEffectPool effect = EnemyImpactEffectPool.LastPlayed;
                Require(effect != null && !effect.GetComponent<ParticleSystemRenderer>().enabled
                    && effect.GetComponent<ParticleSystem>().particleCount == 0,
                    "Ordinary swamp hit must not render particle billboards");
                Require(effect.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled) >= 13,
                    "Actual swamp enemy must still show visible slime drops");
            }
            if (cycle == 0 || cycle == 18 || cycle == 35)
            {
                yield return null;
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>()) canvas.enabled = false;
                CaptureAndCheck("Logs/BogHit-" + cycle + ".png");
            }
            end = Time.time + 0.7f;
            while (Time.time < end) { CheckDropBounds(true); yield return null; }
        }
        Require(EnemyImpactEffectPool.ActiveEffectCount == 0, "All bursts must finish and return to the pool");
        File.WriteAllText("Logs/BogImpactSizeValidation.txt",
            "PASSED: 108 bursts, including 72 ordinary hits on actual swamp prefabs.\n"
            + "Mixed fire/dust/slime pool reuse; every visible drop bounded throughout its lifetime.\n"
            + "Three rendered captures contain no pixels outside the local enemy hit region.\n");
    }

    private static void CheckDropBounds(bool splash)
    {
        foreach (EnemyImpactEffectPool effect in Object.FindObjectsOfType<EnemyImpactEffectPool>())
        foreach (SpriteRenderer drop in effect.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled))
        {
            Require(drop.bounds.size.x <= 0.43f && drop.bounds.size.y <= 0.43f,
                "A rotated drop exceeded its fixed world diameter: " + drop.bounds.size);
            Require(!float.IsNaN(drop.bounds.size.x) && !float.IsInfinity(drop.bounds.size.x), "Invalid drop bounds");
            if (splash) Require(drop.transform.localPosition.magnitude <= 0.651f, "Ordinary hit spread exceeds its local radius");
        }
    }

    private static void CaptureAndCheck(string path)
    {
        var render = new RenderTexture(512, 512, 24);
        render.Create(); camera.targetTexture = render; camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        Color32[] pixels = image.GetPixels32();
        for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
        {
            float wx = (x / 511f - 0.5f) * 6f, wy = (y / 511f - 0.5f) * 6f;
            if (Mathf.Abs(wx) <= 2.1f && Mathf.Abs(wy) <= 1.4f) continue;
            Color32 color = pixels[y * 512 + x];
            Require(Mathf.Abs(color.r - 20) <= 5 && Mathf.Abs(color.g - 28) <= 5 && Mathf.Abs(color.b - 24) <= 5,
                "Hit rendering escaped its local region at pixel " + x + "," + y);
        }
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.Destroy(image); render.Release(); Object.Destroy(render);
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Bog effect test timed out");
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (error != null)
        {
            File.WriteAllText("Logs/BogImpactSizeValidation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        else Debug.Log("BOG_IMPACT_SIZE_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

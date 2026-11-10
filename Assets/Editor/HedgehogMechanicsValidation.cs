using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch-only play-mode validation. Run from an isolated project copy.
[InitializeOnLoad]
public static class HedgehogMechanicsValidation
{
    private const string Running = "ForestAnimalWar.HedgehogValidation.Running";
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private static int checks;
    private static int lastFrame = -1;
    private static double deadline;

    static HedgehogMechanicsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 45;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        yield return null;

        GameManager manager = GameManager.Instance;
        if (manager != null) manager.enabled = false;
        Player player = Object.FindObjectsOfType<Player>().First(component => component.isActiveAndEnabled);
        player.enabled = false;
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;

        Check(queue != null && queue.Animals.Count >= 3, "Projectile queue exposes all three animals");
        GameObject hedgehogPrefab = queue.Animals[1].Prefab;
        GameObject rabbitPrefab = queue.Animals[2].Prefab;
        Check(hedgehogPrefab != null && rabbitPrefab != null && hedgehogPrefab != rabbitPrefab,
            "Hedgehog and rabbit use independent prefab assets");
        Check(Mathf.Approximately(hedgehogPrefab.GetComponent<Animal>().dmg, 60f)
            && Mathf.Approximately(rabbitPrefab.GetComponent<Animal>().dmg, 150f),
            "Hedgehog and rabbit keep independent direct damage values");
        Check(Mathf.Approximately(queue.GetExplosionRadius(1), 1.2f)
            && Mathf.Approximately(queue.GetExplosionDuration(1), 0.18f)
            && queue.GetExplosionDamageMultiplier(1) > 0f
            && Mathf.Approximately(queue.GetHedgehogQuillDamageGrowthPerHit(1), 0f),
            "Hedgehog starts with its compact pulse and no flat upgrade damage");

        for (int i = 0; i < 3; i++)
        {
            GameObject prefab = queue.Animals[i].Prefab;
            Sprite sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Check(assetPath.EndsWith("-v2-simple.png") && sprite.texture.width == 256
                && importer != null && importer.alphaIsTransparency
                && importer.spriteImportMode == SpriteImportMode.Single,
                "Animal " + i + " uses its own compact transparent 256px sprite");
            GameObject sample = Object.Instantiate(prefab, new Vector3(110f + i * 3f, 100f, 0f), Quaternion.identity);
            ProjectileFlightVisual visual = sample.GetComponent<ProjectileFlightVisual>();
            Check(visual != null && visual.Renderer != null && visual.Renderer.transform != sample.transform,
                "Animal " + i + " has a separate spinning visual");
            Check(sample.transform.Find("Circle").GetComponent<SpriteRenderer>().enabled == (i == 0),
                "Only the firefly keeps its existing aura (animal " + i + ")");
            sample.GetComponent<ProjectilePhysics2D>().Launch(Vector2.up, 6f);
            float rotationBefore = visual.Renderer.transform.eulerAngles.z;
            float spinUntil = Time.time + 0.06f;
            while (Time.time < spinUntil) yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(rotationBefore, visual.Renderer.transform.eulerAngles.z)) > 1f
                && Mathf.Abs(sample.GetComponent<Rigidbody2D>().rotation) < 0.1f
                && Mathf.Approximately(sample.GetComponent<CircleCollider2D>().radius, 0.5f),
                "Animal " + i + " spins without changing its physics body or collider");
            Object.Destroy(sample);
            yield return null;
        }

        Vector3 origin = new Vector3(100f, 100f, 0f);
        GameObject hedgehogObject = Object.Instantiate(hedgehogPrefab, origin, Quaternion.identity);
        Animal hedgehogAnimal = hedgehogObject.GetComponent<Animal>();
        hedgehogAnimal.SetReturnTarget(player.gameObject);
        HedgehogExplosion explosion = hedgehogObject.AddComponent<HedgehogExplosion>();
        explosion.Configure(hedgehogAnimal.dmg, queue.GetExplosionRadius(1),
            queue.GetExplosionDuration(1), queue.GetExplosionDamageMultiplier(1));

        CircleCollider2D hedgehogCollider = hedgehogObject.GetComponent<CircleCollider2D>();
        SpriteRenderer hedgehogRenderer = hedgehogObject.GetComponentsInChildren<SpriteRenderer>(true)
            .First(renderer => renderer.enabled && renderer.transform.name != "Circle");
        float visualToCollider = hedgehogRenderer.bounds.size.x / hedgehogCollider.bounds.size.x;
        Check(visualToCollider > 1.9f && visualToCollider < 2.1f,
            "Hedgehog uses the larger prefab-authored picture while its collider stays unchanged");

        Brick enemyPrefab = Object.FindObjectOfType<LevelGenerator>().brickPrefab.GetComponent<Brick>();
        Brick directEnemy = SpawnEnemy(enemyPrefab, origin + Vector3.up * 1.25f, 1000f);
        Brick nearbyEnemy = SpawnEnemy(enemyPrefab, origin + new Vector3(0.9f, 1.25f, 0f), 1000f);
        Brick distantEnemy = SpawnEnemy(enemyPrefab, origin + new Vector3(2.2f, 1.25f, 0f), 1000f);
        float directBefore = directEnemy.hp;
        float nearbyBefore = nearbyEnemy.hp;
        float distantBefore = distantEnemy.hp;

        ProjectilePhysics2D flight = hedgehogObject.GetComponent<ProjectilePhysics2D>();
        flight.Launch(Vector2.up, 8f);
        Rigidbody2D hedgehogBody = hedgehogObject.GetComponent<Rigidbody2D>();
        float initialBodyRotation = hedgehogBody.rotation;
        float initialVisualRotation = hedgehogRenderer.transform.eulerAngles.z;
        float collisionDeadline = Time.time + 1f;
        while (Object.FindObjectOfType<HedgehogExplosionPulse>() == null
            && Time.time < collisionDeadline) yield return null;

        Check(hedgehogObject != null && flight.IsFlying
            && hedgehogObject.GetComponent<Rigidbody2D>().simulated,
            "First enemy hit leaves the hedgehog alive and in normal flight");
        Check(Mathf.Abs(Mathf.DeltaAngle(initialVisualRotation, hedgehogRenderer.transform.eulerAngles.z)) > 1f
            && Mathf.Abs(Mathf.DeltaAngle(initialBodyRotation, hedgehogBody.rotation)) < 0.1f,
            "Hedgehog visual rotates without touching the interpolated physics body");
        HedgehogExplosionPulse burst = Object.FindObjectOfType<HedgehogExplosionPulse>();
        Check(burst != null, "Enemy collision creates a detached quill burst");
        MeshFilter filter = burst.GetComponent<MeshFilter>();
        Check(filter != null && filter.sharedMesh.vertexCount == 16 * 9
            && burst.GetComponent<SpriteRenderer>() == null,
            "Sixteen separate quills replace the solid circle in one small mesh");
        Check(filter.sharedMesh.colors32.All(color => color.r >= color.g && color.g >= color.b)
            && burst.GetComponent<MeshRenderer>().sortingOrder > RoomDarkness.WorldOverlayOrder,
            "Quills use brown/ivory colors and render above room darkness");
        float pictureTime = Time.time + 0.035f;
        while (Time.time < pictureTime) yield return null;
        CaptureBurst(burst.transform.position);

        float pulseDeadline = Time.time + 0.24f;
        while (Time.time < pulseDeadline) yield return null;
        float configuredAreaDamage = hedgehogAnimal.dmg * queue.GetExplosionDamageMultiplier(1);
        Check(Mathf.Approximately(directEnemy.hp,
                directBefore - hedgehogAnimal.dmg - configuredAreaDamage),
            "Direct target receives direct and configured area damage");
        Check(Mathf.Approximately(nearbyEnemy.hp, nearbyBefore - configuredAreaDamage),
            "Nearby target receives only the reduced area damage (before=" + nearbyBefore
            + ", after=" + nearbyEnemy.hp + ")");
        Check(Mathf.Approximately(distantEnemy.hp, distantBefore),
            "Reduced area radius does not reach distant enemies");
        Check(Object.FindObjectOfType<HedgehogExplosionPulse>() == null,
            "Impact animation completes in well under a quarter second");
        Check(hedgehogObject != null && flight.IsFlying,
            "Hedgehog keeps flying after its impact animation finishes");

        float healthBeforeReuse = nearbyEnemy.hp;
        HedgehogExplosionPulse reused = HedgehogExplosionPulse.Create(hedgehogRenderer,
            origin + Vector3.up * 1.25f, 15f, 1.2f, 0.18f);
        Check(reused == burst, "A completed quill mesh is reused from the pool");
        pulseDeadline = Time.time + 0.24f;
        while (Time.time < pulseDeadline) yield return null;
        Check(Mathf.Approximately(nearbyEnemy.hp, healthBeforeReuse - 15f),
            "Reused bursts reset their targets and damage each enemy only once");

        HedgehogQuillStacks stacks = nearbyEnemy.gameObject.AddComponent<HedgehogQuillStacks>();
        Check(Mathf.Approximately(stacks.RegisterExplosion(7, 3f), 0f)
                && Mathf.Approximately(stacks.RegisterExplosion(7, 3f), 3f)
                && Mathf.Approximately(stacks.RegisterExplosion(7, 3f), 6f)
                && Mathf.Approximately(stacks.RegisterExplosion(8, 3f), 0f),
            "Quill damage grows per enemy during one turn and resets on the next turn");

        Object.Destroy(hedgehogObject);
        Object.Destroy(directEnemy.gameObject);
        Object.Destroy(nearbyEnemy.gameObject);
        Object.Destroy(distantEnemy.gameObject);
        // Let pending projectile OnDestroy callbacks finish while rooms still exist,
        // before the batch editor tears the entire scene down.
        yield return null;
        yield return null;
    }

    private static void CaptureBurst(Vector3 position)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        GameObject cameraObject = new GameObject("Quill proof camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = position + Vector3.back * 10f;
        camera.orthographic = true;
        camera.orthographicSize = 1.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.11f, 0.14f, 0.12f);
        RenderTexture target = RenderTexture.GetTemporary(512, 512, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D capture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            capture.Apply();
            File.WriteAllBytes("Logs/HedgehogQuillBurst.png", capture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(capture);
            Object.Destroy(cameraObject);
        }
    }

    private static Brick SpawnEnemy(Brick prefab, Vector3 position, float health)
    {
        Brick enemy = Object.Instantiate(prefab, position, Quaternion.identity);
        enemy.hp = health;
        enemy.levelGenerator = null;
        return enemy;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hedgehog validation timed out");
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

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        report.Add("PASS " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Time.timeScale = 1f;
        string result = error == null
            ? "PASS: " + checks + " hedgehog integration checks."
            : "FAIL: " + error;
        report.Add(result);
        File.WriteAllLines("Logs/HedgehogMechanicsValidation.txt", report);
        if (error == null) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

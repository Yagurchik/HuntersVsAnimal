using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated editor copy: -executeMethod RabbitGrowthValidation.RunBatch
[InitializeOnLoad]
public static class RabbitGrowthValidation
{
    private const string Running = "ForestAnimalWar.RabbitGrowthValidation.Running";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double deadline;
    private static int lastFrame = -1;

    static RabbitGrowthValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60;
            steps.Clear();
            steps.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run only in an isolated Unity project copy.");
        SessionState.SetBool(Running, true);
        GameRunMode.PrepareCampaign();
        LevelProgression.PrepareLevel(1);
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        GameManager manager = GameManager.Instance;
        Check(manager != null, "Gameplay is loaded");
        manager.enabled = false;
        manager.isPaused = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/AnimalsBullet/Rabbit.prefab");
        RabbitReplication authored = prefab != null
            ? prefab.GetComponent<RabbitReplication>() : null;
        Check(authored != null && Mathf.Approximately(authored.SizeGrowthPerHit, 0.05f),
            "Rabbit prefab has editable five-percent growth");

        GameObject instance = Object.Instantiate(prefab);
        RabbitReplication rabbit = instance.GetComponent<RabbitReplication>();
        Animal projectile = instance.GetComponent<Animal>();
        CircleCollider2D collider = instance.GetComponent<CircleCollider2D>();
        Check(collider != null, "Rabbit has a physical collider");
        rabbit.Configure(prefab, 3, 18f);
        Brick target = Object.Instantiate(manager.rooms[0].brickPrefab).GetComponent<Brick>();
        target.hp = 100000f;
        Vector3 originalScale = instance.transform.localScale;
        Physics2D.SyncTransforms();
        float originalRadius = collider.bounds.extents.x;

        Check(target.ApplyProjectileHit(projectile, instance.transform.position, Vector2.up),
            "First real projectile hit is resolved");
        Physics2D.SyncTransforms();
        Check(Close(instance.transform.localScale.x, originalScale.x * 1.05f)
            && Close(collider.bounds.extents.x, originalRadius * 1.05f),
            "First hit grows image and collider by five percent");
        target.ApplyProjectileHit(projectile, instance.transform.position, Vector2.up);
        Physics2D.SyncTransforms();
        Check(Close(instance.transform.localScale.x, originalScale.x * 1.05f * 1.05f)
            && Close(collider.bounds.extents.x, originalRadius * 1.05f * 1.05f),
            "Second hit grows from the current size");

        target.ApplyProjectileHit(projectile, instance.transform.position, Vector2.up);
        Physics2D.SyncTransforms();
        RabbitReplication replica = Object.FindObjectsOfType<RabbitReplication>()
            .FirstOrDefault(candidate => candidate != rabbit);
        Check(replica != null && Close(instance.transform.localScale.x, originalScale.x)
            && Close(collider.bounds.extents.x, originalRadius),
            "Reproduction resets the original rabbit and its collider");
        Check(Close(replica.transform.localScale.x, originalScale.x),
            "New rabbit starts at its normal size");
    }

    private static bool Close(float left, float right)
        => Mathf.Abs(left - right) < 0.0005f;

    private static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Rabbit growth validation timed out");
            while (steps.Count > 0)
            {
                IEnumerator current = steps.Peek();
                if (!current.MoveNext()) { steps.Pop(); continue; }
                if (current.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("PASS: " + description);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (error != null) Debug.LogError("FAIL: " + error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

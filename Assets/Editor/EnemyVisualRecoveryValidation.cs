using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyVisualRecoveryValidation
{
    private const string Running = "EnemyVisualRecoveryValidation.Running";
    private static int lastFrame = -1;
    private static int frames;
    private static Brick sample;
    private static SpriteRenderer head;
    private static Sprite original;

    static EnemyVisualRecoveryValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(Running, false)) return;
            frames = 0;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated project copy.");
        try
        {
            AuditAnimationReferences();
            LevelProgression.PrepareLevel(1);
            EditorSceneManager.OpenScene("Assets/Asset.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void AuditAnimationReferences()
    {
        int framesChecked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip",
            new[] { "Assets/MonsterSprite" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.propertyName != "m_Sprite") continue;
                foreach (ObjectReferenceKeyframe frame in
                    AnimationUtility.GetObjectReferenceCurve(clip, binding))
                {
                    if (!(frame.value is Sprite))
                        throw new InvalidOperationException("Missing idle sprite in " + path
                            + " at " + frame.time);
                    framesChecked++;
                }
            }
        }
        Debug.Log("Enemy animation audit resolved " + framesChecked + " sprite frames.");
    }

    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        frames++;
        try
        {
            if (frames == 4)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Brick.prefab");
                sample = Object.Instantiate(prefab).GetComponent<Brick>();
                head = sample.transform.Find("Visual").GetComponent<SpriteRenderer>();
                original = head.sprite;
                if (original == null)
                    throw new InvalidOperationException("Base skeleton starts without a sprite.");
                head.sprite = null;
            }
            else if (frames == 5)
            {
                if (head.sprite == null)
                    throw new InvalidOperationException("Idle sprite did not recover after becoming null.");
                sample.GetComponent<EnemyHitReaction>().Play();
            }
            else if (frames == 6)
            {
                sample.GetComponent<EnemyHitReaction>().Cancel();
                if (head.sprite == null)
                    throw new InvalidOperationException("Idle sprite did not recover after damage pose.");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/EnemyVisualRecoveryValidation.txt",
                    "PASS: all enemy animation sprite references resolve and a missing idle"
                    + " sprite recovers before and after damage.");
                Debug.Log("ENEMY_VISUAL_RECOVERY_PASSED");
                Finish(0);
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Fail(Exception error)
    {
        Debug.LogException(error);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/EnemyVisualRecoveryValidation.txt", error.ToString());
        Finish(1);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        EditorApplication.Exit(code);
    }
}

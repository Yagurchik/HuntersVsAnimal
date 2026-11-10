using System;
using UnityEditor;
using UnityEngine;

public static class EnemyHoverValidation
{
    private static readonly string[] EnemyPaths = {
        "Assets/Enemy/Brick.prefab",
        "Assets/Resources/Enemies/Zombie.prefab",
        "Assets/Resources/Enemies/ArmoredSkeleton.prefab",
        "Assets/Resources/Enemies/LichRegular.prefab",
        "Assets/Resources/Enemies/LichBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab",
        "Assets/Resources/Enemies/GhostlySorrowBoss.prefab",
        "Assets/Resources/Enemies/PoisonousGloomBoss.prefab",
        "Assets/Resources/Enemies/HiveHollowMiniBoss.prefab",
        "Assets/Resources/Enemies/GreenJudgeMiniBoss.prefab",
        "Assets/Resources/Enemies/BellRingerBoss.prefab",
        "Assets/Resources/Enemies/KnotBinderBoss.prefab",
        "Assets/Resources/Enemies/VolleyEaterBoss.prefab",
        "Assets/Resources/Enemies/ThreeSealIdolBoss.prefab",
        "Assets/Resources/Enemies/ImpostorBoss.prefab"
    };

    [MenuItem("Tools/Forest Animal War/Tests/Check Enemy Hover")]
    public static void Check()
    {
        foreach (string path in EnemyPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            Brick brick = prefab.GetComponent<Brick>();
            Transform visual = prefab.transform.Find("Visual");
            EnemyVisualHover hover = visual != null
                ? visual.GetComponent<EnemyVisualHover>() : null;
            if (brick == null || hover == null || visual.GetComponent<SpriteRenderer>() == null)
                throw new InvalidOperationException("Missing editable hover animation: " + path);
            SerializedObject settings = new SerializedObject(brick);
            if (!Mathf.Approximately(settings.FindProperty("stepJumpHeight").floatValue, 0f)
                || !Mathf.Approximately(settings.FindProperty("stepStretch").floatValue, 0f)
                || !Mathf.Approximately(settings.FindProperty("landingSquash").floatValue, 0f))
                throw new InvalidOperationException("Enemy still jumps or squashes: " + path);
        }

        const string ghostPath = "Assets/Resources/Enemies/Ghost.prefab";
        GameObject ghost = AssetDatabase.LoadAssetAtPath<GameObject>(ghostPath);
        if (ghost == null || ghost.GetComponentInChildren<GhostVisualAnimator>() == null
            || ghost.GetComponentInChildren<EnemyVisualHover>() != null)
            throw new InvalidOperationException("Ghost animation was changed unexpectedly.");
        Debug.Log("Enemy hover validation passed: editable prefabs glide and hover; ghost keeps its own animation.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        try
        {
            Check();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}

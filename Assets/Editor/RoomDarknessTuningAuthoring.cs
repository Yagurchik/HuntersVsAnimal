using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RoomDarknessTuningAuthoring
{
    private const string GameScene = "Assets/Asset.unity";

    [MenuItem("Forest Animal War/Gameplay/Tune line darkness")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing the scene.");
        var scene = EditorSceneManager.OpenScene(GameScene);
        Stage[] rooms = Object.FindObjectsOfType<Stage>(true);
        int[] counts = new int[5];
        foreach (Stage room in rooms)
        {
            if (room.DefenseLine < 1 || room.DefenseLine > 4)
                throw new InvalidOperationException("Unexpected defense line in " + room.name);
            RoomDarkness darkness = room.GetComponent<RoomDarkness>();
            if (darkness == null)
                throw new InvalidOperationException("Missing editable darkness in " + room.name);
            var settings = new SerializedObject(darkness);
            settings.FindProperty("environmentOpacity").floatValue = OpacityForLine(room.DefenseLine);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(darkness);
            BattlefieldForestStyle frame = room.GetComponent<BattlefieldForestStyle>();
            if (frame == null)
                throw new InvalidOperationException("Missing editable battlefield frame in " + room.name);
            frame.RefreshFrameSorting();
            EditorUtility.SetDirty(frame);
            if (frame.FrameRenderer != null) EditorUtility.SetDirty(frame.FrameRenderer);
            foreach (SpriteRenderer edge in frame.BoardEdgeRenderers)
                if (edge != null) EditorUtility.SetDirty(edge);
            if (frame.VineRenderer != null) EditorUtility.SetDirty(frame.VineRenderer);
            foreach (ForestEyeBlinker eyes in frame.BlinkingEyes)
            {
                if (eyes == null) continue;
                if (eyes.LeftEye != null) EditorUtility.SetDirty(eyes.LeftEye);
                if (eyes.RightEye != null) EditorUtility.SetDirty(eyes.RightEye);
            }
            counts[room.DefenseLine]++;
        }
        if (rooms.Length != 16 || counts[1] != 1 || counts[2] != 3
            || counts[3] != 5 || counts[4] != 7)
            throw new InvalidOperationException("Unexpected room layout while tuning darkness.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Authored room darkness saved: line 2 0.84, line 3 0.92, line 4 0.97; frames above fog.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated project copy.");
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static float OpacityForLine(int line)
    {
        switch (line)
        {
            case 2: return 0.84f;
            case 3: return 0.92f;
            case 4: return 0.97f;
            default: return 0.55f;
        }
    }
}

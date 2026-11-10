using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bakes the same geometry used at runtime into ordinary, editable scene objects.
[CustomEditor(typeof(Stage))]
public class BattlefieldLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        Stage room = (Stage)target;
        Vector2Int size = room.BattlefieldSize;
        EditorGUILayout.HelpBox(size.x + " columns × " + size.y + " rows. Camera stays upright.", MessageType.Info);
        BattlefieldTilePalette palette = room.TilePalette;
        EditorGUILayout.HelpBox(palette != null && palette.IsConfigured
            ? "Terrain: " + palette.name + ". The palette and every Tile asset remain editable."
            : "No complete terrain palette is assigned; the legacy Tilemap will be preserved.",
            palette != null && palette.IsConfigured ? MessageType.Info : MessageType.Warning);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            if (GUILayout.Button("Apply line size and repaint this room"))
            {
                Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Resize battlefield");
                room.ConfigureBattlefield(room.DefenseLine);
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            }
    }

    [MenuItem("Forest Animal War/Gameplay/Apply portrait layout to all rooms")]
    public static void ApplyToScene()
    {
        if (Application.isPlaying) return;
        var rooms = new Dictionary<int, Stage>();
        foreach (Stage room in UnityEngine.Object.FindObjectsOfType<Stage>(true))
        {
            if (room.gameObject.scene != SceneManager.GetActiveScene()) continue;
            for (int line = 1; line <= 4; line++)
            for (int index = 1; index <= line * 2 - 1; index++)
                if (room.name == RoomName(line, index) || room.name == "Line" + line + "_Room" + index)
                    rooms[DefenseFront.IdFor(line, index)] = room;
        }
        if (!rooms.TryGetValue(2, out Stage template))
            throw new InvalidOperationException("The gameplay scene needs Stage 2_2 as the outer-room template.");

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply portrait battlefields");
        for (int line = 1; line <= 4; line++)
        {
            Transform lineRoot = null;
            for (int index = 1; index <= line * 2 - 1; index++)
                if (rooms.TryGetValue(DefenseFront.IdFor(line, index), out Stage existing)
                    && existing.transform.parent != null) lineRoot = existing.transform.parent;
            for (int index = 1; index <= line * 2 - 1; index++)
            {
                int id = DefenseFront.IdFor(line, index);
                if (!rooms.TryGetValue(id, out Stage room))
                {
                    if (lineRoot == null)
                    {
                        GameObject root = new GameObject("Stage " + line);
                        Undo.RegisterCreatedObjectUndo(root, "Create defense line");
                        lineRoot = root.transform;
                    }
                    GameObject clone = Instantiate(template.gameObject, lineRoot, false);
                    clone.name = RoomName(line, index);
                    Undo.RegisterCreatedObjectUndo(clone, "Create defense room");
                    room = clone.GetComponent<Stage>();
                    clone.transform.position = new Vector3(id * 30f, template.transform.position.y, template.transform.position.z);
                    clone.SetActive(false);
                    rooms[id] = room;
                }
                Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Resize battlefield");
                room.GetComponentInChildren<LevelGenerator>(true)?.ConfigureRoomIdentity(line, index);
                room.ConfigureBattlefield(line);
            }
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a batch-mode project copy.");
        EditorSceneManager.OpenScene("Assets/Asset.unity");
        ApplyToScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Saved 16 portrait battlefields: 8x10, 7x9, 6x8, 5x7.");
    }

    private static string RoomName(int line, int index) => line == 1 ? "Stage 1" : "Stage " + line + "_" + index;
}

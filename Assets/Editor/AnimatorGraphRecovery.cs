using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Editor window graphs are transient ScriptableObjects, separate from controller
// assets. Unity 2022 can retain edges whose nodes no longer belong to a graph.
[InitializeOnLoad]
public static class AnimatorGraphRecovery
{
    private const string Marker = "Library/AnimatorGraphRecovery-20260912.txt";
    private const string WindowType = "UnityEditor.Graphs.AnimatorControllerTool";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static AnimatorGraphRecovery()
    {
        if (Application.isBatchMode || File.Exists(Marker)) return;
        EditorApplication.delayCall += RecoverOnce;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += RecoverOnce;
    }

    private static void RecoverOnce()
    {
        if (File.Exists(Marker) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Repair();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    [MenuItem("Tools/Forest Animal War/Repair Animator Window")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("Animator window recovery is available after leaving Play Mode.");
            return;
        }

        var report = new List<string> { DateTime.UtcNow.ToString("O") };
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>()
            .Where(w => w.GetType().FullName == WindowType).ToArray();
        ScriptableObject[] caches = Resources.FindObjectsOfTypeAll<ScriptableObject>()
            .Where(IsAnimatorWindowCache).ToArray();
        var nodes = new HashSet<Object>();
        foreach (ScriptableObject cache in caches)
        {
            report.Add("Cache: " + cache.GetType().FullName + " #" + cache.GetInstanceID());
            IList cachedNodes = ReadField(cache, "nodes") as IList;
            if (cachedNodes != null)
            {
                foreach (object node in cachedNodes)
                    if (node is Object obj && obj != null && !EditorUtility.IsPersistent(obj)) nodes.Add(obj);
            }
            IList edges = ReadField(cache, "edges") as IList;
            if (edges == null) continue;
            report.Add("Edges: " + edges.Count);
            foreach (object edge in edges)
            {
                Object from = ReadField(edge, "m_FromNode") as Object;
                Object to = ReadField(edge, "m_ToNode") as Object;
                Object owner = from != null ? ReadField(from, "m_Graph") as Object : null;
                report.Add("  " + (from != null ? from.name : "<missing source>")
                    + " -> " + (to != null ? to.name : "<missing destination>")
                    + "; source graph=" + (owner != null ? owner.GetInstanceID().ToString() : "<missing>"));
            }
        }

        // Close only the Animator tab. Preserve scenes, assets, selections and
        // all other tabs. DestroyImmediate never receives allowDestroyingAssets.
        foreach (EditorWindow window in windows) window.Close();
        foreach (ScriptableObject cache in caches)
            if (cache != null && !EditorUtility.IsPersistent(cache)) Object.DestroyImmediate(cache);
        foreach (Object node in nodes)
            if (node != null && !EditorUtility.IsPersistent(node)) Object.DestroyImmediate(node);

        report.Add("Closed Animator windows: " + windows.Length);
        report.Add("Discarded temporary graph objects: " + caches.Length);
        report.Add("Discarded temporary graph nodes: " + nodes.Count);
        report.Add("Controller/clip/scene assets modified: 0");
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/AnimatorGraphRecovery.txt", report);
        File.WriteAllText(Marker, DateTime.UtcNow.ToString("O"));
        Debug.Log("Animator window cache rebuilt. Reopen Window > Animation > Animator when needed. "
            + "Report: Logs/AnimatorGraphRecovery.txt");
    }

    private static bool IsAnimatorWindowCache(ScriptableObject obj)
    {
        if (obj == null || EditorUtility.IsPersistent(obj)) return false;
        string name = obj.GetType().FullName;
        return name == "UnityEditor.Graphs.AnimationStateMachine.Graph"
            || name == "UnityEditor.Graphs.AnimationStateMachine.GraphGUI"
            || name == "UnityEditor.Graphs.AnimationBlendTree.Graph"
            || name == "UnityEditor.Graphs.AnimationBlendTree.GraphGUI";
    }

    private static object ReadField(object obj, string name)
    {
        if (obj == null) return null;
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Fields | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(obj);
        }
        return null;
    }
}

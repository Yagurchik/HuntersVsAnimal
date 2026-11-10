using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnimatorGraphRecoveryValidation
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Run only in an isolated batch project. Exercises the actual Unity graph
    // code that appears in Edge.WakeUp, rather than merely importing YAML.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        var report = new List<string>();
        try
        {
            string[] paths = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var originals = paths.ToDictionary(p => p, File.ReadAllBytes);
            Type graphType = Assembly.Load("UnityEditor.Graphs").GetType(
                "UnityEditor.Graphs.AnimationStateMachine.Graph", true);
            int edgesChecked = 0;
            ScriptableObject corruptGraph = null;
            foreach (string path in paths)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null || controller.layers.Length == 0)
                    throw new Exception("Missing controller/layer: " + path);
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    if (layer.stateMachine == null) throw new Exception("Missing state machine: " + path);
                    ScriptableObject graph = BuildGraph(graphType, layer.stateMachine);
                    IList edges = (IList)FindField(graphType, "edges").GetValue(graph);
                    foreach (object edge in edges)
                    {
                        InvokeWakeUp(edge);
                        edgesChecked++;
                    }
                    if (corruptGraph == null && edges.Count > 0) corruptGraph = graph;
                }
                report.Add("PASS Unity built controller graph: " + path);
            }
            if (corruptGraph == null) throw new Exception("No graph edge available for reproduction.");
            IList corruptEdges = (IList)FindField(graphType, "edges").GetValue(corruptGraph);
            object badEdge = corruptEdges[0];
            object sourceNode = FindField(badEdge.GetType(), "m_FromNode").GetValue(badEdge);
            FindField(sourceNode.GetType(), "m_Graph").SetValue(sourceNode, null);
            bool reproduced = false;
            try { InvokeWakeUp(badEdge); }
            catch (TargetInvocationException error) when (error.InnerException is NullReferenceException)
            {
                reproduced = error.InnerException.StackTrace.Contains("Edge.WakeUp");
            }
            if (!reproduced) throw new Exception("Failed to reproduce stale-node Edge.WakeUp exception.");
            report.Add("PASS Reproduced Edge.WakeUp NullReferenceException with a stale node graph reference");

            AnimatorGraphRecovery.Repair();
            if (corruptGraph != null) throw new Exception("Recovery retained the corrupt graph.");
            report.Add("PASS Recovery removed the corrupt temporary graph");

            foreach (string path in paths)
            {
                if (!originals[path].SequenceEqual(File.ReadAllBytes(path)))
                    throw new Exception("Recovery modified a controller asset: " + path);
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    ScriptableObject graph = BuildGraph(graphType, layer.stateMachine);
                    foreach (object edge in (IList)FindField(graphType, "edges").GetValue(graph)) InvokeWakeUp(edge);
                }
            }
            report.Add("PASS All controller graphs rebuild and wake up after recovery; assets unchanged");
            AnimatorGraphRecovery.Repair();
            report.Add("PASS: " + report.Count + " graph checks; " + edgesChecked + " original edges checked.");
            Finish(report, 0);
        }
        catch (Exception error)
        {
            report.Add("FAIL: " + error);
            Finish(report, 1);
        }
    }

    private static ScriptableObject BuildGraph(Type graphType, AnimatorStateMachine stateMachine)
    {
        ScriptableObject graph = ScriptableObject.CreateInstance(graphType);
        graph.hideFlags = HideFlags.HideAndDontSave;
        graphType.GetMethod("SetStateMachines", Flags).Invoke(graph,
            new object[] { stateMachine, stateMachine, stateMachine });
        graphType.GetMethod("BuildGraphFromStateMachine", Flags).Invoke(graph, new object[] { stateMachine });
        return graph;
    }

    private static void InvokeWakeUp(object edge)
    {
        if (!(bool)edge.GetType().GetMethod("WakeUp", Flags).Invoke(edge, null))
            throw new Exception("An edge failed to reconnect its slots.");
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }

    private static void Finish(List<string> report, int exitCode)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/AnimatorGraphRecoveryValidation.txt", report);
        Debug.Log(report.Last());
        EditorApplication.Exit(exitCode);
    }
}

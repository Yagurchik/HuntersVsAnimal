using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// One-time scene migration. No menu objects are removed or rebuilt at runtime.
public static class ExperienceTabRemovalAuthoring
{
    private const string MenuPath = "Assets/Scenes/UIscene.unity";

    [MenuItem("Forest Animal War/UI/Remove obsolete experience tab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before editing the menu.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MenuPath)
            throw new InvalidOperationException("Open the menu scene first.");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null) throw new InvalidOperationException("Menu tabs are missing.");
        var retained = new List<MenuHubController.Tab>();
        Undo.RecordObject(hub, "Remove experience tab");
        foreach (MenuHubController.Tab tab in hub.tabs)
        {
            if (tab.title != "Опыт" && (tab.page == null || tab.page.name != "Experience Page"))
            {
                retained.Add(tab);
                continue;
            }
            if (tab.page != null) Undo.DestroyObjectImmediate(tab.page);
            if (tab.buttonBackground != null) Undo.DestroyObjectImmediate(tab.buttonBackground.gameObject);
        }
        hub.tabs = retained.ToArray();
        if (hub.tabs.Length != 4) throw new InvalidOperationException("Expected the four remaining menu tabs.");
        float width = 1080f / hub.tabs.Length;
        for (int i = 0; i < hub.tabs.Length; i++)
        {
            MenuHubController.Tab tab = hub.tabs[i];
            RectTransform button = tab.buttonBackground.rectTransform;
            Undo.RecordObject(button, "Resize menu tabs");
            MenuHubBuilder.Top(button, i * width, 0f, width, 154f);
            RectTransform icon = button.Find("Icon") as RectTransform;
            if (icon != null) MenuHubBuilder.Top(icon, 0f, 19f, width, 60f);
            if (tab.buttonLabel != null) MenuHubBuilder.Top(tab.buttonLabel.rectTransform, 0f, 92f, width, 39f);
            if (tab.selectedMark != null) MenuHubBuilder.Top((RectTransform)tab.selectedMark.transform,
                (width - 112f) * 0.5f, 0f, 112f, 5f);
            RectTransform badge = button.Find("Reward Ready") as RectTransform;
            if (badge != null) MenuHubBuilder.Top(badge, width - 54f, 20f, 19f, 19f);
            Button navigation = button.GetComponent<Button>();
            navigation.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(navigation.onClick, hub.SelectTab, i);
            EditorUtility.SetDirty(navigation);
        }
        hub.initialTab = Mathf.Clamp(hub.initialTab, 0, hub.tabs.Length - 1);
        hub.SelectTab(hub.initialTab);
        EditorUtility.SetDirty(hub);
        var scene = hub.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void BakeAndValidateBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new InvalidOperationException("Use an isolated validation project.");
        try
        {
            EditorSceneManager.OpenScene(MenuPath);
            Install();
            // A second pass verifies that repeating the migration is harmless.
            Install();
            EditorSceneManager.OpenScene(MenuPath);
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
            string[] expected = { "Кампания", "Артефакты", "Деревня", "События" };
            Require(hub.tabs.Select(tab => tab.title).SequenceEqual(expected), "Only the four retained tabs are saved");
            for (int i = 0; i < hub.tabs.Length; i++)
            {
                MenuHubController.Tab tab = hub.tabs[i];
                Require(tab.page != null && tab.buttonBackground != null && tab.buttonLabel != null,
                    "Remaining page and button references are preserved");
                RectTransform rect = tab.buttonBackground.rectTransform;
                Require(Mathf.Approximately(rect.sizeDelta.x, 270f)
                    && Mathf.Approximately(rect.anchoredPosition.x, i * 270f), "Navigation fills the bar without gaps");
                Button button = rect.GetComponent<Button>();
                Require(button.onClick.GetPersistentEventCount() == 1
                    && button.onClick.GetPersistentTarget(0) == hub
                    && button.onClick.GetPersistentMethodName(0) == "SelectTab", "Remaining navigation keeps one saved callback");
                hub.SelectTab(i);
                Require(hub.tabs.Where((value, index) => value.page.activeSelf != (index == i)).Count() == 0,
                    "Every remaining tab opens exactly its own page");
            }
            foreach (Transform child in hub.GetComponentsInChildren<Transform>(true))
                Require(child.name != "Experience Page"
                    && child.name != "Опыт Tab", "Obsolete experience objects are absent");
            string savedScene = File.ReadAllText(MenuPath);
            Require(!savedScene.Contains("eaf5e6b6a98e68b4981e715a3244e353")
                && !savedScene.Contains("42c3db7182fe49e8b36a5ccb45a92d01")
                && !savedScene.Contains("PermanentExperience"), "Saved scene contains no removed scripts or purchase callbacks");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ExperienceTabRemovalValidation.txt",
                "PASS: four editable tabs; uniform 270px navigation; working page selection; "
                + "experience hierarchy, scripts and purchases removed; repeated migration remains safe.");
            Debug.Log("Experience tab removal validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

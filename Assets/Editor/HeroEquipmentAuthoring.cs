using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HeroEquipmentAuthoring
{
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Batch mode required.");
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        BakeOpenScenes();
        EditorApplication.Exit(0);
    }

    public static bool HasCurrentLayout(MenuHubController hub)
    {
        Transform content = Content(hub);
        return content != null && content.Find("Equipment Slot 7") == null
            && content.Cast<Transform>().Count(t => t.name.StartsWith("Equipment Slot ")) == 6
            && Enumerable.Range(1, 6).All(i => content.Find("Equipment Slot " + i)
                ?.GetComponent<ArtifactCellView>()?.IsCurrent == true)
            && content.GetComponentsInChildren<ArtifactCellView>(true).Length == 66
            && content.GetComponentsInChildren<ArtifactCellView>(true).All(v => v.IsCurrent);
    }

    private static Transform Content(MenuHubController hub) => hub.tabs[1].page.transform
        .Find("Hero Equipment Runtime/Viewport/Content");

    public static bool Synchronize(MenuHubController hub)
    {
        if (HasCurrentLayout(hub)) return false;
        Transform content = Content(hub);
        if (content == null) return false;
        Undo.RegisterFullObjectHierarchyUndo(hub.gameObject, "Six artifact cells");
        foreach (Transform child in content.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Equipment Slot ")
                && int.TryParse(child.name.Substring(15), out int slot) && slot > 6)
                Object.DestroyImmediate(child.gameObject);
        for (int i = 0; i < MetaProgression.EquipmentSlotCount; i++)
        {
            Transform cell = content.Find("Equipment Slot " + (i + 1));
            RectTransform rect = (RectTransform)cell;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(i < 3 ? -416f : 416f, -214f - (i % 3) * 208f);
            rect.sizeDelta = new Vector2(200f, 200f);
            ArtifactCellView.Author(cell.GetComponent<Button>(), true, i + 1);
        }
        for (int i = 0; i < 60; i++)
            ArtifactCellView.Author(content.Find("Backpack Cell " + (i + 1)).GetComponent<Button>(), false, i + 1);
        hub.RebindEquipmentForAuthoring();
        EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        return true;
    }

    [MenuItem("Forest Animal War/UI/Update six artifact cells")]
    public static void BakeOpenScenes()
    {
        foreach (MenuHubController hub in Object.FindObjectsOfType<MenuHubController>(true))
            if (Synchronize(hub)) EditorSceneManager.SaveScene(hub.gameObject.scene);
    }
}

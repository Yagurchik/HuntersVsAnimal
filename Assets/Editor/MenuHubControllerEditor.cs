using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MenuHubController))]
public class MenuHubControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var hub = (MenuHubController)target;
        if (GUILayout.Button("Обзор всех экранов игры")) GameUIPreviewWindow.Open();
        EditorGUILayout.HelpBox("Постройки деревни — отдельные префабы в Resources/Village. Вкладка Артефакты показывает Тыкву, экипировку и рюкзак. Предпросмотр не меняет прогресс.", MessageType.Info);
        if (hub.tabs != null)
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < hub.tabs.Length; i++)
                if (GUILayout.Button(hub.tabs[i].title)) { Record(hub); hub.PrepareVillageAndHero(); hub.SelectTab(i); Dirty(hub); }
            EditorGUILayout.EndHorizontal();
        }
        if (!Application.isPlaying)
        {
            if (GUILayout.Button("Сохранить деревню и артефакты в сцену"))
                VillageBuildingAuthoring.BakeEditableInterface();
            if (GUILayout.Button("Обновить данные карточек")) { Record(hub); hub.Refresh(); Dirty(hub); }
            if (GUILayout.Button("Предпросмотр: награда")) { Record(hub); hub.ShowInformation("Награда получена", "+15 желудей\n\nВерный защитник"); Dirty(hub); }
            if (GUILayout.Button("Предпросмотр: настройки")) { Record(hub); hub.ShowSettings(); Dirty(hub); }
            if (GUILayout.Button("Закрыть окна")) { Record(hub); hub.CloseDialogs(); Dirty(hub); }
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
    private static void Record(MenuHubController hub) => Undo.RegisterFullObjectHierarchyUndo(hub.gameObject, "Preview menu UI");
    private static void Dirty(MenuHubController hub)
    {
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
    }
}

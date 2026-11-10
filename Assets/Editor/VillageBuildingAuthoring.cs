using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;

public static class VillageBuildingAuthoring
{
    private const string Folder = "Assets/Resources/Village";

    [MenuItem("Forest Animal War/UI/Create village entity window prefabs")]
    public static void Install()
    {
        VillageEntityWindowAuthoring.EnsurePrefabs();
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated project copy.");
        try
        {
            Install();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
            VillageEntityWindowAuthoring.InstallIntoMenu(UnityEngine.Object.FindObjectOfType<MenuHubController>(true));
            MenuHubBuilder.EnsureResourceBar(UnityEngine.Object.FindObjectOfType<MenuHubController>(true));
            MenuHubValidation.Validate();
            var hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
            Render(hub, 2, "VillageMap");
            hub.tabs[2].page.GetComponentInChildren<VillageBuildingView>().Open();
            Render(hub, -1, "BuildingMenu");
            hub.CloseDialogs();
            foreach (var building in hub.tabs[2].page.GetComponentsInChildren<VillageBuildingView>())
                if (building.kind == VillageBuildingKind.ForestShop) building.Open();
            Render(hub, -1, "ForestShopMenu");
            hub.CloseDialogs();
            Render(hub, 1, "HeroEquipment");
            // Persist the full editable hierarchy. Runtime only reconnects
            // behaviour to these authored objects and never rebuilds the UI.
            hub.CloseDialogs();
            hub.SelectTab(0);
            Transform oldUpgrade = hub.transform.Find("Safe Area/Portrait Frame/Dialogs/Upgrade Dialog");
            if (oldUpgrade != null) UnityEngine.Object.DestroyImmediate(oldUpgrade.gameObject);
            hub.tabs[0].page.SetActive(true);
            hub.tabs[1].page.SetActive(false);
            hub.tabs[2].page.SetActive(false);
            hub.tabs[3].page.SetActive(false);
            Text help = hub.settingsModal != null
                ? hub.settingsModal.transform.Find("Panel/Help")?.GetComponent<Text>() : null;
            if (help != null) help.text = "Развивай деревню, надевай артефакты и защищай лес.\n\nБонусы дают только надетые предметы.";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(hub.gameObject.scene);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Forest Animal War/UI/Bake editable village and artifacts into UIscene")]
    public static void BakeEditableInterface()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before baking the menu.");
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != "Assets/Scenes/UIscene.unity")
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        Install();
        var hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null) throw new InvalidOperationException("MenuHubController is missing from UIscene.");
        VillageEntityWindowAuthoring.InstallIntoMenu(hub);
        MenuHubBuilder.EnsureResourceBar(hub);
        hub.RebindVillageForAuthoring();
        hub.CloseDialogs();
        hub.SelectTab(0);
        EditorUtility.SetDirty(hub);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(hub.gameObject.scene);
        Selection.activeGameObject = hub.gameObject;
        Debug.Log("Editable village and artifact hierarchy baked into Assets/Scenes/UIscene.unity.");
    }

    private static void Render(MenuHubController hub, int tab, string file)
    {
        if (tab >= 0) hub.SelectTab(tab);
        var canvas = hub.GetComponent<Canvas>();
        var cameraObject = new GameObject("Village validation camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(1080, 1920, 24);
        camera.targetTexture = target;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(1080, 1920, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/" + file + ".png", image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}

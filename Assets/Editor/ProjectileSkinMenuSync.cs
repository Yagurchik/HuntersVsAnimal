using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Asset imports do not replace a menu that is already loaded in the editor.
// Migrate the complete authored village, before Unity backs it up for Play Mode.
// Never install a single new entrance over the legacy house map.
[InitializeOnLoad]
public static class ProjectileSkinMenuSync
{
    private const string MenuPath = "Assets/Scenes/UIscene.unity";

    static ProjectileSkinMenuSync()
    {
        if (Application.isBatchMode) return;
        QueueSync();
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => QueueSync();

    private static void QueueSync()
    {
        EditorApplication.update -= SyncWhenReady;
        EditorApplication.update += SyncWhenReady;
    }

    private static void SyncWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating) return;
        EditorApplication.update -= SyncWhenReady;
        SyncOpenMenus();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
            SyncOpenMenus();
    }

    public static void SyncOpenMenus()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            var report = new System.Collections.Generic.List<string> { DateTime.UtcNow.ToString("O") };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != MenuPath) continue;
                bool wasDirty = scene.isDirty;
                bool changed = false;
                foreach (var hub in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MenuHubController>(true)))
                {
                    changed |= ProjectileSkinAuthoring.InstallIntoMenu(hub);
                    changed |= HeroEquipmentAuthoring.Synchronize(hub);
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    // Keep unsaved user edits unsaved. A clean scene can be saved immediately.
                    if (!wasDirty) EditorSceneManager.SaveScene(scene);
                    Debug.Log("Complete village layout synchronized in the open menu scene.");
                }
                var shops = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ProjectileSkinShopUI>(true)).ToArray();
                report.Add(scene.path + ": shops=" + shops.Length + ", installed=" + changed
                    + ", previouslyDirty=" + wasDirty + ", dirty=" + scene.isDirty);
                foreach (var hub in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuHubController>(true)))
                    report.Add("Complete entity layout=" + VillageEntityWindowAuthoring.HasCurrentLayout(hub));
                foreach (var shop in shops)
                    report.Add("Shop cards=" + shop.Cards.Length + ", prefab="
                        + PrefabUtility.IsPartOfPrefabInstance(shop));
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/ProjectileSkinMenuSync.txt", report);
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Use an isolated validation project.");
        try
        {
            var scene = EditorSceneManager.OpenScene(MenuPath);
            var hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
            Transform page = hub.tabs[2].page.transform;
            Transform field = page.Find("Forest Village Runtime/Viewport/Content/Village Field");
            string[] prefabPaths = VillageEntityWindowAuthoring.PrefabNames
                .Select(name => VillageEntityWindowAuthoring.PrefabFolder + "/" + name + ".prefab").ToArray();
            byte[][] prefabBefore = prefabPaths.Select(File.ReadAllBytes).ToArray();
            const string entrance = "Forest Village Runtime/Viewport/Content/Village Field/Open Skin Shop";
            Action removeShop = () => {
                UnityEngine.Object.DestroyImmediate(page.Find("Projectile Skin Shop").gameObject);
                UnityEngine.Object.DestroyImmediate(page.Find(entrance).gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            };
            removeShop();
            // Reproduce the reported hybrid: a new entrance coexists with an
            // old clearing/house hierarchy in a menu already loaded in memory.
            if (field.Find("Clearing Background") == null)
            {
                var oldMap = new GameObject("Clearing Background", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                oldMap.transform.SetParent(field, false);
            }
            EditorSceneManager.SaveScene(scene);
            SyncOpenMenus();
            if (page.Find(entrance) == null || page.Find("Projectile Skin Shop") == null || scene.isDirty)
                throw new Exception("Clean stale menu was not installed and saved.");
            if (!VillageEntityWindowAuthoring.HasCurrentLayout(hub))
                throw new Exception("Only one entrance was repaired; the saved village is still mixed.");
            if (ProjectileSkinAuthoring.InstallIntoMenu(hub))
                throw new Exception("Installation is not idempotent.");
            hub.PrepareVillageAndHero();
            removeShop();
            var obsoleteRaven = field.Find("ForestShopBuilding");
            UnityEngine.Object.DestroyImmediate(obsoleteRaven.gameObject);
            var oldHouse = new GameObject("ForestShopBuilding", typeof(RectTransform), typeof(VillageBuildingView));
            oldHouse.transform.SetParent(field, false);
            var oldArtwork = new GameObject("Building Artwork", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            oldArtwork.transform.SetParent(oldHouse.transform, false);
            var duplicateTree = new GameObject("GreatTreeBuilding", typeof(RectTransform));
            duplicateTree.transform.SetParent(field, false);
            var note = new GameObject("Unsaved author edit");
            SceneManager.MoveGameObjectToScene(note, scene);
            SyncOpenMenus();
            if (page.Find(entrance) == null || page.Find("Projectile Skin Shop") == null
                || !scene.isDirty || note == null || File.ReadAllText(MenuPath).Contains("Unsaved author edit"))
                throw new Exception("Dirty scene edits were lost or saved without being requested.");
            if (!VillageEntityWindowAuthoring.HasCurrentLayout(hub))
                throw new Exception("A legacy house survived complete village sync.");
            if (oldHouse != null || duplicateTree != null)
                throw new Exception("A legacy house or duplicate service survived migration.");
            hub.SelectTab(2);
            field.Find("ForestShopBuilding").GetComponent<VillageBuildingView>().openButton.onClick.Invoke();
            if (!hub.IsDialogOpen || !page.Find("Building Menu/Panel/Shop Offers").gameObject.activeSelf)
                throw new Exception("The loaded controller is still bound to removed houses.");
            hub.CloseDialogs();
            if (!PrefabUtility.IsPartOfPrefabInstance(page.Find("Projectile Skin Shop").gameObject))
                throw new Exception("Shop is not an editable prefab instance.");
            for (int index = 0; index < prefabPaths.Length; index++)
                if (!prefabBefore[index].SequenceEqual(File.ReadAllBytes(prefabPaths[index])))
                    throw new Exception("Automatic scene sync rewrote an authored entity prefab.");
            UnityEngine.Object.DestroyImmediate(note);
            EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(MenuPath);
            hub = UnityEngine.Object.FindObjectOfType<MenuHubController>(true);
            if (!VillageEntityWindowAuthoring.HasCurrentLayout(hub)
                || ProjectileSkinAuthoring.InstallIntoMenu(hub) || scene.isDirty)
                throw new Exception("The complete layout did not persist across a cold scene reload.");
            File.WriteAllText("Logs/ProjectileSkinMenuSyncValidation.txt",
                "PASS: stale clean scene migrates all four windows and removes the old clearing\n"
                + "PASS: repeated installation creates no duplicates\n"
                + "PASS: unsaved scene edits remain intact and unsaved\n"
                + "PASS: an obsolete house is replaced and the loaded controller is rebound\n"
                + "PASS: automatic sync leaves all four authored prefab assets byte-identical\n"
                + "PASS: complete layout survives cold reopening with no further migration\n"
                + "PASS: shop remains an authored editable prefab instance\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

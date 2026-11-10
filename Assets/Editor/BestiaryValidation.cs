using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BestiaryValidation
{
    public static void RunBatch()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
            MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
            if (hub == null) throw new Exception("Menu hub is missing.");
            Transform page = hub.tabs[2].page.transform;
            Transform open = page.Find("Forest Village Runtime/Viewport/Content/Village Field/Open Bestiary");
            BestiaryUI bestiary = page.Find("Bestiary Menu")?.GetComponent<BestiaryUI>();
            if (open == null || bestiary == null)
                throw new Exception("Editable bestiary button or panel is missing.");

            BestiaryEntryView[] entries = bestiary.GetComponentsInChildren<BestiaryEntryView>(true);
            CheckCount(entries, BestiaryEntryView.Section.Locations, 6);
            CheckCount(entries, BestiaryEntryView.Section.Enemies, 63);
            CheckCount(entries, BestiaryEntryView.Section.Projectiles, 8);
            if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.Title)
                || string.IsNullOrWhiteSpace(entry.Body) || entry.Portrait == null))
                throw new Exception("A bestiary card has missing text or artwork.");

            hub.PrepareVillageAndHero();
            hub.SelectTab(2);
            open.GetComponent<Button>().onClick.Invoke();
            if (!bestiary.IsOpen || !hub.IsDialogOpen)
                throw new Exception("Village bestiary button did not open the panel.");
            Transform panel = bestiary.transform.Find("Panel");
            Text detailTitle = panel.Find("Detail/Title").GetComponent<Text>();
            if (detailTitle.text != LevelProgression.LocationNameForIndex(1))
                throw new Exception("Location list did not select its first card.");
            Capture(hub, "Bestiary-Locations.png");
            panel.Find("Category Tabs/Enemies").GetComponent<Button>().onClick.Invoke();
            if (detailTitle.text != "Лесной скелет")
                throw new Exception("Enemy section did not show the forest skeleton.");
            Capture(hub, "Bestiary-Enemies.png");
            panel.Find("Category Tabs/Projectiles").GetComponent<Button>().onClick.Invoke();
            if (detailTitle.text != "Светлячок")
                throw new Exception("Projectile section did not show the firefly.");
            Capture(hub, "Bestiary-Projectiles.png");
            panel.Find("Close").GetComponent<Button>().onClick.Invoke();
            if (bestiary.IsOpen || hub.IsDialogOpen)
                throw new Exception("Bestiary close button did not dismiss the panel.");
            Capture(hub, "Bestiary-Village.png");

            Debug.Log("BestiaryValidation passed: village button, three sections, 77 authored cards, portraits and close controls.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckCount(BestiaryEntryView[] entries, BestiaryEntryView.Section category, int expected)
    {
        int count = entries.Count(entry => entry.Category == category);
        if (count != expected)
            throw new Exception(category + ": expected " + expected + " cards, got " + count + ".");
    }

    private static void Capture(MenuHubController hub, string name)
    {
        Canvas canvas = hub.GetComponent<Canvas>();
        GameObject cameraObject = new GameObject("Bestiary validation camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        RenderTexture target = new RenderTexture(1080, 1920, 24);
        camera.targetTexture = target;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = VillageUIStyle.Background;
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1080, 1920, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/" + name, image.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCamera;
        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(cameraObject);
    }
}

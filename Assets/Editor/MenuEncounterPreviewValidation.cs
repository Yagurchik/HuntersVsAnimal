using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MenuEncounterPreviewValidation
{
    public static void RunBatch()
    {
        if (!Application.isBatchMode || PlayerSettings.companyName != "CodexValidation")
            throw new InvalidOperationException("Run only in an isolated project copy.");
        try
        {
            Validate();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Validate()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        Require(hub != null && hub.encounterPreviews != null
            && hub.encounterPreviews.Length == LevelProgression.MaxLevel,
            "All 30 levels have an authored enemy preview.");
        Require(hub.levels != null && hub.levels.Length >= 5,
            "Five editable level cards are present.");
        int icons = 0;
        for (int location = 1; location <= LevelProgression.LocationCount; location++)
        for (int local = 1; local <= LevelProgression.LevelsPerLocation; local++)
        {
            int global = LevelProgression.GlobalLevelFor(location, local);
            MenuHubController.LevelEncounterPreview preview = hub.EncounterPreviewFor(global);
            Require(preview != null && preview.enemies != null
                && preview.enemies.Length == 8,
                "Level " + global + " has five ordinary types and three distinct bosses.");
            for (int i = 0; i < preview.enemies.Length; i++)
            {
                GameObject enemy = preview.enemies[i];
                Require(enemy != null && Array.IndexOf(preview.enemies, enemy) == i,
                    "Level " + global + " has unique valid enemy references.");
                EnemyFirstEncounterProfile profile = enemy.GetComponent<EnemyFirstEncounterProfile>();
                Transform visual = enemy.transform.Find("Visual");
                SpriteRenderer renderer = visual != null
                    ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
                Require(profile?.Portrait != null || renderer?.sprite != null,
                    "Level " + global + " enemy " + i + " has portrait art.");
                Require(enemy.GetComponent<Brick>().UsesBossHealth == (i >= 5),
                    "Level " + global + " shows ordinary types before bosses.");
                icons++;
            }
            Require(preview.enemies[5] != preview.enemies[6]
                && preview.enemies[5] != preview.enemies[7]
                && preview.enemies[6] != preview.enemies[7],
                "Level " + global + " does not repeat a boss portrait.");
            MenuLevelCard card = Array.Find(hub.levels,
                candidate => candidate != null && candidate.levelNumber == local);
            Require(card != null && card.transform.Find("Enemy Portraits") == null
                && Mathf.Approximately(((RectTransform)card.transform).sizeDelta.y, 119f),
                "Level " + local + " remains a compact selection button.");
            Require(hub.encounterPortraits?.Length == 8
                && hub.encounterFrames?.Length == 8
                && hub.encounterNames?.Length == 8,
                "The location illustration has eight authored portrait slots.");
            hub.RefreshEncounterPreview(global);
            Require(hub.encounterTitle.text.EndsWith(local.ToString("00")),
                "The location preview shows the selected level number.");
            for (int i = 0; i < hub.encounterPortraits.Length; i++)
            {
                Image image = hub.encounterPortraits[i];
                bool expected = i < preview.enemies.Length;
                Require(image != null && image.gameObject.activeSelf == expected
                    && (image.sprite != null) == expected
                    && hub.encounterFrames[i].gameObject.activeSelf == expected,
                    "Location art switches images for level " + global + ".");
                if (expected)
                    Require(hub.encounterNames[i].text == preview.enemies[i]
                        .GetComponent<EnemyFirstEncounterProfile>().DisplayName,
                        "Location art shows the enemy name for level " + global + ".");
            }
        }
        Transform hero = hub.tabs[0].page.transform.Find("Viewport/Content/Forest Hero");
        RectTransform overlay = hero != null
            ? hero.Find("Level Enemy Preview") as RectTransform : null;
        Require(overlay != null && overlay.anchoredPosition.y == -235f
            && overlay.sizeDelta.y == 178f,
            "Enemy portraits sit on the location illustration above its caption.");
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/UI/MenuPrefabs/LevelCard.prefab");
        MenuLevelCard templateCard = template != null ? template.GetComponent<MenuLevelCard>() : null;
        Require(templateCard != null && templateCard.transform.Find("Enemy Portraits") == null
            && Mathf.Approximately(((RectTransform)templateCard.transform).sizeDelta.y, 119f),
            "Reusable level card prefab keeps its compact layout.");
        hub.SelectTab(0);
        hub.RefreshEncounterPreview(1);
        Capture(hub);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/MenuEncounterPreviewValidation.txt",
            "PASS: 30 level rosters, " + icons
            + " portrait references on the location illustration, compact level cards.");
        Debug.Log("MENU_ENCOUNTER_PREVIEWS_PASSED: " + icons + " portrait references.");
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private static void Capture(MenuHubController hub)
    {
        const int width = 1080;
        const int height = 1920;
        Canvas canvas = hub.GetComponent<Canvas>();
        var cameraObject = new GameObject("Encounter preview validation camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(width, height, 24);
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/MenuEncounterPreview.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally
        {
            RenderTexture.active = oldActive;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }
}

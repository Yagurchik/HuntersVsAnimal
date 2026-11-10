using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class LocationExteriorAuthoring
{
    private static readonly string[] ArtworkPaths = {
        "Assets/LocationArt/TwilightThicket/TwilightExterior.png",
        "Assets/LocationArt/GloomyHollow/GloomyHollowBattlefield.png",
        "Assets/LocationArt/DrownedBog/DrownedBogBattlefield.png",
        "Assets/LocationArt/ForgottenBay/ForgottenBayBattlefield.png",
        "Assets/LocationArt/SnowyHaze/SnowyHazeBattlefield.png",
        "Assets/LocationArt/FieryGehenna/GehennaBattlefield.png"
    };
    private static readonly Color[] FrameTints = {
        Color.white, Color.white, Color.white,
        Color.white, Color.white, Color.white
    };
    private static readonly Color[] MarginColors = {
        new Color(0.06f, 0.13f, 0.08f, 0.28f),
        new Color(0.12f, 0.08f, 0.17f, 0.32f),
        new Color(0.13f, 0.16f, 0.07f, 0.32f),
        new Color(0.04f, 0.16f, 0.19f, 0.30f),
        new Color(0.21f, 0.28f, 0.34f, 0.30f),
        new Color(0.23f, 0.07f, 0.05f, 0.33f)
    };
    private static readonly Color[] EdgeColors = {
        new Color(0.02f, 0.06f, 0.04f, 0.75f),
        new Color(0.04f, 0.03f, 0.07f, 0.78f),
        new Color(0.05f, 0.07f, 0.04f, 0.78f),
        new Color(0.02f, 0.07f, 0.09f, 0.78f),
        new Color(0.11f, 0.17f, 0.23f, 0.78f),
        new Color(0.11f, 0.025f, 0.025f, 0.80f)
    };
    private static readonly Color[] OrnamentColors = {
        new Color(0.74f, 0.58f, 0.25f, 0.48f),
        new Color(0.58f, 0.44f, 0.79f, 0.45f),
        new Color(0.56f, 0.65f, 0.27f, 0.40f),
        new Color(0.31f, 0.70f, 0.69f, 0.44f),
        new Color(0.76f, 0.88f, 0.98f, 0.48f),
        new Color(0.95f, 0.42f, 0.19f, 0.54f)
    };
    private static readonly Color[] AccentColors = {
        new Color(0.36f, 0.55f, 0.29f, 0.44f),
        new Color(0.44f, 0.36f, 0.57f, 0.44f),
        new Color(0.37f, 0.48f, 0.22f, 0.44f),
        new Color(0.20f, 0.48f, 0.51f, 0.44f),
        new Color(0.55f, 0.70f, 0.83f, 0.44f),
        new Color(0.60f, 0.24f, 0.17f, 0.44f)
    };

    [MenuItem("Tools/Forest Animal War/Locations/Bake location exteriors")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before baking exteriors.");
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.OpenScene("Assets/Asset.unity", OpenSceneMode.Single);
        GameplayBackdropUI backdrop = Object.FindObjectOfType<GameplayBackdropUI>(true);
        if (backdrop == null)
            throw new InvalidOperationException("Authored gameplay backdrop is missing.");
        Transform pattern = backdrop.transform.Find("Pumpkin And Vine Pattern");
        if (pattern == null)
            throw new InvalidOperationException("Authored margin pattern is missing.");
        Transform exterior = backdrop.transform.Find("Location Exterior");
        if (exterior == null)
        {
            GameObject art = new GameObject("Location Exterior", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(LocationBackdropGraphic));
            exterior = art.transform;
            exterior.SetParent(backdrop.transform, false);
        }
        RectTransform rect = (RectTransform)exterior;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(pattern.GetSiblingIndex());
        LocationBackdropGraphic graphic = exterior.GetComponent<LocationBackdropGraphic>();
        graphic.raycastTarget = false;
        SerializedObject exteriorData = new SerializedObject(graphic);
        SerializedProperty images = exteriorData.FindProperty("locationArtwork");
        images.arraySize = ArtworkPaths.Length;
        for (int i = 0; i < ArtworkPaths.Length; i++)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPaths[i]);
            if (texture == null)
                throw new InvalidOperationException("Missing exterior art: " + ArtworkPaths[i]);
            images.GetArrayElementAtIndex(i).objectReferenceValue = texture;
        }
        exteriorData.FindProperty("opacity").floatValue = 0.88f;
        exteriorData.ApplyModifiedPropertiesWithoutUndo();

        PumpkinBackdropGraphic ornaments = pattern.GetComponent<PumpkinBackdropGraphic>();
        if (ornaments == null)
            throw new InvalidOperationException("Authored ornament graphic is missing.");
        for (int i = 0; i < 6; i++)
            ornaments.SetPalette(i + 1, MarginColors[i], EdgeColors[i],
                OrnamentColors[i], AccentColors[i]);
        ornaments.ShowLocation(1);
        // Store a useful editor preview as well; runtime refines this cut-out
        // against the currently focused room on each device and during pans.
        Camera camera = Camera.main;
        Stage previewRoom = Object.FindObjectsOfType<Stage>(true)
            .FirstOrDefault(stage => stage.name == "Stage 1");
        if (camera != null && previewRoom != null)
        {
            camera.GetComponent<CameraRenderer>()?.FrameRoom(previewRoom);
            Bounds bounds = previewRoom.GetViewBounds();
            Vector3 lower = camera.WorldToViewportPoint(bounds.min);
            Vector3 upper = camera.WorldToViewportPoint(bounds.max);
            Vector2 min = new Vector2(Mathf.Clamp01(lower.x + 0.002f),
                Mathf.Clamp01(lower.y + 0.002f));
            Vector2 max = new Vector2(Mathf.Clamp01(upper.x - 0.002f),
                Mathf.Clamp01(upper.y - 0.002f));
            graphic.SetBoardWindow(min, max);
            ornaments.SetBoardWindow(min, max);
        }
        EditorUtility.SetDirty(ornaments);
        EditorUtility.SetDirty(graphic);

        int stageCount = 0;
        foreach (Stage stage in Object.FindObjectsOfType<Stage>(true))
        {
            DrownedBogVisuals visuals = stage.GetComponent<DrownedBogVisuals>();
            if (visuals == null)
                throw new InvalidOperationException("Stage " + stage.name + " has no visual switcher.");
            SerializedObject data = new SerializedObject(visuals);
            foreach (string field in new[] { "ordinaryTerrainTint", "gloomyTerrainTint",
                "bogTerrainTint", "bayTerrainTint", "snowTerrainTint",
                "gehennaTerrainTint" })
                data.FindProperty(field).colorValue = Color.white;
            SerializedProperty tints = data.FindProperty("locationFrameTints");
            tints.arraySize = FrameTints.Length;
            for (int i = 0; i < FrameTints.Length; i++)
                tints.GetArrayElementAtIndex(i).colorValue = FrameTints[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visuals);
            stageCount++;
        }
        if (stageCount != 16)
            throw new InvalidOperationException("Expected 16 stages, found " + stageCount);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Six exterior location themes and board-aligned margins saved in editable gameplay scene.");
    }

    public static void BakeBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

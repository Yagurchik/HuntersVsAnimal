using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Creates editable UI prefabs from the approved generated frame art.</summary>
public static class EnemyHealthBarPrefabAuthoring
{
    private const string Folder = "Assets/Resources/UI/EnemyHealthBars/";
    private const string ComicFontPath =
        "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    private const string ComicOutlinePath = Folder + "EnemyHpComicOutline.mat";

    [InitializeOnLoadMethod]
    private static void EnsurePrefabsAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool needRegular = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RegularEnemyHealthBar.prefab") == null;
            bool needBoss = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "BossEnemyHealthBar.prefab") == null;
            if (needRegular || needBoss) CreatePrefabs(needRegular, needBoss);
        };
    }

    [MenuItem("Tools/Forest Animal War/Create Enemy Health Bar Prefabs")]
    private static void CreateMissingPrefabs()
    {
        CreatePrefabs(true, true);
    }

    private static void CreatePrefabs(bool regular, bool boss)
    {
        if (regular) CreatePrefab("RegularEnemyHealthBar", "RegularFrame.png", 90f, 34f,
            74f, 21f, 21f, new Color32(115, 184, 52, 255),
            new Rect(0f, 0.280f, 1f, 0.447f));
        if (boss) CreatePrefab("BossEnemyHealthBar", "BossFrame.png", 180f, 42f,
            148f, 26f, 25f, new Color32(177, 110, 205, 255),
            new Rect(0f, 0.212f, 1f, 0.614f));
        AssetDatabase.SaveAssets();
    }

    private static void CreatePrefab(string name, string textureName, float width, float height,
        float trackWidth, float trackHeight, float fontSize, Color fillColor, Rect frameUv)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + textureName);
        TMP_FontAsset comicFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ComicFontPath);
        Material comicOutline = AssetDatabase.LoadAssetAtPath<Material>(ComicOutlinePath);
        if (texture == null || comicFont == null || comicOutline == null)
        {
            Debug.LogError("Missing enemy health bar frame, comic font, or outline material for " + name);
            return;
        }

        GameObject root = new GameObject(name, typeof(RectTransform), typeof(EnemyHealthBarView));
        root.layer = 5;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(width, height);

        Image missing = AddImage(root.transform, "Dark Track", trackWidth, trackHeight,
            new Color32(190, 43, 47, 255));
        Image trail = AddImage(root.transform, "Damage Trail", trackWidth, trackHeight,
            new Color32(235, 94, 81, 255));
        Image fill = AddImage(root.transform, "Health Fill", trackWidth, trackHeight, fillColor);

        GameObject frameObject = new GameObject("Generated Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        frameObject.layer = 5;
        frameObject.transform.SetParent(root.transform, false);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(width, height);
        RawImage frame = frameObject.GetComponent<RawImage>();
        frame.texture = texture;
        frame.uvRect = frameUv;
        frame.raycastTarget = false;

        GameObject numberObject = new GameObject("HP Number", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        numberObject.layer = 5;
        numberObject.transform.SetParent(root.transform, false);
        RectTransform numberRect = numberObject.GetComponent<RectTransform>();
        numberRect.anchorMin = numberRect.anchorMax = numberRect.pivot = new Vector2(0.5f, 0.5f);
        numberRect.sizeDelta = new Vector2(trackWidth, height);
        TextMeshProUGUI number = numberObject.GetComponent<TextMeshProUGUI>();
        number.font = comicFont;
        number.fontSharedMaterial = comicOutline;
        number.text = "100";
        number.fontSize = fontSize;
        number.fontStyle = FontStyles.Bold;
        number.alignment = TextAlignmentOptions.Center;
        number.enableWordWrapping = false;
        number.overflowMode = TextOverflowModes.Overflow;
        number.color = new Color32(250, 244, 216, 255);
        number.raycastTarget = false;
        number.extraPadding = true;

        root.GetComponent<EnemyHealthBarView>().Configure(fill, missing, trail, number);
        PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    private static Image AddImage(Transform parent, string name, float width, float height, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.layer = 5;
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}

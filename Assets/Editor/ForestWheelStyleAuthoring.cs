using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Saves the wheel's decorative elements in the scene, so they remain editable.
public static class ForestWheelStyleAuthoring
{
    private const string ScenePath = "Assets/Asset.unity";
    private const string PanelPath = "Assets/UI/MenuStyle/WoodcutPanel.png";
    private const string WheelArtPath = "Assets/UI/WheelStyle/ForestWheelRing.png";

    [MenuItem("Forest Animal War/UI/Bake forest projectile wheel")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before editing the projectile wheel.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AnimalSelectionWheel wheel = UnityEngine.Object.FindObjectOfType<AnimalSelectionWheel>(true);
        AnimalProjectileQueue queue = UnityEngine.Object.FindObjectOfType<AnimalProjectileQueue>(true);
        Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        Sprite ringArt = AssetDatabase.LoadAssetAtPath<Sprite>(WheelArtPath);
        if (wheel == null || queue == null || panel == null || ringArt == null)
            throw new InvalidOperationException("The authored wheel, queue or forest artwork is missing.");

        EnsureArtwork(wheel, ringArt);
        wheel.Preview(queue, true);
        Transform toggle = wheel.AllVolleyButton != null ? wheel.AllVolleyButton.transform : null;
        Image background = toggle != null ? toggle.GetComponent<Image>() : null;
        if (background == null)
            throw new InvalidOperationException("The combined-volley button is missing from the wheel.");
        background.sprite = panel;
        background.type = Image.Type.Sliced;
        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(wheel);

        Validate(wheel, panel, ringArt);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        wheel = UnityEngine.Object.FindObjectOfType<AnimalSelectionWheel>(true);
        Validate(wheel, panel, ringArt);
        Debug.Log("Forest projectile wheel saved in Assets/Asset.unity.");
    }

    public static void BakeBatch()
    {
        try
        {
            Bake();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureArtwork(AnimalSelectionWheel wheel, Sprite ringArt)
    {
        Transform artwork = wheel.transform.Find("Forest Wheel Artwork");
        if (artwork == null)
        {
            GameObject objectInScene = new GameObject("Forest Wheel Artwork",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objectInScene.transform.SetParent(wheel.transform, false);
            artwork = objectInScene.transform;
        }
        Image image = artwork.GetComponent<Image>();
        image.sprite = ringArt;
        image.color = new Color(0.68f, 0.70f, 0.66f, 0.96f);
        image.preserveAspect = true;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);

        foreach (string name in new[] { "Firefly", "Hedgehog", "Rabbit", "Honey Badger", "Boar" })
        {
            Transform item = wheel.transform.Find(name);
            if (item == null) throw new InvalidOperationException("Wheel item is missing: " + name);
            Transform medallion = item.Find("Carved Rim");
            if (medallion == null)
            {
                GameObject objectInItem = new GameObject("Carved Rim",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                objectInItem.transform.SetParent(item, false);
                medallion = objectInItem.transform;
            }
            Image medallionImage = medallion.GetComponent<Image>();
            medallionImage.sprite = ringArt;
            medallionImage.preserveAspect = true;
            medallionImage.raycastTarget = false;
            medallion.SetSiblingIndex(1);
            EditorUtility.SetDirty(medallionImage);
        }

        // The generated sprite replaces the first, code-drawn version of the rim.
        foreach (string name in new[] { "Bark Crown", "Moss Inlay" })
        {
            Transform oldRim = wheel.transform.Find(name);
            if (oldRim != null) UnityEngine.Object.DestroyImmediate(oldRim.gameObject);
        }
    }

    private static void Validate(AnimalSelectionWheel wheel, Sprite panel, Sprite ringArt)
    {
        Transform artwork = wheel != null ? wheel.transform.Find("Forest Wheel Artwork") : null;
        Image artImage = artwork != null ? artwork.GetComponent<Image>() : null;
        if (artImage == null || artImage.sprite != ringArt || artImage.raycastTarget)
            throw new InvalidOperationException("The generated forest wheel is not saved in the scene.");

        string[] names = { "Firefly", "Hedgehog", "Rabbit", "Honey Badger", "Boar" };
        foreach (string name in names)
        {
            Transform item = wheel.transform.Find(name);
            Transform rim = item != null ? item.Find("Wood Rim") : null;
            Image carved = item != null ? item.Find("Carved Rim")?.GetComponent<Image>() : null;
            if (rim == null || rim.GetComponent<RadialSegmentGraphic>() == null
                || carved == null || carved.sprite != ringArt || carved.raycastTarget)
                throw new InvalidOperationException("Missing editable medallion on " + name);
        }

        Transform toggle = wheel.AllVolleyButton != null ? wheel.AllVolleyButton.transform : null;
        Image background = toggle != null ? toggle.GetComponent<Image>() : null;
        if (background == null || background.sprite != panel
            || background.type != Image.Type.Sliced)
            throw new InvalidOperationException("The woodcut volley button was not saved.");
    }
}

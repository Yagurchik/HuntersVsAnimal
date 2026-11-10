using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One source of truth for all readable game typography. The font assets live in
/// Resources so runtime-created HUD elements and authored scene labels match.
/// </summary>
public static class GameTypography
{
    // TMP shader dimensions scale with the glyph; a little face dilation keeps
    // counters and narrow strokes visible without losing the dark silhouette.
    public const float TmpOutlineWidth = 0.16f;
    public const float TmpFaceDilate = 0.10f;
    public const float LegacyOutlineRatio = 0.07f;
    private const string LegacyFontResource = "Fonts/Kurale-Regular";
    private const string TmpFontResource = "Fonts/Kurale SDF";

    private static Font legacyFont;
    private static TMP_FontAsset tmpFont;

    public static Font Font
    {
        get
        {
            if (legacyFont == null) legacyFont = Resources.Load<Font>(LegacyFontResource);
            if (legacyFont == null) legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return legacyFont;
        }
    }

    public static TMP_FontAsset TmpFont
    {
        get
        {
            if (tmpFont == null) tmpFont = Resources.Load<TMP_FontAsset>(TmpFontResource);
            Material material = tmpFont != null ? tmpFont.material : null;
            if (material != null)
            {
                material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, TmpOutlineWidth);
                material.SetFloat(ShaderUtilities.ID_FaceDilate, TmpFaceDilate);
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            return tmpFont;
        }
    }

    public static void Apply(Text label)
    {
        if (label == null) return;
        if (Font != null) label.font = Font;
        Outline outline = label.GetComponent<Outline>();
        if (outline == null) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        float distance = LegacyOutlineDistanceFor(label);
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    public static float LegacyOutlineDistanceFor(Text label)
    {
        if (label == null) return 0f;
        // Best-fit may shrink the face well below fontSize. Base the outline
        // on its smallest possible size so the fill is never swallowed.
        float size = label.resizeTextForBestFit && label.resizeTextMinSize > 0
            ? Mathf.Min(label.fontSize, label.resizeTextMinSize)
            : label.fontSize;
        return Mathf.Max(0.6f, size * LegacyOutlineRatio);
    }

    public static void Apply(TMP_Text label)
    {
        if (label == null) return;
        if (label.GetComponentInParent<EnemyHealthBarView>(true) != null) return;
        if (TmpFont != null)
        {
            label.font = TmpFont;
            if (TmpFont.material != null) label.fontSharedMaterial = TmpFont.material;
        }
    }

    public static void ApplyTo(GameObject root)
    {
        if (root == null) return;
        foreach (Text label in root.GetComponentsInChildren<Text>(true)) Apply(label);
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) Apply(label);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        legacyFont = null;
        tmpFont = null;
        SceneManager.sceneLoaded -= ApplyToScene;
        SceneManager.sceneLoaded += ApplyToScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToLoadedScene()
    {
        ApplyToScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void ApplyToScene(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects()) ApplyTo(root);
    }
}

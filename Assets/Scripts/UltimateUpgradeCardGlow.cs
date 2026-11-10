using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UltimateUpgradeCardGlow : MonoBehaviour
{
    private Image frame;
    private Image portraitGlow;
    private Color baseFrameColor;
    private Color baseGlowColor;
    private Color accent;
    private bool active;

    public void Configure(bool shouldGlow, Image targetFrame, Image targetGlow,
        Color frameColor, Color glowColor, Color accentColor)
    {
        frame = targetFrame;
        portraitGlow = targetGlow;
        baseFrameColor = frameColor;
        baseGlowColor = glowColor;
        accent = accentColor;
        active = shouldGlow;
        enabled = shouldGlow;
        if (!shouldGlow) RestoreColors();
    }

    private void Update()
    {
        if (!active) return;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);
        if (frame != null)
            frame.color = Color.Lerp(baseFrameColor, accent, 0.12f + wave * 0.22f);
        if (portraitGlow != null)
        {
            Color color = baseGlowColor;
            color.a = Mathf.Lerp(0.34f, 0.68f, wave);
            portraitGlow.color = color;
        }
    }

    private void OnDisable()
    {
        RestoreColors();
    }

    private void RestoreColors()
    {
        if (frame != null) frame.color = baseFrameColor;
        if (portraitGlow != null) portraitGlow.color = baseGlowColor;
    }
}

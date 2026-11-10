using UnityEngine;

// A small, prefab-authored light inside each button eye. Both eyes share a
// flame rhythm while their fine flicker stays independent.
[DisallowMultipleComponent]
public sealed class PumpkinEyeFire : MonoBehaviour
{
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.16f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.62f;
    [SerializeField, Min(0f)] private float flickerSpeed = 11f;
    [SerializeField, Range(0f, 0.4f)] private float sizePulse = 0.12f;
    [SerializeField] private Color emberColor = new Color(1f, 0.26f, 0.015f, 1f);
    [SerializeField] private Color flameColor = new Color(1f, 0.82f, 0.16f, 1f);

    private Vector3 leftBaseScale;
    private Vector3 rightBaseScale;

    private void Awake()
    {
        if (leftEye != null) leftBaseScale = leftEye.transform.localScale;
        if (rightEye != null) rightBaseScale = rightEye.transform.localScale;
    }

    public void MatchSorting(SpriteRenderer character)
    {
        if (character == null) return;
        SetSorting(leftEye, character);
        SetSorting(rightEye, character);
    }

    private static void SetSorting(SpriteRenderer eye, SpriteRenderer character)
    {
        if (eye == null) return;
        eye.sortingLayerID = character.sortingLayerID;
        eye.sortingOrder = character.sortingOrder + 1;
    }

    private void Update()
    {
        float time = Time.time * flickerSpeed;
        float sharedFlame = Mathf.PerlinNoise(time * 0.38f, 2.71f);
        Flicker(leftEye, leftBaseScale, sharedFlame,
            Mathf.PerlinNoise(time, 0.37f));
        Flicker(rightEye, rightBaseScale, sharedFlame,
            Mathf.PerlinNoise(time + 4.6f, 8.19f));
    }

    private void Flicker(SpriteRenderer eye, Vector3 baseScale,
        float sharedFlame, float fineFlicker)
    {
        if (eye == null) return;
        float brightness = Mathf.Clamp01(sharedFlame * 0.66f + fineFlicker * 0.34f);
        Color color = Color.Lerp(emberColor, flameColor, brightness);
        color.a = Mathf.Lerp(minimumAlpha, maximumAlpha, brightness);
        eye.color = color;
        eye.transform.localScale = baseScale * (1f + (brightness - 0.5f) * sizePulse);
    }
}

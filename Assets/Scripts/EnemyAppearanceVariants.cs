using System;
using UnityEngine;

// Authored visual alternatives for one enemy type. Gameplay stays on the same prefab.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(-800)]
public sealed class EnemyAppearanceVariants : MonoBehaviour
{
    [Serializable]
    public struct Appearance
    {
        public Sprite sprite;
        public Vector3 visualScale;
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Appearance[] appearances;

    public int AppearanceCount => appearances != null ? appearances.Length : 0;
    public Sprite GetAppearanceSprite(int index) => appearances[index].sprite;
    public Vector3 GetAppearanceScale(int index) => appearances[index].visualScale;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null || AppearanceCount == 0)
        {
            return;
        }

        Appearance selected = appearances[UnityEngine.Random.Range(0, appearances.Length)];
        if (selected.sprite == null)
        {
            return;
        }

        spriteRenderer.sprite = selected.sprite;
        if (selected.visualScale.x > 0f && selected.visualScale.y > 0f
            && selected.visualScale.z > 0f)
        {
            transform.localScale = selected.visualScale;
        }
    }
}

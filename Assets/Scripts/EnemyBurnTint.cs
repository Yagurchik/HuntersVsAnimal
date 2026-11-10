using UnityEngine;

// Visualizes firefly stacks without changing materials or the enemy's visibility in darkness.
[DisallowMultipleComponent]
[DefaultExecutionOrder(850)]
public sealed class EnemyBurnTint : MonoBehaviour
{
    [SerializeField] private Color burnColor = new Color(1f, 0.37f, 0.08f, 1f);
    [SerializeField, Range(0f, 1f)] private float oneStackStrength = 0.2f;
    [SerializeField, Range(0f, 1f)] private float twoStackStrength = 0.4f;
    [SerializeField, Range(0f, 1f)] private float threeStackStrength = 0.62f;
    private EnemyStatusEffects statuses;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private int displayedStacks = -1;

    public int DisplayedStacks => Mathf.Max(0, displayedStacks);

    private void Awake()
    {
        statuses = GetComponent<EnemyStatusEffects>();
        CacheRenderers();
    }

    private void OnEnable()
    {
        if (statuses == null)
        {
            statuses = GetComponent<EnemyStatusEffects>();
        }

        if (renderers == null)
        {
            CacheRenderers();
        }

        displayedStacks = -1;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (statuses == null)
        {
            statuses = GetComponent<EnemyStatusEffects>();
        }

        int stacks = statuses != null
            ? Mathf.Clamp(statuses.GetStackCount(FireflyImpactEffect.BurnEffectId), 0,
                FireflyImpactEffect.MaximumBurnStacks)
            : 0;
        if (stacks == displayedStacks)
        {
            return;
        }

        displayedStacks = stacks;
        float strength = stacks == 1 ? oneStackStrength : stacks == 2 ? twoStackStrength
            : stacks >= 3 ? threeStackStrength : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                Color tinted = Color.Lerp(baseColors[i], burnColor, strength);
                tinted.a = baseColors[i].a;
                renderers[i].color = tinted;
            }
        }
    }

    public void CaptureCurrentAsBaseColors()
    {
        CacheRenderers();
        displayedStacks = -1;
        Refresh();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseColors[i] = renderers[i].color;
        }
    }

    private void OnDisable()
    {
        if (renderers != null && baseColors != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = baseColors[i];
                }
            }
        }

        displayedStacks = -1;
    }

    private void OnValidate()
    {
        oneStackStrength = Mathf.Clamp01(oneStackStrength);
        twoStackStrength = Mathf.Clamp(twoStackStrength, oneStackStrength, 1f);
        threeStackStrength = Mathf.Clamp(threeStackStrength, twoStackStrength, 1f);
    }
}

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class EnemyFirstEncounterProfile : MonoBehaviour
{
    [SerializeField, Tooltip("Stable identifier used to show this enemy only once per level run.")]
    private string enemyTypeId = "enemy";
    [SerializeField] private string displayName = "Противник";
    [SerializeField, TextArea(2, 4)] private string summary =
        "Новый противник. Изучи его поведение и не подпускай к линии поражения.";
    [SerializeField, Tooltip("Optional portrait. When empty, the current enemy sprite is used.")]
    private Sprite portrait;
    [SerializeField] private Color accentColor = new Color(0.92f, 0.49f, 0.16f, 1f);

    public string EnemyTypeId => string.IsNullOrWhiteSpace(enemyTypeId)
        ? gameObject.name.Replace("(Clone)", string.Empty).Trim()
        : enemyTypeId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Противник" : displayName.Trim();
    public string Summary => string.IsNullOrWhiteSpace(summary)
        ? "Новый противник. Изучи его поведение и не подпускай к линии поражения."
        : summary.Trim();
    public Sprite Portrait => portrait;
    public Color AccentColor => accentColor;

    public void Configure(string typeId, string title, string description,
        Sprite portraitSprite, Color accent)
    {
        enemyTypeId = string.IsNullOrWhiteSpace(typeId) ? "enemy" : typeId.Trim();
        displayName = string.IsNullOrWhiteSpace(title) ? "Противник" : title.Trim();
        summary = string.IsNullOrWhiteSpace(description)
            ? "Новый противник. Изучи его поведение и не подпускай к линии поражения."
            : description.Trim();
        portrait = portraitSprite;
        accentColor = new Color(Mathf.Clamp01(accent.r), Mathf.Clamp01(accent.g),
            Mathf.Clamp01(accent.b), 1f);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(enemyTypeId))
        {
            enemyTypeId = "enemy";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Противник";
        }

        accentColor.a = 1f;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseDamageUI : MonoBehaviour
{
    [Serializable]
    public sealed class DamageRow
    {
        public RectTransform root;
        public Image icon;
        public Text name;
        public Text damage;
        public Text percentage;
        public Image fill;
        public Button button;
        [NonSerialized] public int animalIndex = -1;
    }

    public static readonly string[] AnimalNames = {
        "Светлячок", "Ёж", "Кролик", "Медоед", "Кабан", "Бобёр", "Лягушка", "Белка"
    };
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    [SerializeField] private RectTransform card;
    [SerializeField] private RectTransform content;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Text totalDamage;
    [SerializeField] private Text hint;
    [SerializeField] private Sprite[] fallbackIcons;
    [SerializeField] private DamageRow[] rows;
    [SerializeField] private PauseProjectileUpgradesUI projectileUpgrades;
    private LevelTransition transition;
    private bool drawing;

    private void Awake()
    {
        if (rows == null) return;
        foreach (var row in rows)
            if (row != null && row.button != null)
                row.button.onClick.AddListener(() => OpenProjectileUpgrades(row));
    }

    private void OpenProjectileUpgrades(DamageRow row)
    {
        if (projectileUpgrades == null || row.animalIndex < 0) return;
        projectileUpgrades.Show(AnimalProjectileQueue.Instance, row.animalIndex, row.icon.sprite);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        projectileUpgrades?.Close();
        ProjectileDamageStats.Changed += Refresh;
        Canvas.ForceUpdateCanvases();
        LayoutCard();
        Refresh();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void OnDisable()
    {
        ProjectileDamageStats.Changed -= Refresh;
        projectileUpgrades?.Close();
    }

    private void OnRectTransformDimensionsChange() => LayoutCard();

    private void LayoutCard()
    {
        if (drawing || card == null) return;
        var root = transform as RectTransform;
        if (root == null || root.rect.width <= 1f || root.rect.height <= 1f) return;
        card.sizeDelta = new Vector2(Mathf.Min(980f, root.rect.width - 72f),
            Mathf.Min(1460f, root.rect.height - 72f));
    }

    public void Refresh()
    {
        AnimalProjectileQueue queue = AnimalProjectileQueue.Instance;
        var included = new List<int>(AnimalProjectileQueue.AnimalTypeCount);
        for (int i = 0; i < AnimalProjectileQueue.AnimalTypeCount; i++)
        {
            bool available = queue != null && i < queue.Animals.Count
                && (queue.HasRunLoadout ? queue.IsAnimalAllowedForRun(i) : queue.IsAvailable(i));
            if (available || ProjectileDamageStats.ForAnimal(i) > 0 || (queue == null && i == 0)) included.Add(i);
        }
        Draw(included, ProjectileDamageStats.ForAnimal, ProjectileDamageStats.TotalDamage, queue);
    }

    // Editor preview uses sample values without changing the live battle totals.
    public void Preview(AnimalProjectileQueue queue = null)
    {
        if (Application.isPlaying) return;
        LayoutCard();
        double[] sample = { 12340, 0, 4800, 0, 7350, 0, 3120, 5610 };
        var included = new List<int> { 0, 2, 4, 6, 7 };
        double total = 0;
        foreach (int i in included) total += sample[i];
        Draw(included, index => sample[index], total, queue);
    }

    private void Draw(List<int> included, Func<int, double> damageFor, double total, AnimalProjectileQueue queue)
    {
        if (drawing || rows == null || content == null) return;
        drawing = true;
        try
        {
            included.Sort((a, b) => {
                int compare = damageFor(b).CompareTo(damageFor(a));
                return compare != 0 ? compare : a.CompareTo(b);
            });
            if (totalDamage != null) totalDamage.text = FormatDamage(total);
            if (hint != null) hint.text = total > 0 ? "Нажми на снаряд — посмотреть улучшения"
                : "Урон появится после первых попаданий\nНажми на снаряд — посмотреть улучшения";
            int count = Mathf.Min(rows.Length, included.Count);
            for (int slot = 0; slot < rows.Length; slot++)
            {
                DamageRow row = rows[slot];
                if (row == null || row.root == null) continue;
                row.root.gameObject.SetActive(slot < count);
                row.animalIndex = slot < count ? included[slot] : -1;
                if (slot >= count) continue;
                int index = included[slot];
                double damage = damageFor(index);
                float share = total > 0 ? Mathf.Clamp01((float)(damage / total)) : 0f;
                row.root.anchoredPosition = new Vector2(0, -slot * 146f);
                row.name.text = AnimalNames[index];
                row.damage.text = FormatDamage(damage);
                row.percentage.text = (share * 100f).ToString("0.#", Russian) + "%";
                Sprite icon = queue != null && index < queue.Animals.Count ? queue.Animals[index].GetIcon() : null;
                if (icon == null && fallbackIcons != null && index < fallbackIcons.Length) icon = fallbackIcons[index];
                row.icon.sprite = icon;
                row.fill.color = CombatDamagePalette.ColorFor(CombatDamagePalette.FromAnimalIndex(index));
                row.fill.rectTransform.anchorMax = new Vector2(share, 1f);
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(0, count * 146f - 10f));
        }
        finally { drawing = false; }
    }

    private static string FormatDamage(double damage) => damage.ToString("N0", Russian);

    private LevelTransition ResolveTransition()
    {
        if (transition != null) return transition;
        foreach (LevelTransition candidate in FindObjectsOfType<LevelTransition>(true))
            if (candidate.PausePanel == gameObject) { transition = candidate; break; }
        return transition;
    }

    public void Continue() => ResolveTransition()?.ContinueButtonPressed();
    public void Restart() => ResolveTransition()?.ChangeScene(1);
    public void MainMenu() => ResolveTransition()?.ChangeScene(0);
}

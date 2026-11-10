using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PauseProjectileUpgradesUI : MonoBehaviour
{
    [Serializable]
    public sealed class UpgradeRow
    {
        public RectTransform root;
        public Text title;
        public Text description;
        public Text rank;
    }

    [SerializeField] private RectTransform card;
    [SerializeField] private Image icon;
    [SerializeField] private Text projectileName;
    [SerializeField] private Text state;
    [SerializeField] private Text empty;
    [SerializeField] private RectTransform content;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private UpgradeRow[] rows;
    [SerializeField] private Button closeButton;
    private AnimalProjectileQueue queue;
    private int animalIndex = -1;
    private int displayedCount;
    private bool drawing;
    private GameObject previousSelection;
    public bool IsOpen => gameObject.activeInHierarchy;
    public int AnimalIndex => animalIndex;

    public void Show(AnimalProjectileQueue currentQueue, int index, Sprite fallbackIcon)
    {
        if (index < 0 || index >= PauseDamageUI.AnimalNames.Length) return;
        if (IsOpen) Close();
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        queue = currentQueue; animalIndex = index;
        icon.sprite = queue != null && index < queue.Animals.Count ? queue.Animals[index].GetIcon() : null;
        if (icon.sprite == null) icon.sprite = fallbackIcon;
        icon.color = queue != null && index < queue.Animals.Count ? queue.Animals[index].GetIconColor() : Color.white;
        projectileName.text = PauseDamageUI.AnimalNames[index];
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases(); LayoutCard();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        if (EventSystem.current != null && closeButton != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void OnEnable()
    {
        if (queue != null) queue.UpgradesChanged += Refresh;
        LayoutCard();
    }
    private void OnDisable()
    {
        if (queue != null) queue.UpgradesChanged -= Refresh;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }
    private void OnRectTransformDimensionsChange() => LayoutCard();
    public void Close()
    {
        gameObject.SetActive(false);
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void Refresh()
    {
        if (drawing || rows == null || content == null) return;
        drawing = true;
        try
        {
            var chosen = ProjectileUpgradeSummary.ForAnimal(queue, animalIndex);
            displayedCount = Mathf.Min(rows.Length, chosen.Count);
            bool available = queue != null && queue.IsAvailable(animalIndex);
            state.text = available ? "Снарядов за залп: " + queue.Animals[animalIndex].AmmoPerTurn : "Снаряд ещё не открыт";
            empty.gameObject.SetActive(displayedCount == 0);
            empty.text = "Улучшения пока не выбраны";
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].root.gameObject.SetActive(i < displayedCount);
                if (i >= displayedCount) continue;
                var item = chosen[i]; var row = rows[i];
                row.root.anchoredPosition = new Vector2(0, -i * 190f);
                row.title.text = item.Title; row.description.text = item.Description;
                row.rank.text = item.Ultimate ? "УЛЬТА" : item.MaximumRank > 0 ? item.Rank + " / " + item.MaximumRank : "Ур. " + item.Rank;
                row.rank.color = item.Ultimate ? new Color(1f, 0.8f, 0.3f) : new Color(0.75f, 0.82f, 0.65f);
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(0, displayedCount * 190f - 10f));
        }
        finally { drawing = false; }
        LayoutCard();
    }

    private void LayoutCard()
    {
        if (drawing || card == null) return;
        var root = transform as RectTransform;
        if (root == null || root.rect.width <= 1f || root.rect.height <= 1f) return;
        float desiredHeight = Mathf.Clamp(364f + displayedCount * 190f, 560f, 1320f);
        card.sizeDelta = new Vector2(Mathf.Min(950f, root.rect.width - 72f), Mathf.Min(desiredHeight, root.rect.height - 72f));
    }
}

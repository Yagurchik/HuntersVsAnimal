using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ArtifactShopUI : MonoBehaviour
{
    [Serializable] public sealed class Card
    {
        public Button Button;
        public Image Icon;
        public Text Name, Price, Owned;
    }
    public Text Wallet, Heading, Feedback;
    public Button OrdinaryTab, AssembledTab, CloseButton, BackButton, AcquireButton;
    public Text AcquireLabel;
    public InputField Search;
    public ScrollRect List;
    public GameObject Browse, Detail;
    public Card[] Cards;
    public Image DetailIcon;
    public Text DetailName, DetailDescription, DetailCost, DetailOwned, DetailHint, RecipeHeading;
    public Card[] Components;
    private readonly List<int> shown = new List<int>();
    private readonly Stack<int> history = new Stack<int>();
    private bool assembled;
    private int selected = -1;
    private bool bound;
    public bool IsOpen => gameObject.activeSelf;

    private void OnEnable()
    {
        Bind();
        MetaProgression.Changed += Refresh;
        Refresh();
    }
    private void OnDisable() => MetaProgression.Changed -= Refresh;

    private void Bind()
    {
        if (bound)
        {
            return;
        }

        bound = true;
        OrdinaryTab.onClick.AddListener(() => SelectKind(false));
        AssembledTab.onClick.AddListener(() => SelectKind(true));
        CloseButton.onClick.AddListener(Close);
        BackButton.onClick.AddListener(Back);
        AcquireButton.onClick.AddListener(AcquireSelected);
        Search.onValueChanged.AddListener(_ => RefreshList());
        for (int i = 0; i < Cards.Length; i++)
        {
            int cell = i;
            Cards[i].Button.onClick.AddListener(() => { if (cell < shown.Count) { SelectArtifact(shown[cell]); } });
        }
        for (int i = 0; i < Components.Length; i++)
        {
            int cell = i;
            Components[i].Button.onClick.AddListener(() =>
            {
                var item = MetaProgression.GetArtifactDefinition(selected);
                if (item == null || cell >= item.Ingredients.Length)
                {
                    return;
                }

                history.Push(selected);
                ShowDetail(MetaProgression.FindArtifact(item.Ingredients[cell].Id));
            });
        }
    }
    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Bind();
        selected = -1; history.Clear();
        Search.SetTextWithoutNotify("");
        Feedback.text = "";
        Browse.SetActive(true); Detail.SetActive(false);
        List.verticalNormalizedPosition = 1f;
        Refresh();
    }
    public void Close() => gameObject.SetActive(false);
    private void SelectKind(bool value)
    {
        assembled = value;
        List.verticalNormalizedPosition = 1f;
        RefreshList();
    }
    public void SelectArtifact(int index)
    {
        history.Clear(); ShowDetail(index);
    }
    private void ShowDetail(int index)
    {
        if (MetaProgression.GetArtifactDefinition(index) == null)
        {
            return;
        }

        selected = index;
        Browse.SetActive(false); Detail.SetActive(true);
        Feedback.text = "";
        RefreshDetail();
    }
    public void Back()
    {
        Feedback.text = "";
        if (history.Count > 0) { ShowDetail(history.Pop()); return; }
        selected = -1;
        Browse.SetActive(true); Detail.SetActive(false);
        RefreshList();
    }
    public void Refresh()
    {
        Wallet.text = "ЗОЛОТО  " + MetaProgression.Gold;
        if (selected >= 0)
        {
            RefreshDetail();
        }
        else
        {
            RefreshList();
        }
    }
    private void RefreshList()
    {
        if (Cards == null)
        {
            return;
        }

        Heading.text = assembled ? "Сборные реликвии" : "Обычные артефакты";
        OrdinaryTab.image.color = assembled ? VillageUIStyle.Panel : VillageUIStyle.Green;
        AssembledTab.image.color = assembled ? VillageUIStyle.Green : VillageUIStyle.Panel;
        shown.Clear();
        string query = Search.text.Trim().ToLowerInvariant();
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
        {
            var item = MetaProgression.GetArtifactDefinition(i);
            if (item.IsAssembled != assembled)
            {
                continue;
            }

            if (query.Length > 0 && !(item.Name + " " + item.Category + " " + item.Description).ToLowerInvariant().Contains(query))
            {
                continue;
            }

            shown.Add(i);
        }
        for (int i = 0; i < Cards.Length; i++)
        {
            var card = Cards[i];
            card.Button.gameObject.SetActive(i < shown.Count);
            if (i >= shown.Count)
            {
                continue;
            }

            int index = shown[i]; var item = MetaProgression.GetArtifactDefinition(index);
            card.Icon.sprite = item.Icon;
            card.Name.text = item.Name;
            card.Price.text = item.IsAssembled ? "СБОРКА  ·  " + item.GoldCost : item.GoldCost + " ЗОЛОТА";
            card.Owned.text = MetaProgression.OwnedArtifactAmount(index) > 0
                ? "В наличии: " + MetaProgression.OwnedArtifactAmount(index) : item.Category;
            card.Owned.color = MetaProgression.OwnedArtifactAmount(index) > 0 ? VillageUIStyle.Gold : VillageUIStyle.Muted;
        }
        List.content.sizeDelta = new Vector2(0, Mathf.Max(1, (shown.Count + 2) / 3) * 248);
    }
    private void RefreshDetail()
    {
        var item = MetaProgression.GetArtifactDefinition(selected);
        if (item == null)
        {
            return;
        }

        Heading.text = item.IsAssembled ? "Сборная реликвия" : "Обычный артефакт";
        DetailIcon.sprite = item.Icon;
        DetailName.text = item.Name;
        DetailDescription.text = item.Description;
        DetailOwned.text = "В наличии: " + MetaProgression.OwnedArtifactAmount(selected)
            + (MetaProgression.IsArtifactEquipped(selected) ? "  ·  один надет" : "");
        DetailCost.text = item.IsAssembled ? "Работа мастера: " + item.GoldCost + " золота"
            + "\nПолная стоимость: " + MetaProgression.GetArtifactTotalCost(selected)
            : "Цена: " + item.GoldCost + " золота";
        RecipeHeading.text = item.IsAssembled ? "КОМПОНЕНТЫ  ·  нажми, чтобы посмотреть" : "Готовый предмет из лесной лавки";
        for (int i = 0; i < Components.Length; i++)
        {
            var card = Components[i]; card.Button.gameObject.SetActive(i < item.Ingredients.Length);
            if (i >= item.Ingredients.Length)
            {
                continue;
            }

            var ingredient = item.Ingredients[i];
            int index = MetaProgression.FindArtifact(ingredient.Id);
            var component = MetaProgression.GetArtifactDefinition(index);
            card.Icon.sprite = component.Icon;
            card.Name.text = component.Name;
            int available = MetaProgression.AvailableArtifactAmount(index);
            card.Price.text = "В РЮКЗАКЕ: " + available + " / " + ingredient.Amount;
            card.Owned.text = MetaProgression.IsArtifactEquipped(index) ? "Одна копия надета" : component.IsAssembled ? "Сборный компонент" : "Можно купить";
            card.Price.color = available >= ingredient.Amount ? VillageUIStyle.Gold : new Color(1f, 0.57f, 0.39f);
        }
        AcquireButton.interactable = MetaProgression.CanAcquireArtifact(selected, out string reason);
        AcquireLabel.text = (item.IsAssembled ? "СОБРАТЬ" : "КУПИТЬ") + "  ·  " + item.GoldCost + " ЗОЛОТА";
        DetailHint.text = reason + "\nБонус действует, когда предмет надет на героя.";
    }
    public void AcquireSelected()
    {
        if (!Application.isPlaying || !MetaProgression.TryAcquireArtifact(selected))
        {
            return;
        }

        Refresh();
        Feedback.text = MetaProgression.GetArtifactDefinition(selected).Name + " — в рюкзаке.";
    }
}

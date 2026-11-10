using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ProjectileSkinShopUI : MonoBehaviour
{
    [Serializable]
    public sealed class Card
    {
        public Image Preview;
        public Text ProjectileName, SkinName, Status, ActionLabel;
        public Button Action, Original;
    }

    public Text Wallet, Feedback;
    public Button CloseButton;
    public ScrollRect List;
    public Card[] Cards;
    private MenuHubController hub;
    private bool bound;
    public bool IsOpen => gameObject.activeSelf;

    public static ProjectileSkinShopUI Ensure(MenuHubController owner)
    {
        Transform page = owner.tabs[2].page.transform;
        var shop = page.Find("Projectile Skin Shop")?.GetComponent<ProjectileSkinShopUI>();
        var open = page.Find("Forest Village Runtime/Viewport/Content/Village Field/Open Skin Shop")?.GetComponent<Button>();
        if (shop == null || open == null)
        {
            Debug.LogError("Editable projectile skin shop is missing. Bake ProjectileSkinAuthoring into UIscene.");
            return null;
        }
        shop.hub = owner;
        shop.Bind();
        open.onClick.RemoveAllListeners();
        open.onClick.AddListener(() => { owner.CloseDialogs(); shop.Show(); });
        return shop;
    }

    private void Bind()
    {
        if (bound)
        {
            return;
        }

        bound = true;
        CloseButton.onClick.AddListener(Close);
        for (int i = 0; i < Cards.Length; i++)
        {
            int index = i;
            Cards[i].Action.onClick.AddListener(() => AcquireOrEquip(index));
            Cards[i].Original.onClick.AddListener(() => UseOriginal(index));
        }
    }
    private void OnEnable()
    {
        Bind();
        MetaProgression.Changed += Refresh;
        ProjectileSkins.Changed += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        MetaProgression.Changed -= Refresh;
        ProjectileSkins.Changed -= Refresh;
    }
    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Feedback.text = "Купленный облик остаётся навсегда.";
        List.verticalNormalizedPosition = 1f;
        Refresh();
    }
    public void Close() => gameObject.SetActive(false);
    public void Refresh()
    {
        if (Wallet == null || Cards == null)
        {
            return;
        }

        Wallet.text = "БРИЛЛИАНТЫ  " + MetaProgression.Diamonds;
        for (int i = 0; i < Cards.Length; i++)
        {
            var skin = ProjectileSkins.Get(i);
            var card = Cards[i];
            card.Action.transform.parent.gameObject.SetActive(skin != null);
            if (skin == null)
            {
                continue;
            }

            bool owned = ProjectileSkins.IsOwned(i), equipped = ProjectileSkins.IsEquipped(i);
            card.Preview.sprite = skin.Artwork;
            card.ProjectileName.text = skin.ProjectileName;
            card.SkinName.text = skin.Name;
            card.Status.text = equipped ? "Сейчас выбран" : owned ? "В коллекции"
                : MetaProgression.Diamonds < skin.DiamondPrice ? "Не хватает "
                    + (skin.DiamondPrice - MetaProgression.Diamonds) + " бриллиантов" : "Новый облик";
            card.Status.color = owned ? VillageUIStyle.Gold : VillageUIStyle.Muted;
            card.ActionLabel.text = equipped ? "ВЫБРАН" : owned ? "НАДЕТЬ"
                : "КУПИТЬ · " + skin.DiamondPrice + " БРИЛЛ.";
            card.Action.interactable = !equipped && (owned || MetaProgression.Diamonds >= skin.DiamondPrice);
            card.Original.interactable = equipped;
            card.Original.GetComponentInChildren<Text>().text = equipped ? "ОБЫЧНЫЙ ОБЛИК" : "ОБЫЧНЫЙ ВЫБРАН";
        }
    }
    public void AcquireOrEquip(int index)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        bool owned = ProjectileSkins.IsOwned(index);
        bool success = owned ? ProjectileSkins.TryEquip(index, true) : ProjectileSkins.TryBuy(index);
        Refresh();
        Feedback.text = success ? ProjectileSkins.Get(index).Name + (owned ? " — выбран." : " — куплен и выбран!")
            : "Покупка недоступна. Проверь баланс бриллиантов.";
        hub?.Refresh();
    }
    public void UseOriginal(int index)
    {
        if (!Application.isPlaying || !ProjectileSkins.TryEquip(index, false))
        {
            return;
        }

        Refresh();
        Feedback.text = "Выбран обычный облик. Купленный скин сохранён.";
    }
}

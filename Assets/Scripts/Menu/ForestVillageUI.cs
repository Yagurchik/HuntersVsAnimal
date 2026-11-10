using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;

public sealed class ForestVillageUI
{
    public const float EntityContentHeight = 1494f;
    public const float EntityFieldHeight = 1384f;
    public const float TreeWindowHeight = 460f;
    public const float MerchantWindowHeight = 290f;
    public const float EntityWindowGap = 18f;
    private MenuHubController hub;
    private ArtifactShopUI artifactShop;
    private Text wallet;
    private Text collection;
    private VillageBuildingView tree;
    private VillageBuildingView shop;
    private GameObject dialog;
    private RectTransform dialogPanel;
    private Text title;
    private Text income;
    private Text storage;
    private Text nextLevel;
    private Text feedback;
    private Button upgrade;
    private Text upgradeLabel;
    private Button collect;
    private Text collectLabel;
    private GameObject offers;
    private Button spin;
    private Text spinLabel;
    private Text spinResult;
    private bool spinning;
    private VillageBuildingKind selectedBuilding;
    private float nextCollection;
    private bool incomeStarted;
    public bool IsDialogOpen => (dialog != null && dialog.activeSelf) || (artifactShop != null && artifactShop.IsOpen);

    public static ForestVillageUI Ensure(MenuHubController owner)
    {
        if (owner.tabs == null || owner.tabs.Length < 3 || owner.tabs[2].page == null) return null;
        var ui = new ForestVillageUI { hub = owner };
        Transform page = owner.tabs[2].page.transform;
        if (page.Find("Forest Village Runtime") != null) ui.BindExisting(page);
        else if (!Application.isPlaying) ui.Build(page);
        else
        {
            Debug.LogError("The editable village UI is missing from UIscene. "
                + "Use Forest Animal War/UI/Bake editable village and artifacts into UIscene.");
            return null;
        }
        owner.tabs[2].title = "Деревня";
        if (owner.tabs[2].buttonLabel != null) owner.tabs[2].buttonLabel.text = "ДЕРЕВНЯ";
        ui.artifactShop = page.GetComponentInChildren<ArtifactShopUI>(true);
        Transform catalogueButton = page.Find("Building Menu/Panel/Shop Offers/Open Catalog");
        if (catalogueButton != null) Bind(catalogueButton.GetComponent<Button>(), ui.OpenCatalog);
        ui.Refresh();
        return ui;
    }

    private void BindExisting(Transform page)
    {
        Transform content = page.Find("Forest Village Runtime/Viewport/Content");
        wallet = content.Find("Resources").GetComponent<Text>();
        collection = content.Find("Collection").GetComponent<Text>();
        Transform field = content.Find("Village Field");
        tree = field.Find("GreatTreeBuilding").GetComponent<VillageBuildingView>();
        shop = field.Find("ForestShopBuilding").GetComponent<VillageBuildingView>();
        tree.Bind(OpenBuilding);
        shop.Bind(OpenBuilding);

        Transform overlay = page.Find("Building Menu");
        dialog = overlay.gameObject;
        dialogPanel = (RectTransform)overlay.Find("Panel");
        title = dialogPanel.Find("Title").GetComponent<Text>();
        income = dialogPanel.Find("Income").GetComponent<Text>();
        storage = dialogPanel.Find("Storage").GetComponent<Text>();
        nextLevel = dialogPanel.Find("Next Level").GetComponent<Text>();
        feedback = dialogPanel.Find("Feedback").GetComponent<Text>();
        upgrade = dialogPanel.Find("Upgrade Building").GetComponent<Button>();
        upgradeLabel = upgrade.GetComponentInChildren<Text>();
        Transform collectTransform = dialogPanel.Find("Collect Income");
        if (collectTransform == null && !Application.isPlaying)
            collectTransform = Button("Collect Income", dialogPanel, "СОБРАТЬ", 32, 404, 916, 68,
                Collect, 27).transform;
        if (collectTransform != null)
        {
            collect = collectTransform.GetComponent<Button>();
            collectLabel = collect.GetComponentInChildren<Text>();
            Bind(collect, Collect);
        }
        offers = dialogPanel.Find("Shop Offers").gameObject;
        Transform spinButton = offers.transform.Find("Spin Button");
        if (spinButton == null && !Application.isPlaying) InstallRouletteView(offers.transform);
        spinButton = offers.transform.Find("Spin Button");
        if (spinButton != null)
        {
            spin = spinButton.GetComponent<Button>();
            spinLabel = spin.GetComponentInChildren<Text>();
            spinResult = offers.transform.Find("Spin Result").GetComponent<Text>();
            Bind(spin, Spin);
        }
        Bind(upgrade, Upgrade);
        Bind(overlay.GetComponent<Button>(), hub.CloseDialogs);
        Bind(dialogPanel.Find("Close").GetComponent<Button>(), hub.CloseDialogs);
        dialog.SetActive(false);
    }

    private void InstallRouletteView(Transform inventory)
    {
        Text hint = inventory.Find("Hint").GetComponent<Text>();
        hint.text = "Случайный артефакт из тех, которых у тебя ещё нет.";
        spinResult = Label("Spin Result", inventory, "?", 42, Gold, 0, 108, 916, 126,
            true, TextAnchor.MiddleCenter);
        spin = Button("Spin Button", inventory, "", 0, 258, 916, 78, Spin, 29);
        spinLabel = spin.GetComponentInChildren<Text>();
        Label("Pool Header", inventory, "ВОЗМОЖНЫЕ НАХОДКИ", 22, Gold,
            0, 355, 916, 34, true);
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
        {
            Transform card = inventory.Find("Offer " + MetaProgression.GetArtifactDefinition(i).Id);
            if (card == null) continue;
            Top((RectTransform)card, (i % 2) * 466, 396 + (i / 2) * 98, 450, 88);
            Top((RectTransform)card.Find("Name"), 16, 5, 418, 34);
            Top((RectTransform)card.Find("Effect"), 16, 41, 418, 44);
            Transform buy = card.Find("Buy");
            if (buy != null) UnityEngine.Object.DestroyImmediate(buy.gameObject);
        }
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void Tick()
    {
        if (!Application.isPlaying) return;
        if (incomeStarted && Time.unscaledTime < nextCollection) return;
        MetaProgression.AccruePassiveIncome();
        incomeStarted = true;
        nextCollection = Time.unscaledTime + 30f;
        Refresh();
    }

    public void SavePassiveIncome()
    {
        if (Application.isPlaying) MetaProgression.AccruePassiveIncome();
    }

    public void Refresh()
    {
        wallet.text = "ЗОЛОТО  " + FormatAmount(MetaProgression.GoldExact)
            + "     ЖЁЛУДИ  " + FormatAmount(MetaProgression.AcornsExact)
            + "\nСЕМЕНА  " + MetaProgression.Seeds + "     БРИЛЛИАНТЫ  " + MetaProgression.Diamonds;
        PassiveIncomeResult pending = MetaProgression.PendingPassiveIncome;
        collection.text = "НАКОПЛЕНО: " + FormatAmount(pending.Acorns) + " ЖЕЛУДЕЙ  ·  "
            + FormatAmount(pending.Gold) + " ЗОЛОТА";
        if (tree != null) tree.Refresh();
        if (shop != null) shop.Refresh();
        if (artifactShop != null && artifactShop.IsOpen) artifactShop.Refresh();
        if (dialog == null || !dialog.activeSelf) return;
        bool isTree = selectedBuilding == VillageBuildingKind.GreatTree;
        int level = isTree ? MetaProgression.GreatTreeLevel : MetaProgression.ForestShopLevel;
        int cost = isTree ? MetaProgression.GetGreatTreeUpgradeCost() : MetaProgression.GetForestShopUpgradeCost();
        int balance = isTree ? MetaProgression.Gold : MetaProgression.Acorns;
        double rate = isTree ? MetaProgression.GreatTreeAcornsPerMinute : MetaProgression.ForestShopGoldPerMinute;
        string output = isTree ? "желудей" : "золота";
        string payment = isTree ? "ЗОЛОТА" : "ЖЕЛУДЕЙ";
        title.text = (isTree ? "Вечное древо" : "Лесная лавка") + "  ·  ур. " + level;
        income.text = "+" + FormatRate(rate) + " " + output + " / мин"
            + "   ·   " + FormatAmount(rate * 60d) + " / час";
        storage.text = "Накоплено " + FormatAmount(isTree ? pending.Acorns : pending.Gold)
            + " " + output + "  ·  " + FormatAmount(pending.Minutes / 60d) + " / 8 часов";
        nextLevel.text = cost == 0 ? "Достигнут максимальный уровень."
            : "После улучшения: +" + FormatRate(rate * (level + 1d) / level) + " " + output + " / мин.";
        upgrade.interactable = cost > 0 && balance >= cost;
        upgradeLabel.text = cost == 0 ? "МАКСИМАЛЬНЫЙ УРОВЕНЬ" : "УЛУЧШИТЬ  ·  " + cost + " " + payment;
        feedback.text = isTree ? "Вечное древо хранит жёлуди и золото всей деревни."
            : cost > balance ? "Не хватает " + (cost - balance) + " " + payment.ToLowerInvariant() + "."
            : "Ворон копит золото. Забрать его можно у Вечного древа.";
        if (collect != null)
        {
            collect.gameObject.SetActive(isTree);
            collect.interactable = pending.HasIncome;
            collectLabel.text = pending.HasIncome ? "СОБРАТЬ ВСЁ" : "ПОКА НЕЧЕГО СОБИРАТЬ";
            Top((RectTransform)feedback.transform, 32, isTree ? 490 : 412, 916, 66);
        }
        if (spin != null)
        {
            spin.interactable = !spinning && MetaProgression.CanSpinArtifact;
            spinLabel.text = MetaProgression.UnownedOrdinaryArtifactCount == 0 ? "ВСЕ ОБЫЧНЫЕ ПОЛУЧЕНЫ"
                : "КРУТИТЬ  ·  " + MetaProgression.ArtifactSpinCost + " ЗОЛОТА";
        }
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
        {
            MetaArtifactDefinition definition = MetaProgression.GetArtifactDefinition(i);
            Transform card = offers.transform.Find("Offer " + definition.Id);
            if (card == null) continue;
            bool owned = MetaProgression.IsArtifactOwned(i);
            card.Find("Name").GetComponent<Text>().text = definition.Name + (owned ? "  ✓" : "");
            card.Find("Effect").GetComponent<Text>().text = definition.Description;
            card.GetComponent<Image>().color = owned ? Panel : Background;
        }
    }

    public void CloseDialog()
    {
        if (dialog != null) dialog.SetActive(false);
        if (artifactShop != null) artifactShop.Close();
    }

    private void OpenCatalog()
    {
        hub.CloseDialogs();
        artifactShop?.Show();
    }

    public void OpenBuilding(VillageBuildingKind kind)
    {
        hub.CloseDialogs();
        if (Application.isPlaying) MetaProgression.AccruePassiveIncome();
        selectedBuilding = kind;
        bool isShop = kind == VillageBuildingKind.ForestShop;
        offers.SetActive(isShop);
        dialogPanel.sizeDelta = new Vector2(980, isShop ? 1100 : 600);
        if (isShop && spinResult != null && !spinning) spinResult.text = "?";
        dialog.SetActive(true);
        dialog.transform.SetAsLastSibling();
        Refresh();
    }

    private void Upgrade()
    {
        if (!Application.isPlaying) return;
        if (selectedBuilding == VillageBuildingKind.GreatTree) MetaProgression.TryUpgradeGreatTree();
        else MetaProgression.TryUpgradeForestShop();
        Refresh();
    }

    private void Spin()
    {
        if (!Application.isPlaying || spinning || !MetaProgression.TrySpinArtifact(out int index)) return;
        spinning = true;
        Refresh();
        hub.StartCoroutine(SpinAnimation(index));
    }

    private IEnumerator SpinAnimation(int result)
    {
        for (int step = 0; step < 20; step++)
        {
            int preview = UnityEngine.Random.Range(0, Mathf.Min(MetaProgression.OrdinaryArtifactCount, MetaProgression.ArtifactCount));
            spinResult.text = MetaProgression.GetArtifactDefinition(preview).Name;
            yield return new WaitForSecondsRealtime(0.045f + step * 0.0035f);
        }
        spinResult.text = "✦  " + MetaProgression.GetArtifactDefinition(result).Name + "  ✦";
        spinning = false;
        Refresh();
        feedback.text = "Новый артефакт в рюкзаке. Надень его во вкладке «Артефакты».";
    }

    private void Collect()
    {
        if (!Application.isPlaying || selectedBuilding != VillageBuildingKind.GreatTree) return;
        PassiveIncomeResult result = MetaProgression.CollectPassiveIncome();
        if (!result.HasIncome) return;
        Refresh();
        feedback.text = "Собрано: +" + FormatAmount(result.Acorns) + " желудей и +"
            + FormatAmount(result.Gold) + " золота.";
    }

    private void Build(Transform page)
    {
        RectTransform content = ScrollPage(page, "Forest Village Runtime", EntityContentHeight);
        content.GetComponentInParent<ScrollRect>(true).vertical = false;
        // The common header already shows all four wallet resources.
        wallet = Label("Resources", content, "", 24, Cream, 44, 0, 992, 78, true);
        wallet.gameObject.SetActive(false);
        collection = Label("Collection", content, "Сбережения ждут у Вечного древа.",
            23, Muted, 44, EntityFieldHeight + 18f, 992, 64, false, TextAnchor.MiddleCenter);
        RectTransform field = Rect("Village Field", content);
        Top(field, 44, 0, 992, EntityFieldHeight);
        tree = SpawnBuilding("GreatTreeBuilding", field);
        shop = SpawnBuilding("ForestShopBuilding", field);
        SpawnEntityWindow("ChameleonWindow", "Open Skin Shop", field);
        SpawnEntityWindow("OwlWindow", "Open Bestiary", field);
        dialogPanel = Modal(page, "Building Menu", 1330, hub.CloseDialogs, out dialog);
        title = Label("Title", dialogPanel, "", 37, Gold, 32, 40, 830, 70, true);
        income = Label("Income", dialogPanel, "", 28, Cream, 32, 134, 916, 52, true);
        storage = Label("Storage", dialogPanel, "", 23, Muted, 32, 200, 916, 44);
        nextLevel = Label("Next Level", dialogPanel, "", 23, Cream, 32, 254, 916, 44);
        upgrade = Button("Upgrade Building", dialogPanel, "", 32, 316, 916, 76, Upgrade, 27);
        upgradeLabel = upgrade.GetComponentInChildren<Text>();
        collect = Button("Collect Income", dialogPanel, "СОБРАТЬ ВСЁ", 32, 404, 916, 68, Collect, 27);
        collectLabel = collect.GetComponentInChildren<Text>();
        feedback = Label("Feedback", dialogPanel, "", 21, Muted, 32, 412, 916, 66);
        RectTransform inventory = Rect("Shop Offers", dialogPanel);
        Top(inventory, 32, 500, 916, 804);
        offers = inventory.gameObject;
        Label("Title", inventory, "АРТЕФАКТЫ ДЛЯ ТЫКВЫ", 27, Gold, 0, 0, 916, 40, true);
        Label("Hint", inventory, "Случайный артефакт из тех, которых у тебя ещё нет.",
            20, Muted, 0, 45, 916, 48);
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
        {
            MetaArtifactDefinition definition = MetaProgression.GetArtifactDefinition(i);
            RectTransform card = PanelRect("Offer " + definition.Id, inventory, Background,
                (i % 2) * 466, 396 + (i / 2) * 98, 450, 88);
            Label("Name", card, definition.Name, 24, Cream, 16, 5, 418, 34, true);
            Label("Effect", card, definition.Description, 18, Muted, 16, 41, 418, 44);
        }
        spinResult = Label("Spin Result", inventory, "?", 42, Gold, 0, 108, 916, 126,
            true, TextAnchor.MiddleCenter);
        spin = Button("Spin Button", inventory, "", 0, 258, 916, 78, Spin, 29);
        spinLabel = spin.GetComponentInChildren<Text>();
        Label("Pool Header", inventory, "ВОЗМОЖНЫЕ НАХОДКИ", 22, Gold,
            0, 355, 916, 34, true);
    }

    private VillageBuildingView SpawnBuilding(string resource, Transform field)
    {
        GameObject prefab = Resources.Load<GameObject>("Village/" + resource);
        if (prefab == null)
        {
            Debug.LogError("Village building prefab is missing: " + resource);
            return null;
        }
        GameObject instance = UnityEngine.Object.Instantiate(prefab, field, false);
        instance.name = resource;
        var view = instance.GetComponent<VillageBuildingView>();
        view.Bind(OpenBuilding);
        return view;
    }

    private static void SpawnEntityWindow(string resource, string instanceName, Transform field)
    {
        GameObject prefab = Resources.Load<GameObject>("Village/" + resource);
        if (prefab == null)
        {
            Debug.LogError("Village entity window prefab is missing: " + resource);
            return;
        }
        GameObject instance = UnityEngine.Object.Instantiate(prefab, field, false);
        instance.name = instanceName;
    }

    public static string FormatAmount(double value)
        => Math.Max(0d, value).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string FormatRate(double value)
        => Math.Max(0d, value).ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
}

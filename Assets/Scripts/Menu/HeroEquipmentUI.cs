using UnityEngine;
using UnityEngine.UI;
using static VillageUIStyle;

public sealed class HeroEquipmentUI
{
    private const int BackpackCells = 60;
    private MenuHubController hub;
    private readonly Button[] slots = new Button[MetaProgression.EquipmentSlotCount];
    private readonly Text[] slotMarks = new Text[MetaProgression.EquipmentSlotCount];
    private readonly Text[] slotNames = new Text[MetaProgression.EquipmentSlotCount];
    private readonly Button[] bag = new Button[BackpackCells];
    private readonly Text[] bagMarks = new Text[BackpackCells];
    private readonly Text[] bagNames = new Text[BackpackCells];
    private readonly int[] bagContents = new int[BackpackCells];
    private RectTransform contentRect;
    private readonly Image[] slotIcons = new Image[MetaProgression.EquipmentSlotCount];
    private readonly Image[] bagIcons = new Image[BackpackCells];
    private readonly Text[] bagQuantities = new Text[BackpackCells];
    private Text hint;
    private Text bagCount;
    private Text equippedCount;
    private GameObject dialog;
    private Text artifactName;
    private Text artifactEffect;
    private Text artifactActionHint;
    private Button equip;
    private Button remove;
    private int selectedSlot = -1;
    private int selectedArtifact = -1;
    public bool IsDialogOpen => dialog != null && dialog.activeSelf;

    public static HeroEquipmentUI Ensure(MenuHubController owner)
    {
        if (owner.tabs == null || owner.tabs.Length < 2 || owner.tabs[1].page == null) return null;
        var ui = new HeroEquipmentUI { hub = owner };
        Transform page = owner.tabs[1].page.transform;
        if (page.Find("Hero Equipment Runtime") != null) ui.BindExisting(page);
        else if (!Application.isPlaying) ui.Build(page);
        else
        {
            Debug.LogError("The editable artifact UI is missing from UIscene. "
                + "Use Forest Animal War/UI/Bake editable village and artifacts into UIscene.");
            return null;
        }
        owner.tabs[1].title = "Артефакты";
        if (owner.tabs[1].buttonLabel != null) owner.tabs[1].buttonLabel.text = "АРТЕФАКТЫ";
        ui.Refresh();
        return ui;
    }

    private void BindExisting(Transform page)
    {
        Transform content = page.Find("Hero Equipment Runtime/Viewport/Content");
        contentRect = (RectTransform)content;
        equippedCount = content.Find("Equipped Count").GetComponent<Text>();
        hint = content.Find("Hint").GetComponent<Text>();
        bagCount = content.Find("Backpack Title").GetComponent<Text>();
        for (int slot = 0; slot < slots.Length; slot++)
        {
            int index = slot;
            slots[slot] = content.Find("Equipment Slot " + (slot + 1)).GetComponent<Button>();
            slotMarks[slot] = slots[slot].transform.Find("Symbol").GetComponent<Text>();
            slotIcons[slot] = slots[slot].transform.Find("Icon")?.GetComponent<Image>();
            slotNames[slot] = slots[slot].transform.Find("Artifact Name").GetComponent<Text>();
            Bind(slots[slot], () => SelectSlot(index));
        }
        Button villageButton = content.Find("Visit Forest Shop").GetComponent<Button>();
        Bind(villageButton, () => hub.SelectTab(2));
        for (int cell = 0; cell < bag.Length; cell++)
        {
            int index = cell;
            bag[cell] = content.Find("Backpack Cell " + (cell + 1)).GetComponent<Button>();
            bagMarks[cell] = bag[cell].transform.Find("Symbol").GetComponent<Text>();
            bagIcons[cell] = bag[cell].transform.Find("Icon")?.GetComponent<Image>();
            bagQuantities[cell] = bag[cell].transform.Find("Quantity")?.GetComponent<Text>();
            bagNames[cell] = bag[cell].transform.Find("Artifact Name").GetComponent<Text>();
            Bind(bag[cell], () => OpenArtifact(bagContents[index]));
        }

        Transform overlay = page.Find("Artifact Details");
        dialog = overlay.gameObject;
        Transform panel = overlay.Find("Panel");
        artifactName = panel.Find("Name").GetComponent<Text>();
        artifactEffect = panel.Find("Effect").GetComponent<Text>();
        artifactActionHint = panel.Find("Action Hint").GetComponent<Text>();
        equip = panel.Find("Equip").GetComponent<Button>();
        remove = panel.Find("Unequip").GetComponent<Button>();
        Bind(equip, EquipSelected);
        Bind(remove, RemoveSelected);
        Bind(overlay.GetComponent<Button>(), hub.CloseDialogs);
        Bind(panel.Find("Close").GetComponent<Button>(), hub.CloseDialogs);
        dialog.SetActive(false);
        // Migrate the old left-anchored layout once. Keep later Inspector edits intact.
        if (!Application.isPlaying
            && ((RectTransform)content.Find("Hero Stage")).anchorMin.x < 0.49f)
            ApplyCenteredSlotLayout(content);

    }

    private static void CenterTop(RectTransform rect, float centerX, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(centerX, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void ApplyCenteredSlotLayout(Transform content)
    {
        ((RectTransform)content).sizeDelta = new Vector2(0f, 2090f);
        RectTransform stage = (RectTransform)content.Find("Hero Stage");
        CenterTop(stage, 0f, 214f, 576f, 614f);
        RectTransform portrait = (RectTransform)stage.Find("Pumpkin Portrait");
        portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
        portrait.pivot = new Vector2(0.5f, 0.5f);
        portrait.anchoredPosition = new Vector2(0f, 27f);
        portrait.sizeDelta = new Vector2(544f, 528f);
        for (int slot = 0; slot < slots.Length; slot++)
        {
            float centerX = slot < 3 ? -416f : 416f;
            float y = 214f + (slot % 3) * 208f;
            CenterTop((RectTransform)slots[slot].transform, centerX, y, 200f, 200f);
            Top((RectTransform)slotMarks[slot].transform, 6f, 9f, 132f, 60f);
            Top((RectTransform)slotNames[slot].transform, 6f, 73f, 132f, 62f);
        }
        for (int cell = 0; cell < bag.Length; cell++)
        {
            float centerX = -378f + (cell % 4) * 252f;
            float y = 1064f + (cell / 4) * 244f;
            CenterTop((RectTransform)bag[cell].transform, centerX, y, 228f, 228f);
            Top((RectTransform)bagMarks[cell].transform, 10f, 28f, 208f, 84f);
            Top((RectTransform)bagNames[cell].transform, 10f, 122f, 208f, 86f);
        }
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void CloseDialog()
    {
        if (dialog != null) dialog.SetActive(false);
    }

    public void Refresh()
    {
        int equipped = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            int artifact = MetaProgression.GetEquippedArtifact(i);
            MetaArtifactDefinition definition = MetaProgression.GetArtifactDefinition(artifact);
            if (definition != null) equipped++;
            slotMarks[i].text = definition != null ? definition.Mark : "◇";
            if (slotIcons[i] != null)
            {
                slotIcons[i].sprite = definition != null ? definition.Icon : null;
                slotIcons[i].enabled = definition != null;
                slotMarks[i].enabled = definition == null;
            }
            slotNames[i].text = definition != null ? definition.Name : "Пустая ячейка";
            slots[i].GetComponent<ArtifactCellView>()?.SetState(selectedSlot == i, definition != null);
        }
        int count = 0;
        for (int artifact = 0; artifact < MetaProgression.ArtifactCount; artifact++)
            if (MetaProgression.AvailableArtifactAmount(artifact) > 0 && count < bagContents.Length)
                bagContents[count++] = artifact;
        for (int cell = 0; cell < bag.Length; cell++)
        {
            if (cell >= count) bagContents[cell] = -1;
            MetaArtifactDefinition definition = MetaProgression.GetArtifactDefinition(bagContents[cell]);
            bagMarks[cell].text = definition == null ? "◇" : definition.Mark;
            bagNames[cell].text = definition == null ? "" : definition.Name;
            bag[cell].interactable = definition != null;
            bag[cell].GetComponent<ArtifactCellView>()?.SetState(false, definition != null);
            bag[cell].gameObject.SetActive(cell < Mathf.Max(16, ((count + 3) / 4) * 4));
            if (bagIcons[cell] != null)
            {
                bagIcons[cell].sprite = definition != null ? definition.Icon : null;
                bagIcons[cell].enabled = definition != null;
                bagMarks[cell].enabled = definition == null;
            }
            if (bagQuantities[cell] != null) bagQuantities[cell].text = definition != null
                ? "×" + MetaProgression.AvailableArtifactAmount(bagContents[cell]) : "";

        }
        bagCount.text = "РЮКЗАК  ·  " + count + " ВИДОВ";
        if (contentRect != null) contentRect.sizeDelta = new Vector2(0f, 1100f + Mathf.Max(4, (count + 3) / 4) * 244f);
        equippedCount.text = "НАДЕТО " + equipped + " / " + slots.Length;
        hint.text = selectedSlot >= 0
            ? "Выбрана ячейка " + (selectedSlot + 1) + ". Выбери артефакт из рюкзака."
            : "Выбери ячейку рядом с героем или артефакт в рюкзаке.";
        if (count == 0 && equipped == 0)
            hint.text = "Рюкзак пока пуст. Артефакты продаются в Лесной лавке.";
        if (IsDialogOpen) RefreshDetail();
    }

    private void SelectSlot(int slot)
    {
        selectedSlot = slot;
        int artifact = MetaProgression.GetEquippedArtifact(slot);
        if (artifact >= 0) OpenArtifact(artifact);
        Refresh();
    }

    private void OpenArtifact(int artifact)
    {
        if (!MetaProgression.IsArtifactOwned(artifact)) return;
        hub.CloseDialogs();
        selectedArtifact = artifact;
        dialog.SetActive(true);
        dialog.transform.SetAsLastSibling();
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        MetaArtifactDefinition definition = MetaProgression.GetArtifactDefinition(selectedArtifact);
        if (definition == null) return;
        int currentSlot = MetaProgression.GetArtifactSlot(selectedArtifact);
        int target = selectedSlot >= 0 ? selectedSlot : MetaProgression.GetFirstEmptyEquipmentSlot();
        artifactName.text = definition.Name;
        artifactEffect.text = definition.Description;
        equip.gameObject.SetActive(currentSlot < 0);
        remove.gameObject.SetActive(currentSlot >= 0);
        equip.interactable = target >= 0;
        if (currentSlot >= 0)
            artifactActionHint.text = "Надет в ячейке " + (currentSlot + 1) + ". Бонус активен.";
        else if (target < 0)
            artifactActionHint.text = "Все ячейки заняты. Сними один из надетых артефактов.";
        else
            artifactActionHint.text = "Надеть в ячейку " + (target + 1) + "."
                + (MetaProgression.GetEquippedArtifact(target) >= 0
                    ? "\nПредыдущий артефакт вернётся в рюкзак." : "");
    }

    private void EquipSelected()
    {
        if (!Application.isPlaying) return;
        int target = selectedSlot >= 0 ? selectedSlot : MetaProgression.GetFirstEmptyEquipmentSlot();
        if (!MetaProgression.TryEquipArtifact(selectedArtifact, target)) return;
        selectedSlot = -1;
        CloseDialog();
        Refresh();
    }

    private void RemoveSelected()
    {
        if (!Application.isPlaying) return;
        if (!MetaProgression.TryUnequipArtifact(MetaProgression.GetArtifactSlot(selectedArtifact))) return;
        selectedSlot = -1;
        CloseDialog();
        Refresh();
    }

    private void Build(Transform page)
    {
        RectTransform content = ScrollPage(page, "Hero Equipment Runtime", 2090);
        Label("Eyebrow", content, "ХРАНИТЕЛЬ ЛЕСА", 22, Gold, 44, 16, 992, 32, true);
        Label("Title", content, "Тыква и артефакты", 48, Cream, 44, 52, 992, 70, true);
        Label("Subtitle", content, "Сила героя — в реликвиях, которые ты выбираешь.", 24,
            Muted, 44, 134, 992, 45);
        RectTransform stage = PanelRect("Hero Stage", content, new Color(0.07f, 0.13f, 0.1f),
            252, 214, 576, 614);
        RectTransform portraitRect = Rect("Pumpkin Portrait", stage);
        Top(portraitRect, 16, 16, 544, 528);
        var portrait = portraitRect.gameObject.AddComponent<Image>();
        PlayerVisualProfile profile = Resources.Load<PlayerVisualProfile>("PlayerVisualProfile");
        portrait.sprite = profile != null ? profile.IdleSprite : null;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Label("Hero name", stage, "Т Ы К В А", 34, Gold, 16, 542, 544, 49,
            true, TextAnchor.MiddleCenter);
        for (int slot = 0; slot < slots.Length; slot++)
        {
            int index = slot;
            float x = slot < 3 ? 24 : 856;
            float y = 214 + (slot % 3) * 208;
            slots[slot] = Button("Equipment Slot " + (slot + 1), content, "",
                x, y, 200, 200, () => SelectSlot(index));
            slotMarks[slot] = Label("Symbol", slots[slot].transform, "+", 43, Gold,
                6, 9, 132, 60, true, TextAnchor.MiddleCenter);
            slotNames[slot] = Label("Artifact Name", slots[slot].transform, "ПУСТО", 19, Muted,
                6, 73, 132, 62, false, TextAnchor.MiddleCenter);
        }
        equippedCount = Label("Equipped Count", content, "", 21, Gold, 252, 848, 576, 34,
            true, TextAnchor.MiddleCenter);
        hint = Label("Hint", content, "", 23, Muted, 44, 899, 992, 64, false, TextAnchor.MiddleCenter);
        bagCount = Label("Backpack Title", content, "", 30, Gold, 44, 992, 670, 46, true);
        Button("Visit Forest Shop", content, "В ДЕРЕВНЮ →", 744, 990, 292, 52,
            () => hub.SelectTab(2), 22);
        for (int cell = 0; cell < bag.Length; cell++)
        {
            int index = cell;
            float x = 44 + (cell % 4) * 252;
            float y = 1064 + (cell / 4) * 244;
            bag[cell] = Button("Backpack Cell " + (cell + 1), content, "", x, y, 228, 228,
                () => OpenArtifact(bagContents[index]));
            bag[cell].image.color = new Color(0.15f, 0.23f, 0.18f);
            var colors = bag[cell].colors;
            colors.disabledColor = Color.white;
            bag[cell].colors = colors;
            bagMarks[cell] = Label("Symbol", bag[cell].transform, "", 43, Gold,
                10, 28, 208, 84, true, TextAnchor.MiddleCenter);
            bagNames[cell] = Label("Artifact Name", bag[cell].transform, "", 21, Cream,
                10, 122, 208, 86, false, TextAnchor.MiddleCenter);
        }
        RectTransform panel = Modal(page, "Artifact Details", 500, hub.CloseDialogs, out dialog);
        artifactName = Label("Name", panel, "", 39, Gold, 36, 78, 872, 70, true);
        artifactEffect = Label("Effect", panel, "", 31, Cream, 36, 172, 908, 78);
        artifactActionHint = Label("Action Hint", panel, "", 25, Muted, 36, 275, 908, 82);
        equip = Button("Equip", panel, "НАДЕТЬ", 36, 392, 908, 72, EquipSelected, 29);
        remove = Button("Unequip", panel, "СНЯТЬ В РЮКЗАК", 36, 392, 908, 72, RemoveSelected, 29);
        ApplyCenteredSlotLayout(content);
#if UNITY_EDITOR
        for (int i = 0; i < slots.Length; i++)
        {
            ArtifactCellView.Author(slots[i], true, i + 1);
            slotIcons[i] = slots[i].transform.Find("Icon").GetComponent<Image>();
        }
        for (int i = 0; i < bag.Length; i++)
        {
            ArtifactCellView.Author(bag[i], false, i + 1);
            bagIcons[i] = bag[i].transform.Find("Icon").GetComponent<Image>();
            bagQuantities[i] = bag[i].transform.Find("Quantity").GetComponent<Text>();
        }
#endif
    }
}

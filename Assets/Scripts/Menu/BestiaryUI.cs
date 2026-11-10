using UnityEngine;
using UnityEngine.UI;

// All cards and artwork are authored in UIscene. Runtime only binds controls and changes pages.
[DisallowMultipleComponent]
public sealed class BestiaryUI : MonoBehaviour
{
    private MenuHubController hub;
    private Button[] tabs;
    private GameObject[] lists;
    private ScrollRect[] listScrolls;
    private BestiaryEntryView[] entries;
    private BestiaryEntryView selected;
    private Image portrait;
    private Text title;
    private Text subtitle;
    private Text body;
    private ScrollRect bodyScroll;
    private int activeSection;

    public bool IsOpen => gameObject.activeSelf;

    public static BestiaryUI Ensure(MenuHubController owner)
    {
        if (owner.tabs == null || owner.tabs.Length < 3 || owner.tabs[2].page == null)
        {
            return null;
        }

        Transform page = owner.tabs[2].page.transform;
        BestiaryUI view = page.Find("Bestiary Menu")?.GetComponent<BestiaryUI>();
        Transform open = page.Find("Forest Village Runtime/Viewport/Content/Village Field/Open Bestiary");
        if (view == null || open == null)
        {
            Debug.LogError("Editable bestiary is missing from UIscene. Bake the bestiary into the menu scene.");
            return null;
        }
        view.Bind(owner, open.GetComponent<Button>());
        return view;
    }

    private void Bind(MenuHubController owner, Button open)
    {
        hub = owner;
        Transform panel = transform.Find("Panel");
        tabs = new[] {
            panel.Find("Category Tabs/Locations").GetComponent<Button>(),
            panel.Find("Category Tabs/Enemies").GetComponent<Button>(),
            panel.Find("Category Tabs/Projectiles").GetComponent<Button>()
        };
        lists = new[] {
            panel.Find("Entry Lists/Locations").gameObject,
            panel.Find("Entry Lists/Enemies").gameObject,
            panel.Find("Entry Lists/Projectiles").gameObject
        };
        listScrolls = new ScrollRect[lists.Length];
        for (int i = 0; i < lists.Length; i++)
        {
            listScrolls[i] = lists[i].GetComponent<ScrollRect>();
        }

        Transform detail = panel.Find("Detail");
        portrait = detail.Find("Portrait").GetComponent<Image>();
        title = detail.Find("Title").GetComponent<Text>();
        subtitle = detail.Find("Subtitle").GetComponent<Text>();
        bodyScroll = detail.Find("Body Scroll").GetComponent<ScrollRect>();
        body = bodyScroll.content.GetComponent<Text>();
        entries = GetComponentsInChildren<BestiaryEntryView>(true);
        foreach (BestiaryEntryView entry in entries)
        {
            entry.Bind(SelectEntry);
        }

        open.onClick.RemoveAllListeners();
        open.onClick.AddListener(Show);
        Button close = panel.Find("Close").GetComponent<Button>();
        close.onClick.RemoveAllListeners();
        close.onClick.AddListener(owner.CloseDialogs);
        Button shade = GetComponent<Button>();
        shade.onClick.RemoveAllListeners();
        shade.onClick.AddListener(owner.CloseDialogs);
        for (int i = 0; i < tabs.Length; i++)
        {
            int section = i;
            tabs[i].onClick.RemoveAllListeners();
            tabs[i].onClick.AddListener(() => SelectSection(section));
        }
        Close();
    }

    public void Show()
    {
        hub.CloseDialogs();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SelectSection(0);
    }

    public void Close() => gameObject.SetActive(false);

    private void SelectSection(int index)
    {
        if (selected != null)
        {
            selected.SetSelected(false);
        }

        selected = null;
        activeSection = Mathf.Clamp(index, 0, lists.Length - 1);
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i].SetActive(i == activeSection);
            tabs[i].image.color = i == activeSection ? VillageUIStyle.Gold : VillageUIStyle.Green;
            tabs[i].GetComponentInChildren<Text>().color = i == activeSection
                ? VillageUIStyle.Background : VillageUIStyle.Cream;
            if (i == activeSection)
            {
                listScrolls[i].verticalNormalizedPosition = 1f;
            }
        }
        foreach (BestiaryEntryView entry in entries)
        {
            if ((int)entry.Category == activeSection)
            {
                SelectEntry(entry);
                break;
            }
        }
    }

    private void SelectEntry(BestiaryEntryView entry)
    {
        if (entry == null)
        {
            return;
        }

        if (selected != null)
        {
            selected.SetSelected(false);
        }

        selected = entry;
        selected.SetSelected(true);
        portrait.sprite = entry.Portrait;
        portrait.color = entry.Portrait != null ? Color.white : Color.clear;
        title.text = entry.Title;
        subtitle.text = entry.Subtitle;
        body.text = entry.Body;
        RectTransform textRect = (RectTransform)body.transform;
        textRect.sizeDelta = new Vector2(0f,
            Mathf.Max(((RectTransform)bodyScroll.viewport).rect.height,
                body.preferredHeight + 32f));
        bodyScroll.verticalNormalizedPosition = 1f;
    }
}

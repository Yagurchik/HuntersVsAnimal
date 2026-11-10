using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class BestiaryEntryView : MonoBehaviour
{
    public enum Section { Locations, Enemies, Projectiles }

    [SerializeField] private Section section;
    [SerializeField] private string entryTitle;
    [SerializeField] private string entrySubtitle;
    [SerializeField, TextArea(3, 16)] private string entryBody;
    [SerializeField] private Sprite portrait;

    public Section Category => section;
    public string Title => entryTitle;
    public string Subtitle => entrySubtitle;
    public string Body => entryBody;
    public Sprite Portrait => portrait;

    public void Configure(Section category, string title, string subtitle, string body, Sprite icon)
    {
        section = category;
        entryTitle = title;
        entrySubtitle = subtitle;
        entryBody = body;
        portrait = icon;
        RefreshPreview();
    }

    public void Bind(Action<BestiaryEntryView> selected)
    {
        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => selected?.Invoke(this));
        RefreshPreview();
    }

    public void SetSelected(bool value)
    {
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = value
                ? new Color(0.33f, 0.38f, 0.24f, 1f)
                : VillageUIStyle.Panel;
        }
    }

    private void RefreshPreview()
    {
        Transform title = transform.Find("Name");
        if (title != null)
        {
            title.GetComponent<Text>().text = entryTitle;
        }

        Transform subtitle = transform.Find("Subtitle");
        if (subtitle != null)
        {
            subtitle.GetComponent<Text>().text = entrySubtitle;
        }

        Transform icon = transform.Find("Portrait");
        if (icon != null)
        {
            Image image = icon.GetComponent<Image>();
            image.sprite = portrait;
            image.color = portrait != null ? Color.white : Color.clear;
        }
    }
}

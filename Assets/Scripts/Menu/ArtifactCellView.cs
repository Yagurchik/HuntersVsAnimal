using UnityEngine;
using UnityEngine.UI;

// References to authored graphics only; the runtime never constructs inventory cells.
public sealed class ArtifactCellView : MonoBehaviour
{
    [SerializeField] private Image frame;
    [SerializeField] private Image well;
    [SerializeField] private Image selection;
    public const int LayoutVersion = 1;
    [SerializeField] private int layoutVersion = LayoutVersion;
    public bool IsCurrent => layoutVersion == LayoutVersion && frame != null && frame.sprite != null
        && well != null && selection != null;

    public void SetState(bool selected, bool occupied)
    {
        frame.color = selected ? new Color(1f, 0.91f, 0.61f) : Color.white;
        well.color = occupied ? new Color(0.075f, 0.12f, 0.09f) : new Color(0.045f, 0.075f, 0.055f);
        selection.enabled = selected;
    }

#if UNITY_EDITOR
    public static void Author(Button button, bool equipment, int number)
    {
        Transform root = button.transform;
        float size = equipment ? 200f : 228f;
        Image background = button.GetComponent<Image>();
        background.color = new Color(0.045f, 0.075f, 0.055f);
        background.raycastTarget = true;
        Image frameImage = ImageChild(root, "Carved Frame");
        VillageUIStyle.Stretch((RectTransform)frameImage.transform);
        frameImage.sprite = Resources.Load<Sprite>("UI/ArtifactSlotFrame-v1");
        frameImage.type = Image.Type.Sliced;
        frameImage.raycastTarget = false;
        frameImage.transform.SetAsFirstSibling();

        Image selected = ImageChild(root, "Selected Edge");
        VillageUIStyle.Stretch((RectTransform)selected.transform);
        selected.sprite = Resources.Load<Sprite>("UI/ArtifactSlotFrame-v1");
        selected.type = Image.Type.Sliced;
        selected.color = new Color(1f, 0.76f, 0.30f, 0.44f);
        selected.raycastTarget = false;
        selected.enabled = false;
        selected.transform.SetSiblingIndex(1);

        Image icon = ImageChild(root, "Icon");
        VillageUIStyle.Top((RectTransform)icon.transform, 33f, 31f, size - 66f, size - 86f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Color.white;
        Text symbol = root.Find("Symbol").GetComponent<Text>();
        VillageUIStyle.Top((RectTransform)symbol.transform, 38f, 43f, size - 76f, size - 110f);
        symbol.fontSize = 38;
        symbol.color = new Color(0.27f, 0.35f, 0.24f);
        symbol.alignment = TextAnchor.MiddleCenter;
        Text name = root.Find("Artifact Name").GetComponent<Text>();
        VillageUIStyle.Top((RectTransform)name.transform, 23f, size - 56f, size - 46f, 37f);
        name.fontSize = equipment ? 19 : 22;
        name.resizeTextMinSize = equipment ? 14 : 16;
        name.resizeTextMaxSize = name.fontSize;
        name.color = VillageUIStyle.Cream;
        name.alignment = TextAnchor.MiddleCenter;
        if (equipment)
        {
            Text index = TextChild(root, "Slot Number");
            VillageUIStyle.Top((RectTransform)index.transform, size - 45f, 16f, 26f, 27f);
            index.text = number.ToString("00");
            index.fontSize = 17;
            index.resizeTextMaxSize = 17;
            index.color = VillageUIStyle.Gold;
        }
        else
        {
            Text quantity = TextChild(root, "Quantity");
            VillageUIStyle.Top((RectTransform)quantity.transform, size - 82f, 20f, 56f, 30f);
            quantity.fontSize = 22;
            quantity.resizeTextMaxSize = 22;
            quantity.color = VillageUIStyle.Cream;
            quantity.alignment = TextAnchor.MiddleRight;
        }
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
        colors.pressedColor = new Color(0.75f, 0.68f, 0.50f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.targetGraphic = frameImage;
        ArtifactCellView view = button.GetComponent<ArtifactCellView>()
            ?? button.gameObject.AddComponent<ArtifactCellView>();
        view.frame = frameImage;
        view.well = background;
        view.selection = selected;
        view.layoutVersion = LayoutVersion;
        view.SetState(false, false);
    }

    private static Image ImageChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Image>()
            : VillageUIStyle.Rect(name, parent).gameObject.AddComponent<Image>();
    }

    private static Text TextChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Text>()
            : VillageUIStyle.Label(name, parent, "", 22, VillageUIStyle.Cream, 0, 0, 30, 30);
    }
#endif
}

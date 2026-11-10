using System;
using UnityEngine;
using UnityEngine.UI;

public enum VillageBuildingKind { GreatTree, ForestShop }

[DisallowMultipleComponent]
public sealed class VillageBuildingView : MonoBehaviour
{
    public VillageBuildingKind kind;
    public Image artwork;
    public Text nameLabel;
    public Text levelLabel;
    public Button openButton;
    private Action<VillageBuildingKind> open;

    public void Bind(Action<VillageBuildingKind> onOpen)
    {
        open = onOpen;
        openButton.onClick.RemoveListener(Open);
        openButton.onClick.AddListener(Open);
        Refresh();
    }

    public void Open()
    {
        open?.Invoke(kind);
    }

    public void Refresh()
    {
        bool tree = kind == VillageBuildingKind.GreatTree;
        if (nameLabel != null) nameLabel.text = tree ? "ВЕЧНОЕ ДРЕВО" : "ЛЕСНАЯ ЛАВКА";
        if (levelLabel != null) levelLabel.text = "УРОВЕНЬ "
            + (tree ? MetaProgression.GreatTreeLevel : MetaProgression.ForestShopLevel) + "  ·  ОТКРЫТЬ";
    }
}

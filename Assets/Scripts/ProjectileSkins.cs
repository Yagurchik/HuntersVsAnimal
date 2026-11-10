using System;
using UnityEngine;

// Permanent cosmetics are independent from run upgrades and projectile availability.
public static class ProjectileSkins
{
    private static ProjectileSkinCatalog catalogue;
    public static event Action Changed;
    public static ProjectileSkinCatalog Catalogue => catalogue != null ? catalogue
        : catalogue = Resources.Load<ProjectileSkinCatalog>("ProjectileSkins/Catalogue");
    public static int Count => Catalogue != null && Catalogue.Skins != null ? Catalogue.Skins.Length : 0;
    public static ProjectileSkinCatalog.Skin Get(int index) => index >= 0 && index < Count ? Catalogue.Skins[index] : null;
    private static string OwnedKey(ProjectileSkinCatalog.Skin skin) => "ForestAnimalWar.Skins.Owned." + skin.Id;
    private static string EquippedKey(ProjectileSkinCatalog.Skin skin) => "ForestAnimalWar.Skins.Equipped." + skin.Id;
    public static bool IsOwned(int index) => Get(index) is ProjectileSkinCatalog.Skin skin
        && PlayerPrefs.GetInt(OwnedKey(skin), 0) == 1;
    public static bool IsEquipped(int index) => IsOwned(index)
        && PlayerPrefs.GetInt(EquippedKey(Get(index)), 0) == 1;
    public static Sprite EquippedArtwork(int index) => IsEquipped(index) ? Get(index).Artwork : null;
    public static int IndexOf(GameObject prefab)
    {
        if (prefab == null)
        {
            return -1;
        }

        var visual = prefab.GetComponent<ProjectileSkinVisual>();
        return visual != null ? visual.ProjectileIndex : -1;
    }
    public static Sprite EquippedArtwork(GameObject prefab) => EquippedArtwork(IndexOf(prefab));

    public static bool TryBuy(int index)
    {
        var skin = Get(index);
        if (skin == null || skin.Artwork == null || skin.DiamondPrice <= 0 || IsOwned(index)
            || MetaProgression.Diamonds < skin.DiamondPrice)
        {
            return false;
        }

        PlayerPrefs.SetInt(OwnedKey(skin), 1);
        PlayerPrefs.SetInt(EquippedKey(skin), 1);
        // No callbacks run between checking the balance and spending it. The
        // wallet saves ownership and payment together before notifying views.
        if (!MetaProgression.TrySpendDiamonds(skin.DiamondPrice))
        {
            PlayerPrefs.DeleteKey(OwnedKey(skin));
            PlayerPrefs.DeleteKey(EquippedKey(skin));
            return false;
        }
        Changed?.Invoke();
        return true;
    }

    public static bool TryEquip(int index, bool equipped)
    {
        if (!IsOwned(index))
        {
            return false;
        }

        PlayerPrefs.SetInt(EquippedKey(Get(index)), equipped ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }
}

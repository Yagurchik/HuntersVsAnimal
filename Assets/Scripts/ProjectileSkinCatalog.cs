using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Forest Animal War/Projectile skin catalogue")]
public sealed class ProjectileSkinCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Skin
    {
        public string Id;
        public string Name;
        public string ProjectileName;
        [Min(1)] public int DiamondPrice = 30;
        public Sprite Artwork;
        public Sprite OriginalIcon;
        public GameObject ProjectilePrefab;
    }

    public Skin[] Skins;
}

using UnityEngine;

[CreateAssetMenu(menuName = "Forest Animal War/UI/Animal", fileName = "MenuAnimal")]
public class MenuAnimalDefinition : ScriptableObject
{
    [Range(0, 3)] public int animalIndex;
    public string displayName;
    public string role;
    [TextArea(2, 5)] public string description;
    public Color accent = Color.white;
    public Sprite portrait;
}

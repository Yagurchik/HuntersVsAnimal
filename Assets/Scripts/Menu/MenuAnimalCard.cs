using UnityEngine;

// Kept only so existing serialized scenes import without missing-script warnings.
// The permanent animal-upgrade feature has been retired.
public sealed class MenuAnimalCard : MonoBehaviour
{
    private void Awake() { gameObject.SetActive(false); }
}

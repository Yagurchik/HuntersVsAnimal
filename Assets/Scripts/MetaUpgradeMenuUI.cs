using UnityEngine;

// Legacy serialized marker. It no longer creates or sells permanent upgrades.
public sealed class MetaUpgradeMenuUI : MonoBehaviour
{
    private void Awake() { gameObject.SetActive(false); }
}

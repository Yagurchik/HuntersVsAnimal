using UnityEngine;

// Authored scene metadata only; the village is never migrated at runtime.
[DisallowMultipleComponent, AddComponentMenu("")]
public sealed class VillageEntityLayout : MonoBehaviour
{
    public const int CurrentVersion = 1;
    [SerializeField, HideInInspector] private int version;
    public int Version => version;

#if UNITY_EDITOR
    public void RecordCurrentVersion() => version = CurrentVersion;
#endif
}

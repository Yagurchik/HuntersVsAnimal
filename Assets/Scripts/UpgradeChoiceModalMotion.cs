using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UpgradeChoiceModalMotion : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.18f;
    private CanvasGroup group;
    private float elapsed;

    private void Awake() => group = GetComponent<CanvasGroup>();

    private void OnEnable()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        elapsed = 0f;
        group.alpha = Application.isPlaying ? 0f : 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void Update()
    {
        if (!Application.isPlaying || group.alpha >= 1f) return;
        elapsed += Time.unscaledDeltaTime;
        group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
    }
}

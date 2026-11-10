using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored combat HUD banner for an approaching enemy wave.</summary>
public sealed class WaveWarningUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text label;
    private Coroutine warningAnimation;
    private static WaveWarningUI instance;
    private static bool missingWarningLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        missingWarningLogged = false;
    }

    public static bool Show(int defenseLine, int waveNumber, bool endless = false)
    {
        if (instance == null) instance = FindObjectOfType<WaveWarningUI>(true);
        if (instance == null || instance.canvasGroup == null || instance.label == null)
        {
            if (!missingWarningLogged)
            {
                Debug.LogWarning("Wave Warning HUD is missing from the gameplay scene.");
                missingWarningLogged = true;
            }
            return false;
        }
        instance.ShowMessage(defenseLine, waveNumber, endless);
        return true;
    }

    public void Configure(CanvasGroup group, Text message)
    {
        canvasGroup = group;
        label = message;
    }

    private void Awake()
    {
        instance = this;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void ShowMessage(int defenseLine, int waveNumber, bool endless)
    {
        if (warningAnimation != null) StopCoroutine(warningAnimation);
        label.text = endless
            ? "ЛИНИЯ " + defenseLine + "  ·  ВОЛНА " + waveNumber + " ПРИБЛИЖАЕТСЯ"
            : "ЛИНИЯ " + defenseLine + "  ·  ВОЛНА " + waveNumber + "/"
                + LevelProgression.CampaignWavesPerRoom + " ПРИБЛИЖАЕТСЯ";
        warningAnimation = StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        yield return Fade(0f, 1f, 0.08f);
        yield return new WaitForSecondsRealtime(0.55f);
        yield return Fade(1f, 0f, 0.15f);
        warningAnimation = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

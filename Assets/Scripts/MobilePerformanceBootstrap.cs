using UnityEngine;

/// <summary>
/// Applies a predictable frame-rate target on Android. Mobile platforms ignore
/// QualitySettings.vSyncCount, so Application.targetFrameRate must be set explicitly.
/// </summary>
public static class MobilePerformanceBootstrap
{
    public const int AndroidTargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureFrameRate()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            return;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = AndroidTargetFrameRate;
        Screen.orientation = ScreenOrientation.Portrait;
    }
}

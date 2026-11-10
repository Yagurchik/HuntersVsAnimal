using UnityEngine;

public enum GameRunType
{
    Campaign = 0,
    Survival = 1,
    // Clock siege: an endless variant of survival with 12 fronts around one
    // central barricade. It reuses every survival rule unless the clock
    // director overrides it.
    Clock = 2
}

// Keeps the selected run type independent from campaign progression. The requested
// value is consumed by the gameplay scene, just like the requested campaign level.
public static class GameRunMode
{
    public const string RequestedModeKey = "ForestAnimalWar.RequestedRunMode";
    private const string DeveloperModeKey = "ForestAnimalWar.DeveloperMode";
    private const string RequestedDeveloperRunKey = "ForestAnimalWar.RequestedDeveloperRun";
    public const string LastSurvivalTurnsKey = "ForestAnimalWar.Survival.LastTurns";
    public const string BestSurvivalTurnsKey = "ForestAnimalWar.Survival.BestTurns";
    public const string LastClockTurnsKey = "ForestAnimalWar.Clock.LastTurns";
    public const string BestClockTurnsKey = "ForestAnimalWar.Clock.BestTurns";

    public static GameRunType Current { get; private set; } = GameRunType.Campaign;
    // Both endless modes share the survival rules (no victory, records, start upgrades).
    public static bool IsSurvival => Current == GameRunType.Survival || Current == GameRunType.Clock;
    // Survival now plays by the clock siege rules (12 fronts around one barricade).
    public static bool IsClock => Current == GameRunType.Survival || Current == GameRunType.Clock;
    public static bool DeveloperModeEnabled => PlayerPrefs.GetInt(DeveloperModeKey, 0) == 1;
    public static bool IsDeveloperRun { get; private set; }
    public static int LastSurvivalTurns => Mathf.Max(0, PlayerPrefs.GetInt(LastSurvivalTurnsKey, 0));
    public static int BestSurvivalTurns => Mathf.Max(0, PlayerPrefs.GetInt(BestSurvivalTurnsKey, 0));
    public static int LastClockTurns => Mathf.Max(0, PlayerPrefs.GetInt(LastClockTurnsKey, 0));
    public static int BestClockTurns => Mathf.Max(0, PlayerPrefs.GetInt(BestClockTurnsKey, 0));
    public static int BestTurnsForCurrentMode => Current == GameRunType.Clock ? BestClockTurns : BestSurvivalTurns;

    public static void PrepareCampaign()
    {
        Prepare(GameRunType.Campaign);
    }

    public static void PrepareSurvival()
    {
        // Survival uses the basic Twilight Thicket balance, deploys all four
        // lines immediately and then applies its own per-turn scaling.
        LevelProgression.PrepareLevel(1);
        Prepare(GameRunType.Survival);
    }

    public static void PrepareClock()
    {
        // The clock siege also uses the Twilight Thicket enemies and balance.
        LevelProgression.PrepareLevel(1);
        Prepare(GameRunType.Clock);
    }

    public static void PrepareReplay()
    {
        Prepare(Current, IsDeveloperRun);
        if (IsSurvival)
        {
            LevelProgression.PrepareLevel(1);
        }
    }

    public static void SetDeveloperMode(bool enabled)
    {
        PlayerPrefs.SetInt(DeveloperModeKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void BeginGameplay()
    {
        int requested = PlayerPrefs.GetInt(RequestedModeKey, (int)GameRunType.Campaign);
        Current = requested == (int)GameRunType.Survival ? GameRunType.Survival
            : requested == (int)GameRunType.Clock ? GameRunType.Clock
            : GameRunType.Campaign;
        IsDeveloperRun = !IsSurvival && PlayerPrefs.GetInt(RequestedDeveloperRunKey, 0) == 1;
        PlayerPrefs.DeleteKey(RequestedModeKey);
        PlayerPrefs.DeleteKey(RequestedDeveloperRunKey);
        PlayerPrefs.Save();
    }

    // Records the result of the current endless mode in its own keys.
    public static int RecordSurvivalScore(int turns)
    {
        int safeTurns = Mathf.Max(0, turns);
        string lastKey = Current == GameRunType.Clock ? LastClockTurnsKey : LastSurvivalTurnsKey;
        string bestKey = Current == GameRunType.Clock ? BestClockTurnsKey : BestSurvivalTurnsKey;
        int best = Mathf.Max(Mathf.Max(0, PlayerPrefs.GetInt(bestKey, 0)), safeTurns);
        PlayerPrefs.SetInt(lastKey, safeTurns);
        PlayerPrefs.SetInt(bestKey, best);
        PlayerPrefs.Save();
        return best;
    }

    private static void Prepare(GameRunType mode)
    {
        Prepare(mode, mode == GameRunType.Campaign && DeveloperModeEnabled);
    }

    private static void Prepare(GameRunType mode, bool developerRun)
    {
        Current = mode;
        IsDeveloperRun = mode == GameRunType.Campaign && developerRun;
        PlayerPrefs.SetInt(RequestedModeKey, (int)mode);
        PlayerPrefs.SetInt(RequestedDeveloperRunKey, IsDeveloperRun ? 1 : 0);
        PlayerPrefs.Save();
    }
}

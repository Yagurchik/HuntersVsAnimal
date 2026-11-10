using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuHubController : MonoBehaviour
{
    [Serializable]
    public class LevelEncounterPreview
    {
        [Tooltip("Enemy prefabs shown on this campaign level, including its bosses.")]
        public GameObject[] enemies;
    }

    [Serializable]
    public class Tab
    {
        public string title;
        public GameObject page;
        public Image buttonBackground;
        public Text buttonLabel;
        public GameObject selectedMark;
    }

    [Header("Pages — existing scene objects")]
    public Tab[] tabs;
    [Range(0, 3)] public int initialTab;
    [SerializeField] private int gameplaySceneBuildIndex = 1;
    [SerializeField] private ProjectileLoadoutUI projectileLoadoutUI;
    [Header("Header and campaign")]
    public Text currencyText;
    public Text goldText;
    public Text seedsText;
    public Text diamondsText;
    [Tooltip("Editable title of the currently browsed five-level location.")]
    public Text locationText;
    [Tooltip("Authored swamp illustration in UIscene; shown only for location three.")]
    public GameObject drownedBogIllustration;
    [Tooltip("Authored Forgotten Bay illustration in UIscene; shown only for location four.")]
    public GameObject forgottenBayIllustration;
    [Tooltip("Authored Snowy Haze illustration in UIscene; shown only for location five.")]
    public GameObject snowyHazeIllustration;
    [Tooltip("Authored Fiery Gehenna illustration in UIscene; shown only for location six.")]
    public GameObject fieryGehennaIllustration;
    [Tooltip("The authored forest illustration, shown only for location one.")]
    public GameObject forestIllustration;
    [Tooltip("Authored Gloomy Hollow illustration in UIscene; shown only for location two.")]
    public GameObject gloomyHollowIllustration;
    public Button previousLocationButton;
    public Button nextLocationButton;
    public Text campaignText;
    public Text playText;
    public Text survivalPlayText;
    public Text survivalBestText;
    public Text victoriesText;
    public MenuLevelCard[] levels;
    [Tooltip("Editable encounter portraits for all 30 campaign levels, in level order.")]
    public LevelEncounterPreview[] encounterPreviews;
    [Tooltip("Editable enemy preview on top of the current location illustration.")]
    public Text encounterTitle;
    public Image[] encounterPortraits;
    public Image[] encounterFrames;
    public Text[] encounterNames;
    public MenuEventCard[] events;
    public GameObject eventBadge;
    [Header("Information dialog")]
    public GameObject informationModal;
    public Text informationTitle;
    public Text informationBody;
    [Header("Settings dialog")]
    public GameObject settingsModal;
    public Text soundButtonText;
    public Text developerModeButtonText;
    [Header("Tab colors")]
    public Color activeTabColor = new Color(0.22f, 0.34f, 0.27f);
    public Color inactiveTabColor = new Color(0.055f, 0.11f, 0.1f);

    private ForestVillageUI village;
    private BestiaryUI bestiary;
    private ProjectileSkinShopUI skinShop;
    private HeroEquipmentUI equipment;
    private bool loading;
    private const string MutedKey = "ForestAnimalWar.AudioMuted";
    public int SelectedTab { get; private set; }
    public int SelectedLocationIndex { get; private set; }

    public LevelEncounterPreview EncounterPreviewFor(int globalLevel)
    {
        int index = globalLevel - 1;
        return encounterPreviews != null && index >= 0 && index < encounterPreviews.Length
            ? encounterPreviews[index] : null;
    }

    public void RefreshEncounterPreview(int globalLevel)
    {
        LevelEncounterPreview preview = EncounterPreviewFor(globalLevel);
        if (encounterTitle != null)
            encounterTitle.text = "ВРАГИ УРОВНЯ "
                + LevelProgression.LocalLevelFor(globalLevel).ToString("00");
        for (int i = 0; encounterPortraits != null && i < encounterPortraits.Length; i++)
        {
            Image portrait = encounterPortraits[i];
            if (portrait == null) continue;
            GameObject enemy = preview != null && preview.enemies != null
                && i < preview.enemies.Length ? preview.enemies[i] : null;
            EnemyFirstEncounterProfile profile = enemy != null
                ? enemy.GetComponent<EnemyFirstEncounterProfile>() : null;
            Sprite sprite = profile != null ? profile.Portrait : null;
            if (sprite == null && enemy != null)
            {
                Transform visual = enemy.transform.Find("Visual");
                SpriteRenderer renderer = visual != null
                    ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
                sprite = renderer != null ? renderer.sprite : null;
            }
            bool visible = sprite != null;
            portrait.sprite = sprite;
            portrait.gameObject.SetActive(visible);
            if (encounterFrames != null && i < encounterFrames.Length
                && encounterFrames[i] != null)
            {
                Image frame = encounterFrames[i];
                frame.gameObject.SetActive(visible);
                bool boss = enemy != null && enemy.TryGetComponent(out Brick brick)
                    && brick.UsesBossHealth;
                frame.color = boss
                    ? new Color(0.72f, 0.49f, 0.16f, 0.98f)
                    : new Color(0.16f, 0.3f, 0.23f, 0.98f);
            }
            if (encounterNames != null && i < encounterNames.Length
                && encounterNames[i] != null)
                encounterNames[i].text = profile != null ? profile.DisplayName
                    : enemy != null ? enemy.name : string.Empty;
        }
    }

    private void OnEnable()
    {
        MetaProgression.Changed += Refresh;
    }
    private void OnDisable()
    {
        MetaProgression.Changed -= Refresh;
    }
    private void Start()
    {
        MetaProgression.NormalizeEquipmentCapacity();
        SelectedLocationIndex = LevelProgression.SelectedMenuLocation;
        PrepareVillageAndHero();
        CloseDialogs();
        SelectTab(initialTab);
        AudioListener.volume = PlayerPrefs.GetInt(MutedKey, 0) == 1 ? 0f : 1f;
        Refresh();
    }

    private void Update()
    {
        if (village != null) village.Tick();
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsDialogOpen) CloseDialogs();
        else SelectTab(0);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && village != null) village.SavePassiveIncome();
    }

    private void OnApplicationQuit()
    {
        if (village != null) village.SavePassiveIncome();
    }

    public bool IsDialogOpen => (village != null && village.IsDialogOpen)
        || (skinShop != null && skinShop.IsOpen)
        || (bestiary != null && bestiary.IsOpen)
        || (equipment != null && equipment.IsDialogOpen)
        || (informationModal != null && informationModal.activeSelf)
        || (settingsModal != null && settingsModal.activeSelf)
        || (projectileLoadoutUI != null && projectileLoadoutUI.IsOpen);

    public void SelectTab(int index)
    {
        if (tabs == null || index < 0 || index >= tabs.Length || loading) return;
        CloseDialogs();
        SelectedTab = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool selected = i == index;
            if (tabs[i].page != null) tabs[i].page.SetActive(selected);
            if (tabs[i].selectedMark != null) tabs[i].selectedMark.SetActive(selected);
            if (tabs[i].buttonBackground != null) tabs[i].buttonBackground.color = selected ? activeTabColor : inactiveTabColor;
            if (tabs[i].buttonLabel != null) tabs[i].buttonLabel.color = selected ? new Color(1f, 0.85f, 0.49f) : new Color(0.64f, 0.73f, 0.67f);
        }
        ClearSelection();
        if (Application.isPlaying) Refresh();
    }

    public void Refresh()
    {
        if (SelectedLocationIndex < 1 || SelectedLocationIndex > LevelProgression.LocationCount)
            SelectedLocationIndex = LevelProgression.SelectedMenuLocation;
        if (currencyText != null) currencyText.text = ForestVillageUI.FormatAmount(MetaProgression.AcornsExact);
        if (goldText != null) goldText.text = ForestVillageUI.FormatAmount(MetaProgression.GoldExact);
        if (seedsText != null) seedsText.text = MetaProgression.Seeds.ToString();
        if (diamondsText != null) diamondsText.text = MetaProgression.Diamonds.ToString();
        int selected = LevelProgression.SelectedMenuLevel;
        int localLevel = LevelProgression.LocalLevelFor(selected);
        int unlockedHere = GameRunMode.DeveloperModeEnabled
            ? LevelProgression.LevelsPerLocation
            : Mathf.Clamp(LevelProgression.HighestUnlockedLevel
            - (SelectedLocationIndex - 1) * LevelProgression.LevelsPerLocation,
            0, LevelProgression.LevelsPerLocation);
        string locationName = LevelProgression.LocationNameForIndex(SelectedLocationIndex);
        if (drownedBogIllustration != null)
            drownedBogIllustration.SetActive(SelectedLocationIndex == 3);
        if (forgottenBayIllustration != null)
            forgottenBayIllustration.SetActive(SelectedLocationIndex == 4);
        if (snowyHazeIllustration != null)
            snowyHazeIllustration.SetActive(SelectedLocationIndex == 5);
        if (fieryGehennaIllustration != null)
            fieryGehennaIllustration.SetActive(SelectedLocationIndex == 6);
        if (forestIllustration != null)
            forestIllustration.SetActive(SelectedLocationIndex == 1);
        if (gloomyHollowIllustration != null)
            gloomyHollowIllustration.SetActive(SelectedLocationIndex == 2);
        if (locationText != null) locationText.text = locationName.ToUpperInvariant();
        if (campaignText != null) campaignText.text = locationName.ToUpperInvariant()
            + "   ·   УРОВЕНЬ " + localLevel.ToString("00") + " / " + LevelProgression.LevelsPerLocation
            + "   ·   " + unlockedHere + " ОТКРЫТО";
        RefreshEncounterPreview(selected);
        if (playText != null && !loading) playText.text = "В БОЙ  ·  УРОВЕНЬ " + localLevel;
        if (previousLocationButton != null)
            previousLocationButton.interactable = SelectedLocationIndex > 1;
        if (nextLocationButton != null)
            nextLocationButton.interactable = SelectedLocationIndex < LevelProgression.LocationCount;
        if (survivalPlayText != null && !loading) survivalPlayText.text = "НАЧАТЬ ВЫЖИВАНИЕ";
        if (survivalBestText != null) survivalBestText.text = GameRunMode.BestSurvivalTurns > 0
            ? "РЕКОРД  ·  " + GameRunMode.BestSurvivalTurns + " ХОДОВ"
            : "РЕКОРД  ·  ЕЩЁ НЕ УСТАНОВЛЕН";
        if (victoriesText != null) victoriesText.text = "ПОБЕДЫ: " + MetaProgression.TotalVictories
            + "   ·   ЗОЛОТО: " + MetaProgression.Gold;
        if (levels != null) foreach (var card in levels)
        {
            if (card == null) continue;
            bool belongsToLocation = card.levelNumber <= LevelProgression.LevelsPerLocation;
            card.gameObject.SetActive(belongsToLocation);
            if (belongsToLocation) card.Refresh(SelectedLocationIndex);
        }
        bool canClaim = false;
        if (events != null) foreach (var card in events)
        {
            if (card == null) continue;
            card.Refresh();
            var data = card.definition;
            if (data != null && !MetaProgression.IsEventClaimed(data.saveId)
                && MetaProgression.TotalVictories >= Mathf.Max(1, data.requiredVictories)) canClaim = true;
        }
        if (eventBadge != null) eventBadge.SetActive(canClaim);
        if (soundButtonText != null) soundButtonText.text = PlayerPrefs.GetInt(MutedKey, 0) == 1 ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ";
        if (developerModeButtonText != null) developerModeButtonText.text =
            GameRunMode.DeveloperModeEnabled ? "РЕЖИМ РАЗРАБОТЧИКА: ВКЛ" : "РЕЖИМ РАЗРАБОТЧИКА: ВЫКЛ";
        if (village != null) village.Refresh();
        if (equipment != null) equipment.Refresh();
    }

    public void SelectCampaignLevel(int localLevel)
    {
        int globalLevel = LevelProgression.GlobalLevelFor(SelectedLocationIndex, localLevel);
        if (LevelProgression.SelectMenuLevel(globalLevel)) Refresh();
        else ShowInformation("Уровень закрыт", "Пройди предыдущий уровень, чтобы открыть это направление.");
    }

    public void ShowPreviousLocation() => TryShowLocation(SelectedLocationIndex - 1);
    public void ShowNextLocation() => TryShowLocation(SelectedLocationIndex + 1);

    private void TryShowLocation(int locationIndex)
    {
        if (locationIndex < 1 || locationIndex > LevelProgression.LocationCount) return;
        int firstGlobalLevel = LevelProgression.GlobalLevelFor(locationIndex, 1);
        if (!GameRunMode.DeveloperModeEnabled
            && firstGlobalLevel > LevelProgression.HighestUnlockedLevel)
        {
            ShowInformation("Локация закрыта",
                "Заверши «" + LevelProgression.LocationNameForIndex(locationIndex - 1)
                + "», чтобы открыть «" + LevelProgression.LocationNameForIndex(locationIndex) + "».");
            return;
        }

        SelectedLocationIndex = locationIndex;
        LevelProgression.SelectMenuLevel(firstGlobalLevel);
        Refresh();
    }

    public void PlaySelectedLevel()
    {
        if (!Application.isPlaying || loading || IsDialogOpen) return;
        OpenProjectileLoadout(GameRunType.Campaign);
    }

    public void PlaySurvival()
    {
        if (!Application.isPlaying || loading || IsDialogOpen) return;
        OpenProjectileLoadout(GameRunType.Survival);
    }

    private void OpenProjectileLoadout(GameRunType mode)
    {
        bool survival = mode != GameRunType.Campaign;
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneBuildIndex))
        {
            ShowInformation("Режим недоступен", "Игровая сцена отсутствует в сборке.");
            return;
        }
        int levelNumber = LevelProgression.SelectedMenuLevel;
        int location = survival ? 1 : LevelProgression.LocationIndexForLevel(levelNumber);
        if (projectileLoadoutUI == null
            || !projectileLoadoutUI.Show(loadout => StartSelectedRun(mode, levelNumber, loadout), location))
            ShowInformation("Выбор снарядов недоступен", "Не удалось открыть окно подготовки к бою.");
        ClearSelection();
    }

    private void StartSelectedRun(GameRunType mode, int levelNumber, IReadOnlyList<int> loadout)
    {
        bool survival = mode != GameRunType.Campaign;
        int location = survival ? 1 : LevelProgression.LocationIndexForLevel(levelNumber);
        if (loading || !AnimalProjectileQueue.PrepareRunLoadout(loadout, location)) return;
        loading = true;
        if (survival)
        {
            GameRunMode.PrepareSurvival();
            if (survivalPlayText != null) survivalPlayText.text = "ЗАГРУЗКА…";
        }
        else
        {
            GameRunMode.PrepareCampaign();
            LevelProgression.PrepareLevel(levelNumber);
            if (playText != null) playText.text = "ЗАГРУЗКА…";
        }
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(gameplaySceneBuildIndex);
    }

    public void ShowInformation(string title, string body)
    {
        CloseDialogs();
        if (informationTitle != null) informationTitle.text = title;
        if (informationBody != null) informationBody.text = body;
        if (informationModal != null) informationModal.SetActive(true);
    }

    public void ShowResources() => ShowInformation("Ресурсы лагеря",
        "ЖЁЛУДИ: " + ForestVillageUI.FormatAmount(MetaProgression.AcornsExact)
        + "\nВечное древо: +" + ForestVillageUI.FormatRate(MetaProgression.GreatTreeAcornsPerMinute) + " в минуту."
        + "\n\nЗОЛОТО: " + ForestVillageUI.FormatAmount(MetaProgression.GoldExact)
        + "\nЛесная лавка: +" + ForestVillageUI.FormatRate(MetaProgression.ForestShopGoldPerMinute) + " в минуту."
        + "\n\nСЕМЕНА: " + MetaProgression.Seeds + "\nБРИЛЛИАНТЫ: " + MetaProgression.Diamonds
        + "\n\nПассивный доход копится не более 8 часов. Забрать сбережения можно у Вечного древа.");
    public void ShowSettings() { CloseDialogs(); if (settingsModal != null) settingsModal.SetActive(true); Refresh(); }
    public void ToggleSound()
    {
        if (!Application.isPlaying) return;
        bool muted = PlayerPrefs.GetInt(MutedKey, 0) == 0;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        AudioListener.volume = muted ? 0f : 1f;
        Refresh();
    }
    public void ToggleDeveloperMode()
    {
        if (!Application.isPlaying) return;
        GameRunMode.SetDeveloperMode(!GameRunMode.DeveloperModeEnabled);
        SelectedLocationIndex = LevelProgression.SelectedMenuLocation;
        Refresh();
    }
    public void CloseDialogs()
    {
        if (village != null) village.CloseDialog();
        if (bestiary != null) bestiary.Close();
        if (skinShop != null) skinShop.Close();
        if (equipment != null) equipment.CloseDialog();
        if (informationModal != null) informationModal.SetActive(false);
        if (settingsModal != null) settingsModal.SetActive(false);
        if (projectileLoadoutUI != null) projectileLoadoutUI.Close();
        ClearSelection();
    }
    // Also callable by the editor preview; it never claims income or spends currency.
    public void PrepareVillageAndHero()
    {
        if (village == null) village = ForestVillageUI.Ensure(this);
        if (bestiary == null) bestiary = BestiaryUI.Ensure(this);
        if (skinShop == null) skinShop = ProjectileSkinShopUI.Ensure(this);
        if (equipment == null) equipment = HeroEquipmentUI.Ensure(this);
        Transform oldDialog = transform.Find("Safe Area/Portrait Frame/Dialogs/Upgrade Dialog");
        if (oldDialog != null) oldDialog.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void RebindEquipmentForAuthoring()
    {
        if (Application.isPlaying) return;
        equipment = null;
        PrepareVillageAndHero();
        CloseDialogs();
    }

    public void RebindVillageForAuthoring()
    {
        if (Application.isPlaying) return;
        // A loaded scene can still hold wrappers bound to the removed houses
        // and old entrance buttons. Reconnect without reloading other edits.
        village = null;
        bestiary = null;
        skinShop = null;
        PrepareVillageAndHero();
        CloseDialogs();
    }
#endif

    private static void ClearSelection()
    {
        if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
}

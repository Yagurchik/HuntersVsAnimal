using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static VillageUIStyle;
using Object = UnityEngine.Object;

public static class BestiaryAuthoring
{
    private const string MenuScene = "Assets/Scenes/UIscene.unity";
    private const string GameScene = "Assets/Asset.unity";
    private static readonly string[] ProjectileNames = {
        "Светлячок", "Ёж", "Кролик", "Медоед", "Кабан", "Бобр", "Лягушка", "Белочка"
    };
    private static readonly string[] ProjectileRoles = {
        "СВЕТ И ГОРЕНИЕ", "ВЗРЫВ ПО ОБЛАСТИ", "РАЗМНОЖЕНИЕ", "ЦЕПНАЯ ОХОТА",
        "ТАРАН И ОТСКОКИ", "ПЛОТИНА И УКУСЫ", "ПРЫЖКИ И ХОР", "ЖЁЛУДИ И ЛОЗЫ"
    };
    private static readonly string[] ProjectileDescriptions = {
        "Освещает тёмные клетки и поджигает врагов. Повторные залпы позволяют разнести огонь по комнате.",
        "Взрывается при столкновении и наносит урон врагам вокруг точки удара.",
        "После попаданий размножается. Порог следующего рождения постепенно растёт, а каждый кролик живёт ограниченное число ударов.",
        "Прыгает к наиболее раненому врагу в радиусе атаки. Убийства продолжают цепь и усиливают дальнейшие удары.",
        "Проносится по комнате, ударяя врагов на пути и отскакивая от стен.",
        "Помогает защищать плотину и баррикаду; кусает врагов и усиливает защиту нанесённым уроном.",
        "Прыгает между врагами. Лягушки в одной комнате усиливают общий урон своим хором.",
        "Оставляет жёлуди. Они взрываются при гибели врага и могут опутывать соседей лозами."
    };
    private static readonly string[] ProjectileTalents = {
        "Повторный залп — больше ручных залпов за ход; урон последующих залпов зависит от ранга.\n\nГорение — сильнее урон каждого стака огня за ход.\n\nУльта «Вечный огонь» — стаки больше не исчезают, их количество не ограничено.",
        "Радиус взрыва — расширяет область поражения.\n\nОстрые иголки — каждый взрыв в этом ходу наращивает урон по врагу.\n\nУльта «Двойной залп» — при столкновении следом за первым происходит второй взрыв.",
        "Сила стаи — каждый живой кролик добавляет урон.\n\nБыстрое размножение — снижает максимальный порог размножения с 10 до 9/8/7 ударов.\n\nРанний приплод — сокращает первое размножение с 5 до 4/3/2 ударов.\n\nУльта «Пуховая буря» — каждый десятый общий удар вызывает облако урона от всех кроликов.",
        "Радиус атаки — увеличивает дальность прыжка к цели.\n\nЯростная цепь — больше атак и повторных ударов по одному врагу.\n\nУльта «Абсолютная ярость» — каждый следующий удар получает ещё +10 урона.",
        "Неудержимый разбег — увеличивает число отскоков.\n\nПервый таран — усиливает первый удар по каждому врагу.\n\nУльта «Тройной прорыв» — одновременно летят три кабана с чуть разными траекториями.",
        "Крепкая плотина — большая доля нанесённого урона превращается в здоровье защиты.\n\nКрепкие зубы — больше урона за укус.\n\nУльта «Отскок от тыквы» — один дополнительный отскок от поля тыквы.",
        "Дальний прыжок — увеличивает радиус прыжка.\n\nЛягушачий хор — каждая лягушка усиливает урон в комнате.\n\nУльта «КВА» — удар поражает цель и врагов в радиусе одной клетки.",
        "Ранний взрыв — повышает шанс взрыва жёлудя при повторном попадании.\n\nЛесные путы — взрыв может обездвижить врагов на один ход.\n\nУльта «Цепная реакция» — взрывы подрывают соседние жёлуди."
    };
    private static readonly string[] LocationDescriptions = {
        "Тёмные корни и старые кости. Здесь учатся выбирать точку удара и разрывать плотные группы врагов.",
        "Мёртвый лес прячет призраков и личей. Свет становится важнее: на дальних линиях враги скрываются в темноте.",
        "Влажная топь полна яда и взрывоопасных мертвяков. Болотный демон невосприимчив к яду.",
        "У залива обитают чуждые глубоководные головы. Ракушечники, иглы и соляные лужи изнашивают снаряды.",
        "Снежные враги имеют замёрзшую и оттаявшую формы. Огонь растапливает покров; через два хода без нового огня он возвращается.",
        "Огненные твари сочетают сопротивление огню, клейма и взрывы. Порядок ударов особенно важен."
    };
    private static readonly string[] LocationArt = {
        "Assets/LocationArt/TwilightThicket/TwilightThicketMenu_v2.png",
        "Assets/LocationArt/GloomyHollow/GloomyHollowBattlefield.png",
        "Assets/LocationArt/DrownedBog/DrownedBogBattlefield.png",
        "Assets/LocationArt/ForgottenBay/ForgottenBayBattlefield.png",
        "Assets/LocationArt/SnowyHaze/SnowyHazeBattlefield.png",
        "Assets/LocationArt/FieryGehenna/GehennaBattlefield.png"
    };
    private static readonly Dictionary<string, string> EnemyNotes = new Dictionary<string, string> {
        { "ForestBarkGuard", "Удар по верхней части головы наносит 70% обычного прямого урона. По открытой челюсти проходит полный урон." },
        { "ForestThornling", "Первое касание каждого снаряда отнимает у него 1 HP. Повторное касание тем же снарядом не срабатывает." },
        { "ForestLeafEater", "Пока рядом есть другой обычный враг, получает на 20% меньше прямого урона. Огонь и яд действуют полностью." },
        { "ShadowWeaver", "Создаёт видимую тень в радиусе одной клетки. Свет Светлячка разгоняет тень; силуэты врагов остаются видимыми." },
        { "BogLeech", "После первого пережитого прямого удара за залп отнимает 1 HP у ударившего снаряда и лечится на 8% своего максимального HP." },
        { "BogMossHealer", "Каждые два хода лечит самого раненого обычного союзника в радиусе одной клетки на 12% его максимального HP." },
        { "BogTinnik", "Соседние обычные враги получают только половину урона от яда. После гибели тинника защита исчезает." },
        { "BayShellback", "Первое прямое попадание каждого залпа наносит ему только половину урона. Дальнейшие попадания проходят полностью." },
        { "BayTideHerald", "Каждый третий ход даёт соседнему обычному врагу дополнительный шаг, если путь свободен." },
        { "BayPuffer", "Первый прямой контакт с каждым снарядом отнимает у него 2 HP. Повторное касание безопасно." },
        { "BaySaltbearer", "После гибели оставляет соляную лужу до конца текущего залпа. Прошедший через неё снаряд теряет 1 HP один раз." },
        { "SnowSkull", "Огонь снимает снег. Замёрзшая форма возвращается после двух ходов без нового огненного урона." },
        { "SnowIceface", "В ледяной форме получает на 15% меньше прямого урона и на 50% больше огненного. Огонь растапливает лёд." },
        { "SnowWisp", "В замёрзшей форме имеет 25% шанс пропустить снаряд. Свет Светлячка лишает его этой защиты." },
        { "SnowRunesmith", "В ледяной форме каждые два хода даёт соседнему обычному врагу щит на 20% его максимального HP." },
        { "SnowDoppel", "В ледяной форме один раз переживает смертельный прямой удар с 1 HP. Яд и огонь добивают его обычно." },
        { "FireSlag", "Получает 50% огненного урона и 115% прямого. Против него выгоднее обычные снаряды." },
        { "FireEmber", "После гибели взрывается в радиусе одной клетки: отнимает 2 HP у снарядов и наносит соседям огненный урон на 25% своего максимального HP." },
        { "FireAshCollector", "После гибели обычного соседа в радиусе одной клетки лечится на 20% своего максимального HP. Не чаще раза за залп." },
        { "FireBrander", "Каждые два хода метит соседнего врага. Первый снаряд, ударивший помеченного, теряет 2 HP." }
    };

    [MenuItem("Forest Animal War/UI/Bake editable bestiary into UIscene")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before editing UIscene.");
        GameplaySources sources = ReadGameplaySources();
        Scene scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        MenuHubController hub = Object.FindObjectOfType<MenuHubController>(true);
        if (hub == null || hub.tabs == null || hub.tabs.Length < 3 || hub.tabs[2].page == null)
            throw new InvalidOperationException("The editable village page is missing from UIscene.");
        VillageEntityWindowAuthoring.SynchronizeSceneWindows(hub);
        Transform page = hub.tabs[2].page.transform;
        Transform field = page.Find("Forest Village Runtime/Viewport/Content/Village Field");
        if (field == null) throw new InvalidOperationException("Bake the editable village first.");
        Remove(field.Find("Book Hint"));
        Remove(page.Find("Bestiary Menu"));

        VillageEntityWindowAuthoring.EnsureBestiaryWindow(field);

        RectTransform panel = Modal(page, "Bestiary Menu", 1430, null, out GameObject overlay);
        overlay.AddComponent<BestiaryUI>();
        panel.GetComponent<Image>().color = Background;
        Label("Title", panel, "БЕСТИАРИЙ", 43, Gold, 32, 24, 720, 66, true);
        Label("Intro", panel, "Летопись леса и его защитников", 20, Muted, 34, 84, 800, 32);
        RectTransform tabs = Rect("Category Tabs", panel);
        Top(tabs, 32, 125, 916, 65);
        Button("Locations", tabs, "ЛОКАЦИИ", 0, 0, 290, 60, null, 23);
        Button("Enemies", tabs, "ВРАГИ", 313, 0, 290, 60, null, 23);
        Button("Projectiles", tabs, "СНАРЯДЫ", 626, 0, 290, 60, null, 23);
        RectTransform lists = Rect("Entry Lists", panel);
        Top(lists, 32, 210, 372, 1180);
        RectTransform locationList = CreateList(lists, "Locations", 6);
        RectTransform enemyList = CreateList(lists, "Enemies", 64);
        RectTransform projectileList = CreateList(lists, "Projectiles", ProjectileNames.Length);
        int enemyCount = BuildEntries(locationList, enemyList, projectileList, sources);
        BuildDetail(panel);
        overlay.SetActive(false);
        hub.RebindVillageForAuthoring();
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Editable bestiary baked into UIscene: 6 locations, "
            + enemyCount + " enemy cards and 8 projectiles.");
    }

    public static void RunBatch()
    {
        try { Bake(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private sealed class GameplaySources
    {
        public Sprite[] ProjectileIcons;
        public List<string>[] BossesByLocation;
    }

    private static GameplaySources ReadGameplaySources()
    {
        Scene gameplay = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        AnimalProjectileQueue queue = Object.FindObjectsOfType<AnimalProjectileQueue>(true).FirstOrDefault();
        LevelProgression progression = Object.FindObjectsOfType<LevelProgression>(true).FirstOrDefault();
        if (queue == null || queue.Animals.Count < ProjectileNames.Length)
            throw new InvalidOperationException("The gameplay projectile roster is incomplete.");
        if (progression == null) throw new InvalidOperationException("Campaign progression is missing.");
        var result = new GameplaySources {
            ProjectileIcons = new Sprite[ProjectileNames.Length],
            BossesByLocation = new List<string>[LevelProgression.LocationCount]
        };
        for (int i = 0; i < result.ProjectileIcons.Length; i++)
            result.ProjectileIcons[i] = queue.Animals[i].GetIcon();
        for (int location = 1; location <= LevelProgression.LocationCount; location++)
        {
            List<string> bosses = new List<string>();
            for (int localLevel = 1; localLevel <= LevelProgression.LevelsPerLocation; localLevel++)
            for (int line = 2; line <= 4; line++)
            {
                int level = LevelProgression.GlobalLevelFor(location, localLevel);
                string resource = BossResource(progression.GetBossKind(level, line, line));
                if (resource != null && !bosses.Contains(resource)) bosses.Add(resource);
            }
            result.BossesByLocation[location - 1] = bosses;
        }
        EditorSceneManager.CloseScene(gameplay, true);
        return result;
    }

    private static string BossResource(LevelProgression.BossKind kind)
    {
        switch (kind)
        {
            case LevelProgression.BossKind.Lich: return "LichBoss";
            case LevelProgression.BossKind.BoneDragonMini: return "BoneDragonMiniBoss";
            case LevelProgression.BossKind.DrownedMini: return "DrownedMiniBoss";
            case LevelProgression.BossKind.Mirage: return "MirageBoss";
            case LevelProgression.BossKind.BoneDragon: return "BoneDragonBoss";
            case LevelProgression.BossKind.GhostlySorrow: return "GhostlySorrowBoss";
            case LevelProgression.BossKind.PoisonousGloom: return "PoisonousGloomBoss";
            case LevelProgression.BossKind.HiveHollow: return "HiveHollowMiniBoss";
            case LevelProgression.BossKind.GreenJudge: return "GreenJudgeMiniBoss";
            case LevelProgression.BossKind.BellRinger: return "BellRingerBoss";
            case LevelProgression.BossKind.KnotBinder: return "KnotBinderBoss";
            case LevelProgression.BossKind.VolleyEater: return "VolleyEaterBoss";
            case LevelProgression.BossKind.ThreeSealIdol: return "ThreeSealIdolBoss";
            case LevelProgression.BossKind.Impostor: return "ImpostorBoss";
            default: return null;
        }
    }

    private static int BuildEntries(RectTransform locationList, RectTransform enemyList,
        RectTransform projectileList, GameplaySources sources)
    {
        int enemyIndex = 0;
        for (int location = 1; location <= LevelProgression.LocationCount; location++)
        {
            var names = new List<string>(5);
            Sprite ordinaryPortrait = null;
            for (int slot = 0; slot < 5; slot++)
            {
                GameObject prefab = LoadOrdinary(location, slot);
                if (prefab == null) throw new InvalidOperationException("Missing enemy for " + location + ":" + slot);
                EnemyFirstEncounterProfile profile = prefab.GetComponent<EnemyFirstEncounterProfile>();
                if (profile == null) throw new InvalidOperationException("Missing enemy information: " + prefab.name);
                Sprite portrait = Portrait(prefab);
                if (slot == 0) ordinaryPortrait = portrait;
                names.Add(profile.DisplayName);
                AddEntry(enemyList, enemyIndex++, BestiaryEntryView.Section.Enemies,
                    profile.DisplayName, LevelProgression.LocationNameForIndex(location)
                        + (slot == 0 ? "  ·  РЯДОВОЙ" : "  ·  СПЕЦИАЛИСТ"),
                    EnemyDescription(prefab, profile), portrait);
            }
            var bossNames = new List<string>();
            foreach (string resource in sources.BossesByLocation[location - 1])
            {
                GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Enemies/" + resource + ".prefab");
                if (boss == null) throw new InvalidOperationException("Missing boss: " + resource);
                EnemyFirstEncounterProfile profile = boss.GetComponent<EnemyFirstEncounterProfile>();
                if (profile == null) throw new InvalidOperationException("Missing boss information: " + resource);
                bossNames.Add(profile.DisplayName);
                AddEntry(enemyList, enemyIndex++, BestiaryEntryView.Section.Enemies,
                    profile.DisplayName, LevelProgression.LocationNameForIndex(location) + "  ·  БОСС",
                    EnemyDescription(boss, profile), Portrait(boss));
            }
            Sprite locationPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(LocationArt[location - 1]);
            AddEntry(locationList, location - 1, BestiaryEntryView.Section.Locations,
                LevelProgression.LocationNameForIndex(location), "ГЛАВА " + location + "  ·  5 УРОВНЕЙ",
                LocationDescriptions[location - 1] + "\n\nОБЫЧНЫЕ ВРАГИ\n"
                + string.Join("  ·  ", names) + "\n\nБОССЫ И МИНИ-БОССЫ\n"
                + string.Join("  ·  ", bossNames)
                + "\n\nКаждый уровень содержит четыре линии обороны. На линиях 2–4 освещение слабее; Светлячок раскрывает тёмные клетки.",
                locationPortrait != null ? locationPortrait : ordinaryPortrait);
        }
        ResizeList(enemyList, enemyIndex);
        for (int i = 0; i < ProjectileNames.Length; i++)
        {
            string body = ProjectileDescriptions[i] + "\n\nТАЛАНТЫ\n"
                + (i == AnimalProjectileQueue.FireflyIndex
                    ? "Светлячок всегда выпускает 5 снарядов за залп.\n\n"
                    : i == AnimalProjectileQueue.RabbitIndex
                        ? "Кролик начинает с двух снарядов за залп.\n\n"
                        : "Количество — открывает снаряд и увеличивает его запас за залп.\n\n")
                + ProjectileTalents[i]
                + (i == AnimalProjectileQueue.FireflyIndex
                    ? "\n\nУльта появляется после прокачки двух основных веток до III ранга."
                    : "\n\nУльта появляется после прокачки всех трёх основных веток до III ранга.");
            AddEntry(projectileList, i, BestiaryEntryView.Section.Projectiles,
                ProjectileNames[i], ProjectileRoles[i], body, sources.ProjectileIcons[i]);
        }
        return enemyIndex;
    }

    private static GameObject LoadOrdinary(int location, int slot)
    {
        string resource = LevelGenerator.GetLocationEnemyResourceName(location, slot);
        return location == 1 && slot == 0
            ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Brick.prefab")
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies/" + resource + ".prefab");
    }

    private static Sprite Portrait(GameObject prefab)
    {
        EnemyFirstEncounterProfile profile = prefab.GetComponent<EnemyFirstEncounterProfile>();
        if (profile != null && profile.Portrait != null) return profile.Portrait;
        Transform visual = prefab.transform.Find("Visual");
        SpriteRenderer renderer = visual != null ? visual.GetComponentInChildren<SpriteRenderer>(true) : null;
        return renderer != null ? renderer.sprite : null;
    }

    private static string EnemyDescription(GameObject prefab, EnemyFirstEncounterProfile profile)
    {
        Brick enemy = prefab.GetComponent<Brick>();
        string size = enemy.Footprint.x + "×" + enemy.Footprint.y;
        string role = enemy.UsesBossHealth ? "БОСС / МИНИ-БОСС" : "ОБЫЧНЫЙ ВРАГ";
        string details = EnemyNotes.TryGetValue(prefab.name, out string note)
            ? "\n\nОСОБЕННОСТЬ\n" + note : string.Empty;
        return profile.Summary + details + "\n\n" + role + "\nРазмер на поле: " + size
            + (enemy.UsesBossHealth
                ? "\nХП от базового врага того же уровня:\n2-я линия: ×20\n3-я линия: ×30\n4-я линия: ×40"
                : "\nБазовое здоровье: " + enemy.hp.ToString("0.#"))
            + (prefab.name == "DrownedHeir" ? "\nОсколок получает четверть указанных ХП своего родителя." : "")
            + (enemy.ImmuneToPoison ? "\nИммунитет к яду" : "")
            + (enemy.UsesBossHealth
                ? "\n\nМножитель линии применяется при появлении босса. При переходе между линиями ХП сохраняются."
                : "\n\nЗдоровье растёт с уровнем и волной.");
    }

    private static RectTransform CreateList(Transform parent, string name, int count)
    {
        RectTransform root = PanelRect(name, parent, Panel, 0, 0, 372, 1180);
        root.GetComponent<Image>().raycastTarget = true;
        RectTransform viewport = Rect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, count * 116f + 16f);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        return content;
    }

    private static void ResizeList(RectTransform content, int count)
        => content.sizeDelta = new Vector2(0f, count * 116f + 16f);

    private static void AddEntry(Transform parent, int index, BestiaryEntryView.Section category,
        string title, string subtitle, string body, Sprite portrait)
    {
        RectTransform card = PanelRect("Entry " + index, parent, Panel,
            6, 8 + index * 116, 352, 106);
        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;
        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        RectTransform icon = Rect("Portrait", card);
        Top(icon, 9, 13, 80, 80);
        Image image = icon.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        Label("Name", card, title, 23, Cream, 100, 13, 240, 43, true);
        Label("Subtitle", card, subtitle, 17, Muted, 100, 59, 240, 34);
        card.gameObject.AddComponent<BestiaryEntryView>()
            .Configure(category, title, subtitle, body, portrait);
    }

    private static void BuildDetail(Transform panel)
    {
        RectTransform detail = PanelRect("Detail", panel, Panel, 425, 210, 530, 1180);
        RectTransform icon = Rect("Portrait", detail);
        Top(icon, 130, 25, 270, 205);
        Image portrait = icon.gameObject.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Label("Title", detail, "", 35, Cream, 18, 245, 494, 76, true);
        Label("Subtitle", detail, "", 19, Gold, 18, 326, 494, 50, true);
        RectTransform scrollRoot = PanelRect("Body Scroll", detail, Background, 18, 392, 494, 760);
        scrollRoot.GetComponent<Image>().raycastTarget = true;
        RectTransform viewport = Rect("Viewport", scrollRoot);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform textRect = Rect("Body", viewport);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, 760f);
        Text text = textRect.gameObject.AddComponent<Text>();
        text.font = GameTypography.Font;
        text.fontSize = 22;
        text.color = Cream;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = textRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
    }

    private static void Remove(Transform target)
    {
        if (target != null) Object.DestroyImmediate(target.gameObject);
    }
}

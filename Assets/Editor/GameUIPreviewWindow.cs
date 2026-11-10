using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Edits only presentation objects. Never starts a run, unlocks animals or changes PlayerPrefs.
public sealed class GameUIPreviewWindow : EditorWindow
{
    public const string GameplayPath = "Assets/Asset.unity";
    public const string MenuPath = "Assets/Scenes/UIscene.unity";
    private static readonly string[] WheelIconPaths = {
        "Assets/MonsterSprite/ProjectileArt/firefly-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/hedgehog-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/rabbit-v2-simple.png",
        "Assets/MonsterSprite/ProjectileArt/HoneyBadger/honey-badger-v2-simple.png"
    };
    private int previewLevel = 1;
    private int previewLine = 1;
    private int previewRoom = 1;
    private bool allAnimals = true;
    private Vector2 scroll;

    [MenuItem("Forest Animal War/UI/Обзор всего UI", false, 0)]
    public static void Open()
    {
        GetWindow<GameUIPreviewWindow>("Обзор UI").minSize = new Vector2(370f, 510f);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Предпросмотр в Scene и Game без Play. Выбери экран, затем редактируй его объекты в Hierarchy. Данные прогресса и покупки не меняются.", MessageType.Info);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Останови Play для редактирования и предпросмотра. В игре используются сохранённые элементы этих же сцен.", MessageType.Info);
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Открыть главное меню")) OpenScene(MenuPath);
        if (GUILayout.Button("Открыть игровую сцену")) OpenScene(GameplayPath);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Показать окно Game")) EditorApplication.ExecuteMenuItem("Window/General/Game");
        string scene = SceneManager.GetActiveScene().path;
        if (scene == MenuPath) DrawMenu();
        else if (scene == GameplayPath) DrawGameplay();
        else EditorGUILayout.HelpBox("Открой одну из игровых сцен кнопками выше.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawMenu()
    {
        MenuHubController hub = Find<MenuHubController>();
        if (hub == null)
        {
            if (GUILayout.Button("Создать редактируемое меню")) MenuHubBuilder.Install();
            return;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Главное меню · Сумеречная чаща · 5 уровней", EditorStyles.boldLabel);
        for (int i = 0; i < hub.tabs.Length; i++)
        {
            int tab = i;
            if (GUILayout.Button(hub.tabs[i].title)) MenuAction(hub, () => { hub.PrepareVillageAndHero(); hub.SelectTab(tab); hub.Refresh(); });
        }
        if (GUILayout.Button("Ресурсы и доход комнат")) MenuAction(hub, hub.ShowResources);
        if (GUILayout.Button("Окно награды")) MenuAction(hub, () => hub.ShowInformation("Награда получена", "+15 желудей\n\nВерный защитник"));
        if (GUILayout.Button("Настройки")) MenuAction(hub, hub.ShowSettings);
        if (GUILayout.Button("Закрыть окна")) MenuAction(hub, hub.CloseDialogs);
        if (GUILayout.Button("Выделить Forest Menu")) Selection.activeGameObject = hub.gameObject;
    }

    private void DrawGameplay()
    {
        EditorGUILayout.Space();
        if (GUILayout.Button("Добавить недостающие элементы UI в сцену"))
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install editable gameplay UI");
            var existing = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            InstallGameplay();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!existing.Contains(root)) Undo.RegisterCreatedObjectUndo(root, "Install gameplay UI");
            Undo.CollapseUndoOperations(group);
            Changed();
        }
        if (Find<ExperienceAndUpgradeUI>() == null || Find<AnimalSelectionWheel>() == null || Find<GameAnnouncementUI>() == null)
        {
            EditorGUILayout.HelpBox("Сначала добавь недостающие элементы кнопкой выше.", MessageType.Info);
            return;
        }
        previewLevel = EditorGUILayout.IntSlider("Уровень локации", previewLevel, 1, LevelProgression.MaxLevel);
        previewLine = EditorGUILayout.IntSlider("Линия обороны", previewLine, 1, 4);
        previewRoom = Mathf.Clamp(previewRoom, 1, previewLine * 2 - 1);
        previewRoom = EditorGUILayout.IntSlider("Комната в линии", previewRoom, 1, previewLine * 2 - 1);
        allAnimals = EditorGUILayout.Toggle("Показать всех зверей", allAnimals);
        EditorGUILayout.HelpBox("Все звери — только образец колеса. В новом забеге по-прежнему доступны 3 светлячка. Неактивные комнаты выбранного уровня остаются недоступными на карте.", MessageType.None);
        if (GUILayout.Button("Поле, темнота, колесо и опыт")) PreviewGame("hud");
        if (GUILayout.Button("Карта связей и доступные комнаты")) PreviewGame("map");
        if (GUILayout.Button("Выбор боевого улучшения")) PreviewGame("upgrade");
        if (GUILayout.Button("Линия обороны пройдена")) PreviewGame("announcement");
        if (GUILayout.Button("HP и цифры урона")) PreviewGame("health");
        if (GUILayout.Button("Счётчик комбо")) PreviewGame("combo");
        if (GUILayout.Button("Пауза")) PreviewGame("pause");
        if (GUILayout.Button("Победа")) PreviewGame("win");
        if (GUILayout.Button("Поражение")) PreviewGame("lose");
        if (GUILayout.Button("Закрыть окна / обычное начало"))
        {
            previewLine = previewRoom = 1;
            allAnimals = false;
            PreviewGame("hud");
        }
    }

    private void PreviewGame(string mode)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            Undo.RegisterFullObjectHierarchyUndo(root, "Preview gameplay UI");
        ShowGameplay(mode, previewLevel, previewLine, previewRoom, allAnimals);
        Changed();
    }

    public static void InstallGameplay()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != GameplayPath)
            throw new InvalidOperationException("Open Assets/Asset.unity outside Play mode.");
        AnimalProjectileQueue queue = Find<AnimalProjectileQueue>();
        if (queue == null) throw new InvalidOperationException("The gameplay scene needs AnimalProjectileQueue.");
        GameplayBackdropUI.Ensure(Camera.main);
        EnsureWheelIcons(queue);
        AnimalSelectionWheel.Create(queue).Preview(queue, false);
        ExperienceAndUpgradeUI.Ensure(null).Preview(queue, false);
        GameAnnouncementUI.GetOrCreate().Preview("");
        EnemyCombatTextUI.Ensure();
        ProjectileComboUI.Ensure().Preview(0, false);
        DefenseFrontUI map = Find<DefenseFrontUI>();
        if (map == null)
        {
            var root = new GameObject("Defense Front HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DefenseFrontUI));
            map = root.GetComponent<DefenseFrontUI>();
        }
        map.BuildInterface();
        Find<LevelTransition>()?.ConfigurePanelLayers();
        PauseDamageAuthoring.Install();
        ShowGameplay("hud", 1, 1, 1, false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void EnsureWheelIcons(AnimalProjectileQueue queue)
    {
        int count = Mathf.Min(queue.Animals.Count, WheelIconPaths.Length);
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            // Only seed empty slots. The references remain serialized on the queue,
            // so artists can replace any icon directly in the scene later.
            if (queue.Animals[i].HasCustomIcon) continue;
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(WheelIconPaths[i]);
            if (icon == null) throw new InvalidOperationException("Missing wheel icon: " + WheelIconPaths[i]);
            queue.Animals[i].SetIcon(icon);
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(queue);
    }

    public static void ShowGameplay(string mode, int level, int line, int index, bool allAnimals)
    {
        if (Application.isPlaying) return;
        LevelProgression progression = Find<LevelProgression>();
        var model = new DefenseFront();
        var active = new List<int>();
        foreach (DefenseFront.Room r in model.Rooms)
            if (progression == null || progression.IsRoomActive(level, r.Line, r.Index)) active.Add(r.Id);
        model.ConfigureActiveRooms(active);
        for (int opened = 1; opened <= line; opened++) model.OpenLine(opened);
        foreach (DefenseFront.Room r in model.Rooms)
            if (r.IsActive && r.Line <= line)
            {
                var balance = progression != null ? progression.GetBalance(level, r.Line) : new LevelProgression.DefenseLineBalance(5, 1);
                model.Observe(r.Id, r.Line < line ? 0 : balance.enemiesPerWave,
                    r.Line < line ? 0 : balance.additionalWaves);
            }
        int selected = DefenseFront.IdFor(line, index);
        // Absent rooms can be inspected geometrically, but selection on the map stays valid.
        int mapSelection = model.GetRoom(selected).IsActive ? selected : model.Rooms.First(r => r.Line == line && r.IsActive).Id;
        Stage chosen = null;
        foreach (Stage room in Object.FindObjectsOfType<Stage>(true))
        {
            if (room.gameObject.scene != SceneManager.GetActiveScene()) continue;
            LevelGenerator generator = room.GetComponentInChildren<LevelGenerator>(true);
            if (generator == null) continue;
            bool isChosen = room.DefenseLine == line && (room.name == "Stage " + line + "_" + index || line == 1);
            if (isChosen && room.transform.parent != null) room.transform.parent.gameObject.SetActive(true);
            room.gameObject.SetActive(isChosen);
            room.SetPlayerActive(isChosen);
            if (isChosen) chosen = room;
        }
        if (chosen != null)
        {
            chosen.GetComponent<RoomDarkness>()?.Refresh();
            Find<CameraRenderer>()?.FrameRoom(chosen);
        }
        Find<AnimalSelectionWheel>()?.Preview(Find<AnimalProjectileQueue>(), allAnimals);
        Find<ExperienceAndUpgradeUI>()?.Preview(Find<AnimalProjectileQueue>(), mode == "upgrade", level);
        Find<DefenseFrontUI>()?.Preview(model, mapSelection, level, mode == "map");
        ProjectileComboUI.Ensure().Preview(24, mode == "combo");
        string[] lineNames = { "Первая", "Вторая", "Третья", "Четвёртая" };
        Find<GameAnnouncementUI>()?.Preview(mode == "announcement" ? lineNames[line - 1] + " линия обороны пройдена" : "");
        LevelTransition transition = Find<LevelTransition>();
        if (transition != null)
        {
            if (transition.PausePanel != null) transition.PausePanel.SetActive(mode == "pause");
            if (transition.WinPanel != null) transition.WinPanel.SetActive(mode == "win");
            if (transition.LosePanel != null) transition.LosePanel.SetActive(mode == "lose");
            transition.ConfigurePanelLayers();
        }
        if (chosen != null)
        {
            Vector3 position = chosen.PlayerObject.transform.position + Vector3.up;
            Find<EnemyCombatTextUI>()?.PreviewAt(position, mode == "health");
        }
        Canvas.ForceUpdateCanvases();
        if (mode == "pause") transition?.PausePanel?.GetComponent<PauseDamageUI>()?.Preview(Find<AnimalProjectileQueue>());
        GameObject target = mode == "upgrade" ? Find<ExperienceAndUpgradeUI>()?.UpgradeModal
            : mode == "map" ? Find<DefenseFrontUI>()?.gameObject
            : mode == "announcement" ? Find<GameAnnouncementUI>()?.gameObject
            : mode == "health" ? Find<EnemyCombatTextUI>()?.gameObject
            : mode == "combo" ? Find<ProjectileComboUI>()?.gameObject
            : mode == "pause" ? transition?.PausePanel : mode == "win" ? transition?.WinPanel
            : mode == "lose" ? transition?.LosePanel : chosen?.gameObject;
        if (target != null && !Application.isBatchMode) Selection.activeGameObject = target;
    }

    public static T Find<T>() where T : Component
        => Object.FindObjectsOfType<T>(true).FirstOrDefault(c => c.gameObject.scene == SceneManager.GetActiveScene());

    private static void MenuAction(MenuHubController hub, Action action)
    {
        Undo.RegisterFullObjectHierarchyUndo(hub.gameObject, "Preview menu UI");
        action();
        Selection.activeGameObject = hub.gameObject;
        Changed();
    }

    private static void OpenScene(string path)
    {
        if (SceneManager.GetActiveScene().path != path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
        }
        if (path == MenuPath)
        {
            MenuHubController hub = Find<MenuHubController>();
            if (hub != null) { hub.Refresh(); hub.SelectTab(0); Selection.activeGameObject = hub.gameObject; }
        }
        SceneView.RepaintAll();
    }

    private static void Changed()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    // Used only against the isolated validation copy, then its new UI objects are brought back.
    public static void BakeBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
        EditorSceneManager.OpenScene(GameplayPath);
        InstallGameplay();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Editable gameplay UI saved: wheel, experience, upgrade choices, announcements and map.");
    }
}

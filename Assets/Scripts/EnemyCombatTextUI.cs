using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IDefenseHealthTarget
{
    float DefenseHealth { get; }
    float DefenseMaxHealth { get; }
    Vector3 DefenseHealthAnchor { get; }
}

public enum CombatDamageSource
{
    Other,
    Firefly,
    Rabbit,
    Hedgehog,
    HoneyBadger,
    Boar,
    Fire,
    Poison,
    Beaver,
    Frog,
    Squirrel,
    AcornExplosion
}

public static class CombatDamagePalette
{
    public static CombatDamageSource FromAnimalIndex(int index)
    {
        switch (index)
        {
            case AnimalProjectileQueue.FireflyIndex: return CombatDamageSource.Firefly;
            case AnimalProjectileQueue.RabbitIndex: return CombatDamageSource.Rabbit;
            case AnimalProjectileQueue.HedgehogIndex: return CombatDamageSource.Hedgehog;
            case AnimalProjectileQueue.HoneyBadgerIndex: return CombatDamageSource.HoneyBadger;
            case AnimalProjectileQueue.BoarIndex: return CombatDamageSource.Boar;
            case AnimalProjectileQueue.BeaverIndex: return CombatDamageSource.Beaver;
            case AnimalProjectileQueue.FrogIndex: return CombatDamageSource.Frog;
            case AnimalProjectileQueue.SquirrelIndex: return CombatDamageSource.Squirrel;
            default: return CombatDamageSource.Other;
        }
    }

    public static CombatDamageSource FromProjectile(Animal projectile)
    {
        if (projectile == null) return CombatDamageSource.Other;
        if (projectile.CombatSource != CombatDamageSource.Other) return projectile.CombatSource;
        if (projectile.GetComponent<FireflyImpactEffect>() != null) return CombatDamageSource.Firefly;
        if (projectile.GetComponent<RabbitReplication>() != null) return CombatDamageSource.Rabbit;
        if (projectile.GetComponent<HedgehogExplosion>() != null) return CombatDamageSource.Hedgehog;
        if (projectile.GetComponent<HoneyBadgerProjectile>() != null) return CombatDamageSource.HoneyBadger;
        if (projectile.GetComponent<BoarProjectile>() != null) return CombatDamageSource.Boar;
        if (projectile.GetComponent<BeaverProjectile>() != null) return CombatDamageSource.Beaver;
        if (projectile.GetComponent<FrogProjectile>() != null) return CombatDamageSource.Frog;
        if (projectile.GetComponent<SquirrelProjectile>() != null) return CombatDamageSource.Squirrel;
        return CombatDamageSource.Other;
    }

    public static Color ColorFor(CombatDamageSource source)
    {
        switch (source)
        {
            case CombatDamageSource.Firefly:
            case CombatDamageSource.Fire: return new Color32(255, 135, 42, 255);
            case CombatDamageSource.Hedgehog: return new Color32(166, 104, 53, 255);
            case CombatDamageSource.HoneyBadger:
            case CombatDamageSource.Poison: return new Color32(112, 205, 75, 255);
            case CombatDamageSource.Boar: return new Color32(255, 222, 67, 255);
            case CombatDamageSource.Beaver: return new Color32(216, 154, 91, 255);
            case CombatDamageSource.Frog: return new Color32(155, 231, 94, 255);
            case CombatDamageSource.Squirrel:
            case CombatDamageSource.AcornExplosion: return new Color32(242, 173, 69, 255);
            default: return Color.white;
        }
    }

    public static string PrefixFor(CombatDamageSource source)
    {
        switch (source)
        {
            case CombatDamageSource.Fire: return "ОГОНЬ ";
            case CombatDamageSource.Poison: return "ЯД ";
            default: return null;
        }
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class EnemyCombatTextUI : MonoBehaviour
{
    private sealed class HealthState
    {
        public float Health;
        public string Text;
    }

    private sealed class DamageEntry
    {
        public TextMeshProUGUI Label;
        public Vector2 Start;
        public float Elapsed;
        public long GroupKey;
        public float Total;
        public RoomDarkness RoomDarkness;
        public Vector3 WorldPosition;
    }

    private sealed class PendingDamage
    {
        public float Total;
        public Vector2 Start;
        public Vector3 WorldPosition;
        public RoomDarkness RoomDarkness;
        public CombatDamageSource Source;
    }

    [Header("Enemy health")]
    [SerializeField, Min(0f)] private float healthOffset = 43f;
    [SerializeField, Range(0, 48)] private int prewarmHealthLabels = 18;

    [Header("Floating damage")]
    [SerializeField, Min(10f)] private float damageFontSize = 22f;
    [SerializeField, Min(0.1f)] private float damageDuration = 0.72f;
    [SerializeField, Min(1f)] private float damageTravel = 66f;
    [SerializeField] private Color damageColor = new Color(1f, 0.45f, 0.16f, 1f);
    [SerializeField, Range(0, 64)] private int prewarmDamageLabels = 24;
    [SerializeField, Range(8, 96)] private int maximumDamageLabels = 48;

    private readonly Dictionary<Brick, HealthState> healthValues = new Dictionary<Brick, HealthState>();
    private readonly Dictionary<Brick, EnemyHealthBarView> visibleHealth = new Dictionary<Brick, EnemyHealthBarView>();
    private readonly Queue<EnemyHealthBarView> regularHealthPool = new Queue<EnemyHealthBarView>();
    private readonly Queue<EnemyHealthBarView> bossHealthPool = new Queue<EnemyHealthBarView>();
    private readonly List<MonoBehaviour> defenseTargets = new List<MonoBehaviour>();
    private readonly Dictionary<MonoBehaviour, EnemyHealthBarView> visibleDefense =
        new Dictionary<MonoBehaviour, EnemyHealthBarView>();
    private readonly Queue<EnemyHealthBarView> defenseHealthPool = new Queue<EnemyHealthBarView>();
    private readonly Queue<TextMeshProUGUI> damagePool = new Queue<TextMeshProUGUI>();
    private readonly List<DamageEntry> damageEntries = new List<DamageEntry>();
    private readonly Dictionary<long, DamageEntry> recentDamage = new Dictionary<long, DamageEntry>();
    private readonly Stack<DamageEntry> entryPool = new Stack<DamageEntry>(48);
    private readonly Dictionary<long, PendingDamage> pendingAfterTurn =
        new Dictionary<long, PendingDamage>();
    private readonly List<long> pendingOrder = new List<long>();
    private readonly List<Brick> cleanup = new List<Brick>();

    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera gameplayCamera;
    private EnemyHealthBarView regularHealthPrefab;
    private EnemyHealthBarView bossHealthPrefab;
    private EnemyHealthBarView defenseHealthPrefab;
    private int damageSequence;
    private bool poolsPrepared;
    private bool afterTurnBatchActive;

    public static EnemyCombatTextUI Instance { get; private set; }
    public int RegisteredEnemyCount => healthValues.Count;
    public int VisibleHealthLabelCount => visibleHealth.Count;
    public int ActiveDamageNumberCount => damageEntries.Count;

    public void PreviewAt(Vector3 worldPosition, bool visible)
    {
        if (Application.isPlaying) return;
        ConfigureCanvas();
        Canvas.ForceUpdateCanvases();
        Camera camera = ResolveCamera();
        if (camera == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            camera.WorldToScreenPoint(worldPosition), null, out Vector2 position);
        PreviewHealthBar(position + Vector2.up * healthOffset, visible);
        PreviewLabel("Damage Preview (editor)", "-125", position + Vector2.up * (healthOffset + 55f),
            damageFontSize, damageColor, visible);
    }

    private void PreviewHealthBar(Vector2 position, bool visible)
    {
        const string name = "HP Preview (editor)";
        Transform existing = transform.Find(name);
        if (existing == null && !visible) return;
        EnemyHealthBarView view = existing != null ? existing.GetComponent<EnemyHealthBarView>() : null;
        if (view == null)
        {
            if (existing != null) DestroyImmediate(existing.gameObject);
            EnemyHealthBarView prefab = Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/RegularEnemyHealthBar");
            if (prefab == null) return;
            view = Instantiate(prefab, transform, false);
            view.name = name;
            view.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }
        view.SetValue("1.2k", 0.68f);
        view.RectTransform.anchoredPosition = position;
        view.gameObject.SetActive(visible);
    }

    private void PreviewLabel(string name, string value, Vector2 position, float size, Color color, bool visible)
    {
        Transform existing = transform.Find(name);
        if (existing == null && !visible) return;
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : CreateLabel(name, size, color);
        GameTypography.Apply(label);
        label.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.rectTransform.anchoredPosition = position;
        label.gameObject.SetActive(visible);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    public static EnemyCombatTextUI Ensure()
    {
        if (Instance != null) return Instance;
        EnemyCombatTextUI ui = FindObjectOfType<EnemyCombatTextUI>(true);
        if (ui == null)
        {
            GameObject root = new GameObject(
                "Enemy Combat Text UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(EnemyCombatTextUI));
            ui = root.GetComponent<EnemyCombatTextUI>();
        }
        ui.ConfigureCanvas();
        return ui;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        foreach (string previewName in new[] { "HP Preview (editor)", "Damage Preview (editor)" })
        {
            Transform preview = transform.Find(previewName);
            if (preview != null) preview.gameObject.SetActive(false);
        }
        ConfigureCanvas();
        PreparePools();
    }

    private void ConfigureCanvas()
    {
        canvas = GetComponent<Canvas>();
        canvasRect = transform as RectTransform;
        if (canvas == null || canvasRect == null) return;
        // A Canvas created and saved before its first layout pass can retain zero scale.
        // Normalize it here so active TMP labels are actually rendered.
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void PreparePools()
    {
        if (poolsPrepared || !Application.isPlaying) return;
        poolsPrepared = true;
        regularHealthPrefab = Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/RegularEnemyHealthBar");
        bossHealthPrefab = Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/BossEnemyHealthBar");
        defenseHealthPrefab = Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/DefenseShieldHealthBar");
        if (regularHealthPrefab == null || bossHealthPrefab == null)
            Debug.LogError("Enemy health bar prefabs are missing from Resources/UI/EnemyHealthBars.", this);
        if (defenseHealthPrefab == null)
            Debug.LogError("Defense shield health bar prefab is missing from Resources/UI/EnemyHealthBars.", this);
        for (int i = 0; i < prewarmHealthLabels; i++)
        {
            EnemyHealthBarView bar = CreateHealthBar(false);
            if (bar != null) regularHealthPool.Enqueue(bar);
        }
        for (int i = 0; i < prewarmDamageLabels; i++)
            damagePool.Enqueue(CreateLabel("Pooled Damage Number", damageFontSize, damageColor));
    }

    public void Register(Brick enemy, float health)
    {
        if (enemy == null) return;
        healthValues[enemy] = new HealthState { Health = health, Text = FormatPositive(health) };
    }

    public void RegisterDefense(MonoBehaviour target)
    {
        if (target == null || !(target is IDefenseHealthTarget) || defenseTargets.Contains(target)) return;
        defenseTargets.Add(target);
    }

    public void UnregisterDefense(MonoBehaviour target)
    {
        if (ReferenceEquals(target, null)) return;
        defenseTargets.Remove(target);
        ReleaseDefenseLabel(target);
    }

    public void SetHealth(Brick enemy, float health)
    {
        if (enemy == null) return;
        if (!healthValues.TryGetValue(enemy, out HealthState state))
        {
            Register(enemy, health);
            state = healthValues[enemy];
        }
        state.Health = health;
        state.Text = FormatPositive(health);
        if (visibleHealth.TryGetValue(enemy, out EnemyHealthBarView bar))
            bar.SetValue(state.Text, enemy.MaxHp > 0f ? health / enemy.MaxHp : 0f);
    }

    public string GetHealthText(Brick enemy)
    {
        return enemy != null && healthValues.TryGetValue(enemy, out HealthState state) ? state.Text : null;
    }

    public void ShowDamage(Brick enemy, float damage)
    {
        ShowDamage(enemy, damage, damageColor, null);
    }

    public void ShowDamage(Brick enemy, float damage, CombatDamageSource source)
    {
        ShowDamage(enemy, damage, CombatDamagePalette.ColorFor(source),
            CombatDamagePalette.PrefixFor(source), source);
    }

    public void BeginAfterTurnBatch()
    {
        if (afterTurnBatchActive) FinishAfterTurnBatch();
        afterTurnBatchActive = true;
        pendingAfterTurn.Clear();
        pendingOrder.Clear();
    }

    public void QueueAfterTurnDamage(Brick enemy, float damage, CombatDamageSource source)
    {
        if (enemy == null || damage <= 0f) return;
        if (!afterTurnBatchActive)
        {
            ShowDamage(enemy, damage, source);
            return;
        }
        long key = ((long)enemy.GetInstanceID() << 32) | (uint)source;
        if (pendingAfterTurn.TryGetValue(key, out PendingDamage pending))
        {
            pending.Total += damage;
            return;
        }
        Camera camera = ResolveCamera();
        if (camera == null || !TryGetCanvasPosition(enemy, camera, healthOffset + 24f,
            out Vector2 start, false)) return;
        pending = new PendingDamage {
            Total = damage,
            Start = start,
            WorldPosition = enemy.transform.position,
            RoomDarkness = enemy.levelGenerator != null ? enemy.levelGenerator.Darkness : null,
            Source = source
        };
        pendingAfterTurn.Add(key, pending);
        pendingOrder.Add(key);
    }

    public void FinishAfterTurnBatch()
    {
        if (!afterTurnBatchActive) return;
        afterTurnBatchActive = false;
        for (int i = 0; i < pendingOrder.Count; i++)
        {
            PendingDamage pending = pendingAfterTurn[pendingOrder[i]];
            SpawnDamageLabel(pending.Total, pending.Start, pending.WorldPosition,
                pending.RoomDarkness, CombatDamagePalette.ColorFor(pending.Source),
                CombatDamagePalette.PrefixFor(pending.Source));
        }
        pendingAfterTurn.Clear();
        pendingOrder.Clear();
    }

    public void ShowDamage(Brick enemy, float damage, Color color, string prefix, CombatDamageSource source = CombatDamageSource.Other)
    {
        if (enemy == null || damage <= 0f) return;
        Camera camera = ResolveCamera();
        // Damage is combat feedback, not enemy information: it stays visible in darkness.
        if (camera == null || !TryGetCanvasPosition(enemy, camera, healthOffset + 24f,
            out Vector2 start, false)) return;

        long key = ((long)enemy.GetInstanceID() << 32) | (uint)source;
        // A fixed short window avoids a permanent label during sustained bites.
        if (source != CombatDamageSource.Other && recentDamage.TryGetValue(key, out var recent) && recent.Elapsed <= 0.12f)
        {
            recent.Total += damage;
            recent.Label.text = (string.IsNullOrEmpty(prefix) ? string.Empty : prefix) + "-" + FormatPositive(recent.Total);
            return;
        }
        var entry = SpawnDamageLabel(damage, start, enemy.transform.position,
            enemy.levelGenerator != null ? enemy.levelGenerator.Darkness : null, color, prefix);
        if (source != CombatDamageSource.Other)
        {
            entry.GroupKey = key;
            recentDamage[key] = entry;
        }
    }

    private DamageEntry SpawnDamageLabel(float damage, Vector2 start, Vector3 worldPosition,
        RoomDarkness darkness, Color color, string prefix)
    {
        if (damageEntries.Count >= Mathf.Max(8, maximumDamageLabels)) ReleaseDamageAt(0);
        TextMeshProUGUI label = damagePool.Count > 0
            ? damagePool.Dequeue()
            : CreateLabel("Damage Number", damageFontSize, damageColor);
        label.gameObject.SetActive(true);
        label.text = (string.IsNullOrEmpty(prefix) ? string.Empty : prefix) + "-" + FormatPositive(damage);
        label.color = color;
        label.alpha = 1f;
        label.rectTransform.localScale = Vector3.one * 0.82f;

        int spreadIndex = damageSequence++ % 5;
        start.x += (spreadIndex - 2) * 13f;
        label.rectTransform.anchoredPosition = start;
        var entry = entryPool.Count > 0 ? entryPool.Pop() : new DamageEntry();
        entry.Label = label; entry.Start = start; entry.RoomDarkness = darkness; entry.WorldPosition = worldPosition;
        entry.Elapsed = 0f; entry.GroupKey = 0; entry.Total = damage;
        damageEntries.Add(entry);
        return entry;
    }

    public void Unregister(Brick enemy)
    {
        if (ReferenceEquals(enemy, null)) return;
        healthValues.Remove(enemy);
        ReleaseHealthLabel(enemy);
    }

    private void LateUpdate()
    {
        Camera camera = ResolveCamera();
        cleanup.Clear();
        foreach (KeyValuePair<Brick, HealthState> pair in healthValues)
        {
            Brick enemy = pair.Key;
            if (enemy == null)
            {
                cleanup.Add(enemy);
                continue;
            }
            if (camera != null && enemy.gameObject.activeInHierarchy
                && TryGetHealthBarPosition(enemy, camera, out Vector2 position, out float width))
            {
                if (!visibleHealth.TryGetValue(enemy, out EnemyHealthBarView bar))
                {
                    Queue<EnemyHealthBarView> pool = enemy.UsesBossHealth ? bossHealthPool : regularHealthPool;
                    bar = pool.Count > 0 ? pool.Dequeue() : CreateHealthBar(enemy.UsesBossHealth);
                    if (bar == null) continue;
                    bar.gameObject.SetActive(true);
                    visibleHealth.Add(enemy, bar);
                }
                HealthState state = pair.Value;
                bar.SetValue(state.Text, enemy.MaxHp > 0f ? enemy.hp / enemy.MaxHp : 0f);
                bar.SetDimensions(width, enemy.UsesBossHealth ? 42f : 34f);
                if ((bar.RectTransform.anchoredPosition - position).sqrMagnitude > 0.01f)
                    bar.RectTransform.anchoredPosition = position;
            }
            else ReleaseHealthLabel(enemy);
        }
        for (int i = 0; i < cleanup.Count; i++) Unregister(cleanup[i]);
        UpdateDefenseHealth(camera);
        AnimateDamageNumbers();
    }

    private void UpdateDefenseHealth(Camera camera)
    {
        for (int i = defenseTargets.Count - 1; i >= 0; i--)
        {
            MonoBehaviour owner = defenseTargets[i];
            if (owner == null)
            {
                ReleaseDefenseLabel(owner);
                defenseTargets.RemoveAt(i);
                continue;
            }
            IDefenseHealthTarget target = (IDefenseHealthTarget)owner;
            Vector3 world = target.DefenseHealthAnchor;
            Vector3 viewport = camera != null ? camera.WorldToViewportPoint(world) : Vector3.zero;
            bool visible = camera != null && canvasRect != null && owner.gameObject.activeInHierarchy
                && viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
            if (!visible || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                    camera.WorldToScreenPoint(world), null, out Vector2 position))
            {
                ReleaseDefenseLabel(owner);
                continue;
            }
            if (!visibleDefense.TryGetValue(owner, out EnemyHealthBarView bar))
            {
                bar = defenseHealthPool.Count > 0 ? defenseHealthPool.Dequeue() : CreateDefenseHealthBar();
                if (bar == null) continue;
                bar.gameObject.SetActive(true);
                visibleDefense.Add(owner, bar);
            }
            float maximum = Mathf.Max(0f, target.DefenseMaxHealth);
            float current = Mathf.Max(0f, target.DefenseHealth);
            bar.SetValue(FormatDefenseHealth(current) + " / " + FormatDefenseHealth(maximum),
                maximum > 0f ? current / maximum : 0f);
            bar.SetDimensions(180f, 42f);
            position.y -= 42f * 0.5f + 8f;
            bar.RectTransform.anchoredPosition = position;
        }
    }

    private EnemyHealthBarView CreateDefenseHealthBar()
    {
        if (defenseHealthPrefab == null)
            defenseHealthPrefab = Resources.Load<EnemyHealthBarView>("UI/EnemyHealthBars/DefenseShieldHealthBar");
        if (defenseHealthPrefab == null) return null;
        EnemyHealthBarView bar = Instantiate(defenseHealthPrefab, transform, false);
        bar.name = "Defense Shield Health Bar";
        bar.gameObject.SetActive(false);
        return bar;
    }

    private void ReleaseDefenseLabel(MonoBehaviour owner)
    {
        if (!visibleDefense.TryGetValue(owner, out EnemyHealthBarView bar)) return;
        visibleDefense.Remove(owner);
        if (bar == null) return;
        bar.gameObject.SetActive(false);
        defenseHealthPool.Enqueue(bar);
    }

    private static string FormatDefenseHealth(float value)
    {
        if (value >= 1000f) return CompactNumberFormatter.Format(value);
        return value.ToString(value < 10f ? "0.##" : "0.#",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private void AnimateDamageNumbers()
    {
        float duration = Mathf.Max(0.1f, damageDuration);
        for (int i = damageEntries.Count - 1; i >= 0; i--)
        {
            DamageEntry entry = damageEntries[i];
            Vector3 viewport = gameplayCamera != null ? gameplayCamera.WorldToViewportPoint(entry.WorldPosition) : Vector3.zero;
            if (entry.RoomDarkness != null && (!entry.RoomDarkness.gameObject.activeInHierarchy
                || viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f))
            {
                ReleaseDamageAt(i);
                continue;
            }
            entry.Elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(entry.Elapsed / duration);
            float rise = 1f - Mathf.Pow(1f - progress, 3f);
            entry.Label.rectTransform.anchoredPosition = entry.Start + Vector2.up * (damageTravel * rise);
            float scale = Mathf.Lerp(0.82f, 1.08f, Mathf.Sin(Mathf.Min(1f, progress * 2f) * Mathf.PI * 0.5f));
            entry.Label.rectTransform.localScale = Vector3.one * scale;
            entry.Label.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, progress));
            if (progress >= 1f) ReleaseDamageAt(i);
        }
    }

    private TextMeshProUGUI CreateLabel(string labelName, float fontSize, Color color)
    {
        GameObject child = new GameObject(labelName, typeof(RectTransform), typeof(CanvasRenderer));
        child.transform.SetParent(transform, false);
        TextMeshProUGUI label = child.AddComponent<TextMeshProUGUI>();
        GameTypography.Apply(label);
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = color;
        label.outlineColor = Color.black;
        label.outlineWidth = GameTypography.TmpOutlineWidth;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(180f, 54f);
        child.SetActive(false);
        return label;
    }

    private EnemyHealthBarView CreateHealthBar(bool boss)
    {
        EnemyHealthBarView prefab = boss ? bossHealthPrefab : regularHealthPrefab;
        if (prefab == null) return null;
        EnemyHealthBarView bar = Instantiate(prefab, transform, false);
        bar.name = boss ? "Boss Health Bar" : "Enemy Health Bar";
        bar.gameObject.SetActive(false);
        return bar;
    }

    private Camera ResolveCamera()
    {
        if (gameplayCamera != null && gameplayCamera.isActiveAndEnabled) return gameplayCamera;
        gameplayCamera = Camera.main;
        if (gameplayCamera == null) gameplayCamera = FindObjectOfType<Camera>();
        return gameplayCamera;
    }

    private bool TryGetCanvasPosition(Brick enemy, Camera camera, float verticalOffset,
        out Vector2 position, bool requireEnemyVisibility = true)
    {
        position = Vector2.zero;
        if (canvasRect == null || enemy == null || !enemy.gameObject.activeInHierarchy) return false;
        RoomDarkness darkness = enemy.levelGenerator != null ? enemy.levelGenerator.Darkness : null;
        if (requireEnemyVisibility && darkness != null && !darkness.IsVisible(enemy.transform.position)) return false;
        Vector3 viewport = camera.WorldToViewportPoint(enemy.CombatTextAnchor);
        if (viewport.z <= 0f || viewport.x < -0.02f || viewport.x > 1.02f
            || viewport.y < -0.02f || viewport.y > 1.02f) return false;
        Vector3 screen = camera.WorldToScreenPoint(enemy.CombatTextAnchor);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out position)) return false;
        position.y += verticalOffset;
        return true;
    }

    private bool TryGetHealthBarPosition(Brick enemy, Camera camera,
        out Vector2 position, out float width)
    {
        position = Vector2.zero;
        width = 0f;
        if (canvasRect == null || enemy == null || !enemy.gameObject.activeInHierarchy) return false;
        RoomDarkness darkness = enemy.levelGenerator != null ? enemy.levelGenerator.Darkness : null;
        if (darkness != null && !darkness.IsVisible(enemy.transform.position)) return false;
        Vector3 world = new Vector3(enemy.transform.position.x,
            enemy.LowestCellY - 0.5f, enemy.transform.position.z);
        Vector3 viewport = camera.WorldToViewportPoint(world);
        if (viewport.z <= 0f || viewport.x < -0.02f || viewport.x > 1.02f
            || viewport.y < -0.02f || viewport.y > 1.02f) return false;
        Vector3 left = world + Vector3.left * (enemy.Footprint.x * 0.4f);
        Vector3 right = world + Vector3.right * (enemy.Footprint.x * 0.4f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            camera.WorldToScreenPoint(world), null, out position)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            camera.WorldToScreenPoint(left), null, out Vector2 leftLocal)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            camera.WorldToScreenPoint(right), null, out Vector2 rightLocal)) return false;
        width = Mathf.Abs(rightLocal.x - leftLocal.x);
        // Keep the whole bar below the occupied cell so it reads as a strip
        // between enemy rows instead of blending into the bottom of the head.
        position.y -= (enemy.UsesBossHealth ? 42f : 34f) * 0.5f;
        return true;
    }

    private void ReleaseHealthLabel(Brick enemy)
    {
        if (ReferenceEquals(enemy, null) || !visibleHealth.TryGetValue(enemy, out EnemyHealthBarView bar)) return;
        visibleHealth.Remove(enemy);
        if (bar == null) return;
        bar.gameObject.SetActive(false);
        (enemy.UsesBossHealth ? bossHealthPool : regularHealthPool).Enqueue(bar);
    }

    private void ReleaseDamageAt(int index)
    {
        DamageEntry entry = damageEntries[index];
        damageEntries.RemoveAt(index);
        if (entry.GroupKey != 0 && recentDamage.TryGetValue(entry.GroupKey, out var recent)
            && ReferenceEquals(recent, entry)) recentDamage.Remove(entry.GroupKey);
        entry.Label.gameObject.SetActive(false);
        damagePool.Enqueue(entry.Label);
        entry.Label = null;
        entry.RoomDarkness = null;
        if (entryPool.Count < maximumDamageLabels) entryPool.Push(entry);
    }

    private static string FormatPositive(float value)
    {
        return CompactNumberFormatter.Format(System.Math.Max(0d, value));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        healthValues.Clear();
        visibleHealth.Clear();
        regularHealthPool.Clear();
        bossHealthPool.Clear();
        defenseTargets.Clear();
        visibleDefense.Clear();
        defenseHealthPool.Clear();
        damagePool.Clear();
        damageEntries.Clear();
        recentDamage.Clear();
        entryPool.Clear();
        pendingAfterTurn.Clear();
        pendingOrder.Clear();
    }
}

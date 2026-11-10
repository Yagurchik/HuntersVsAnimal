using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FireflyBonfire : MonoBehaviour
{
    private static readonly List<FireflyBonfire> All = new List<FireflyBonfire>();
    private static FireflyBonfire prefab;

    private LevelGenerator room;
    private EnemyIllumination illumination;
    [SerializeField] private ParticleSystem flames;
    [SerializeField, Min(0.5f)] private float fadeDuration = 3f;
    [SerializeField, Range(0f, 1f)] private float occupiedOpacity = 0.45f;
    [SerializeField, Min(0.01f)] private float opacityTransitionDuration = 0.15f;
    private static readonly int TintProperty = Shader.PropertyToID("_Color");
    private ParticleSystemRenderer flameRenderer;
    private MaterialPropertyBlock flameProperties;
    private Color flameTint = Color.white;
    private float visualOpacity = 1f;
    private float damagePerTurn;
    private int damageOwner = AnimalProjectileQueue.FireflyIndex;
    private float damageRadius;
    private int remainingTurns;
    private int lastProcessedTurn = -1;
    private bool initialized;
    private bool fading;

    public static int ActiveCount => All.Count;
    public static FireflyBonfire LastSpawned { get; private set; }
    public float DamagePerTurn => damagePerTurn;
    public float DamageRadius => damageRadius;
    public float LightRadius => illumination != null ? illumination.LightRadius : 0f;
    public int RemainingTurns => remainingTurns;
    public LevelGenerator Room => room;
    public bool IsFading => fading;
    public float FadeDuration => fadeDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        All.Clear();
        LastSpawned = null;
        prefab = null;
    }

    public static FireflyBonfire Spawn(LevelGenerator targetRoom, Vector3 worldPosition,
        float damage, float radius, float lightRadius, int durationTurns,
        int ownerIndex = AnimalProjectileQueue.FireflyIndex)
    {
        if (targetRoom == null || targetRoom.IsLost || damage <= 0f || durationTurns <= 0)
            return null;

        if (prefab == null) prefab = Resources.Load<FireflyBonfire>("Effects/Firefly/Bonfire");
        if (prefab == null)
        {
            Debug.LogError("The authored Effects/Firefly/Bonfire prefab is missing.");
            return null;
        }
        FireflyBonfire bonfire = Instantiate(prefab, worldPosition, Quaternion.identity, targetRoom.transform);
        EnemyIllumination light = bonfire.GetComponent<EnemyIllumination>();
        bonfire.damageOwner = ownerIndex;
        bonfire.Initialize(targetRoom, light, damage, radius, lightRadius, durationTurns);
        LastSpawned = bonfire;
        return bonfire;
    }

    public static int ProcessAllAfterEnemyTurn(int turnNumber)
    {
        int hits = 0;
        int initialCount = All.Count;
        for (int i = initialCount - 1; i >= 0; i--)
        {
            if (i >= All.Count) continue;
            FireflyBonfire bonfire = All[i];
            if (bonfire == null)
            {
                All.RemoveAt(i);
                continue;
            }
            hits += bonfire.ProcessAfterEnemyTurn(turnNumber);
        }
        return hits;
    }

    private void Initialize(LevelGenerator targetRoom, EnemyIllumination light, float damage,
        float radius, float lightRadius, int durationTurns)
    {
        room = targetRoom;
        illumination = light;
        damagePerTurn = Mathf.Max(0.1f, damage);
        damageRadius = Mathf.Max(0.1f, radius);
        remainingTurns = Mathf.Max(1, durationTurns);
        name = "Firefly Bonfire (" + remainingTurns + " turns)";
        if (flames != null)
        {
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            FireflyFlameArt.Apply(flames, flames.GetComponent<ParticleSystemRenderer>());
            flameRenderer = flames.GetComponent<ParticleSystemRenderer>();
            flameProperties = new MaterialPropertyBlock();
            if (flameRenderer != null && flameRenderer.sharedMaterial != null
                && flameRenderer.sharedMaterial.HasProperty(TintProperty))
                flameTint = flameRenderer.sharedMaterial.GetColor(TintProperty);
            flames.Play(true);
        }
        illumination.Illuminate(Mathf.Max(0.1f, lightRadius));
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || flameRenderer == null) return;
        float targetOpacity = HasEnemyOnFire() ? occupiedOpacity : 1f;
        visualOpacity = Mathf.MoveTowards(visualOpacity, targetOpacity,
            Time.deltaTime / Mathf.Max(0.01f, opacityTransitionDuration));
        // A per-renderer tint affects both existing and new particles while
        // preserving their own lifetime fade and the shared fire material.
        flameRenderer.GetPropertyBlock(flameProperties);
        Color tint = flameTint;
        tint.a *= visualOpacity;
        flameProperties.SetColor(TintProperty, tint);
        flameRenderer.SetPropertyBlock(flameProperties);
    }

    private bool HasEnemyOnFire()
    {
        if (room == null) return false;
        Vector3 firePosition = transform.position;
        foreach (GameObject enemyObject in room.EnemyCollection)
        {
            if (enemyObject == null || !enemyObject.activeInHierarchy
                || !enemyObject.TryGetComponent(out Brick enemy) || !enemy.OccupiesGrid) continue;
            Vector2Int size = enemy.Footprint;
            Vector3 center = enemy.transform.position;
            int x = Mathf.FloorToInt(firePosition.x - center.x + size.x * 0.5f);
            int y = Mathf.FloorToInt(center.y - firePosition.y + size.y * 0.5f);
            if (x >= 0 && y >= 0 && x < size.x && y < size.y
                && (enemy.UsesBossHealth || enemy.OccupiesFootprintOffset(new Vector2Int(x, y))))
                return true;
        }
        return false;
    }

    private int ProcessAfterEnemyTurn(int turnNumber)
    {
        if (!initialized || fading || turnNumber <= lastProcessedTurn) return 0;
        lastProcessedTurn = turnNumber;
        if (room == null || room.IsLost)
        {
            Expire();
            return 0;
        }

        int hits = 0;
        float radiusSquared = damageRadius * damageRadius;
        // Work backwards because lethal damage removes enemies from the room list.
        for (int i = room.EnemyCollection.Count - 1; i >= 0; i--)
        {
            GameObject enemyObject = room.EnemyCollection[i];
            if (enemyObject == null || !enemyObject.TryGetComponent(out Brick enemy)
                || enemy.IsDefeated) continue;
            if (((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude
                > radiusSquared + 0.0001f) continue;
            enemy.TakeAfterTurnDamage(damagePerTurn, CombatDamageSource.Fire, damageOwner);
            hits++;
        }

        remainingTurns--;
        name = "Firefly Bonfire (" + Mathf.Max(0, remainingTurns) + " turns)";
        if (remainingTurns <= 0) Expire();
        return hits;
    }

    private void OnEnable()
    {
        if (!fading && !All.Contains(this)) All.Add(this);
        if (initialized && !fading && flames != null) flames.Play(true);
    }

    private void OnDisable()
    {
        All.Remove(this);
        if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // Leaving a room can stop its coroutines. Spent visual embers must not
        // remain as an abandoned effect when that room is activated again.
        if (fading && Application.isPlaying) Destroy(gameObject);
    }

    private void Expire()
    {
        if (fading) return;
        fading = true;
        remainingTurns = 0;
        All.Remove(this);
        illumination?.Extinguish();
        if (!Application.isPlaying) { DestroyImmediate(gameObject); return; }
        if (room == null || room.IsLost) { Destroy(gameObject); return; }
        name = "Bonfire Embers";
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float initialRate = flames != null ? flames.emission.rateOverTime.constant : 0f;
        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            if (room == null || room.IsLost) break;
            if (flames != null)
            {
                var emission = flames.emission;
                float strength = 1f - Mathf.Clamp01(elapsed / (fadeDuration * 0.4f));
                emission.rateOverTime = initialRate * strength * strength;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (LastSpawned == this) LastSpawned = null;
    }
}

public static class FireflyFlameArt
{
    public const int Columns = 11;
    public const int Rows = 5;
    public const int FigureCount = Columns * Rows;
    private const string ResourcePath = "Effects/Firefly/firefly-flames-55";

    private static Texture2D atlas;
    private static Sprite[] figures;
    private static Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        atlas = null;
        figures = null;
        material = null;
    }

    public static bool Apply(ParticleSystem particles, ParticleSystemRenderer renderer)
    {
        if (particles == null || renderer == null || !EnsureLoaded()) return false;

        ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
        animation.enabled = true;
        animation.mode = ParticleSystemAnimationMode.Sprites;
        for (int i = 0; i < figures.Length; i++) animation.AddSprite(figures[i]);
        // Every emitted particle keeps one randomly selected figure. Its existing
        // movement, size, noise and fade modules continue to animate the fire.
        animation.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        animation.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        animation.cycleCount = 1;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        return true;
    }

    private static bool EnsureLoaded()
    {
        if (figures != null && figures.Length == FigureCount && material != null) return true;
        atlas = Resources.Load<Texture2D>(ResourcePath);
        if (atlas == null) return false;

        figures = new Sprite[FigureCount];
        int index = 0;
        for (int row = 0; row < Rows; row++)
        {
            // The generated atlas dimensions are not guaranteed to divide evenly
            // by 11x5. Integer boundaries keep the final cell inside the texture.
            int sourceRow = Rows - 1 - row;
            int yMin = sourceRow * atlas.height / Rows;
            int yMax = (sourceRow + 1) * atlas.height / Rows;
            for (int column = 0; column < Columns; column++)
            {
                int xMin = column * atlas.width / Columns;
                int xMax = (column + 1) * atlas.width / Columns;
                int cellWidth = Mathf.Max(1, xMax - xMin);
                int cellHeight = Mathf.Max(1, yMax - yMin);
                figures[index] = Sprite.Create(atlas,
                    new Rect(xMin, yMin, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f), Mathf.Max(cellWidth, cellHeight), 0,
                    SpriteMeshType.FullRect);
                figures[index].name = "Firefly Flame " + (index + 1).ToString("00");
                index++;
            }
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) return false;
        material = new Material(shader)
        {
            name = "Runtime Firefly Flame Figures",
            mainTexture = atlas,
            hideFlags = HideFlags.HideAndDontSave
        };
        return true;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>A visible room hazard left when enemy retaliation kills the honey badger.</summary>
[DisallowMultipleComponent]
public sealed class HoneyBadgerPoisonCloud : MonoBehaviour
{
    private static readonly List<HoneyBadgerPoisonCloud> All = new List<HoneyBadgerPoisonCloud>();
    private static Material sharedPoisonMaterial;
    private static Material sharedPuffMaterial;

    private LevelGenerator room;
    private ParticleSystem particles;
    private ParticleSystem flames;
    private Sprite puffSprite;
    private EnemyIllumination illumination;
    private float damagePerTurn;
    private float damageRadius;
    private int remainingTurns;
    private int lastProcessedTurn = -1;
    private bool initialized;
    private bool ignited;

    public static int ActiveCount => All.Count;
    public static HoneyBadgerPoisonCloud LastSpawned { get; private set; }
    public float DamagePerTurn => damagePerTurn;
    public float DamageRadius => damageRadius;
    public int RemainingTurns => remainingTurns;
    public LevelGenerator Room => room;
    public bool IsIgnited => ignited;
    public float LightRadius => illumination != null && illumination.IsIlluminated
        ? illumination.LightRadius : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        All.Clear();
        LastSpawned = null;
        sharedPoisonMaterial = null;
        sharedPuffMaterial = null;
    }

    public static HoneyBadgerPoisonCloud Spawn(LevelGenerator targetRoom, Vector3 worldPosition,
        float damage, float radius, int durationTurns, Sprite cloudPuffSprite = null)
    {
        if (targetRoom == null || targetRoom.IsLost || damage <= 0f || durationTurns <= 0)
            return null;

        GameObject cloudObject = new GameObject("Honey Badger Poison Cloud");
        cloudObject.SetActive(false);
        cloudObject.transform.SetParent(targetRoom.transform, true);
        cloudObject.transform.position = worldPosition;
        HoneyBadgerPoisonCloud cloud = cloudObject.AddComponent<HoneyBadgerPoisonCloud>();
        cloud.Initialize(targetRoom, damage, radius, durationTurns, cloudPuffSprite);
        cloudObject.SetActive(true);
        LastSpawned = cloud;
        return cloud;
    }

    public static int ProcessAllAfterEnemyTurn(int turnNumber)
    {
        int hits = 0;
        int initialCount = All.Count;
        for (int i = initialCount - 1; i >= 0; i--)
        {
            if (i >= All.Count) continue;
            HoneyBadgerPoisonCloud cloud = All[i];
            if (cloud == null)
            {
                All.RemoveAt(i);
                continue;
            }
            hits += cloud.ProcessAfterEnemyTurn(turnNumber);
        }
        return hits;
    }

    private void Initialize(LevelGenerator targetRoom, float damage, float radius,
        int durationTurns, Sprite cloudPuffSprite)
    {
        room = targetRoom;
        puffSprite = cloudPuffSprite;
        damagePerTurn = Mathf.Max(0.1f, damage);
        damageRadius = Mathf.Max(0.1f, radius);
        remainingTurns = Mathf.Max(1, durationTurns);
        name = "Honey Badger Poison Cloud (" + remainingTurns + " turn)";
        CreateParticles();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || ignited || Time.timeScale <= 0f) return;
        for (int i = Player.animalsCollect.Count - 1; i >= 0; i--)
        {
            Animal projectile = Player.animalsCollect[i];
            if (projectile != null && TryIgnite(projectile)) return;
        }
    }

    public bool TryIgnite(Animal projectile)
    {
        if (!initialized || ignited || projectile == null
            || projectile.GetComponent<FireflyImpactEffect>() == null) return false;

        Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
        float projectileRadius = projectileCollider != null
            ? Mathf.Max(projectileCollider.bounds.extents.x, projectileCollider.bounds.extents.y)
            : 0f;
        float contactRadius = damageRadius + projectileRadius;
        if (((Vector2)projectile.transform.position - (Vector2)transform.position).sqrMagnitude
            > contactRadius * contactRadius + 0.0001f) return false;

        Ignite();
        return true;
    }

    private void Ignite()
    {
        if (ignited) return;
        ignited = true;
        name = "Burning Honey Badger Poison Cloud (" + remainingTurns + " turn)";

        if (particles != null)
        {
            ParticleSystem.MainModule poisonMain = particles.main;
            poisonMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.35f, 0.04f, 0.8f),
                new Color(1f, 0.84f, 0.12f, 0.95f));
            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            Gradient fireGradient = new Gradient();
            fireGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.62f, 0.18f), 0.55f),
                    new GradientColorKey(new Color(0.75f, 0.18f, 0.04f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.22f),
                    new GradientAlphaKey(0.65f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colors.color = fireGradient;
            particles.Clear(true);
            particles.Play(true);
        }

        CreateIgnitedFlames();
        illumination = GetComponent<EnemyIllumination>();
        if (illumination == null) illumination = gameObject.AddComponent<EnemyIllumination>();
        illumination.Illuminate(damageRadius);
    }

    private int ProcessAfterEnemyTurn(int turnNumber)
    {
        if (!initialized || turnNumber <= lastProcessedTurn) return 0;
        lastProcessedTurn = turnNumber;
        if (room == null || room.IsLost)
        {
            Expire();
            return 0;
        }

        int hits = 0;
        float radiusSquared = damageRadius * damageRadius;
        for (int i = room.EnemyCollection.Count - 1; i >= 0; i--)
        {
            GameObject enemyObject = room.EnemyCollection[i];
            if (enemyObject == null || !enemyObject.TryGetComponent(out Brick enemy)
                || enemy.IsDefeated) continue;
            if (((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude
                > radiusSquared + 0.0001f) continue;
            enemy.TakePoisonDamage(damagePerTurn, ignited);
            hits++;
        }

        remainingTurns--;
        name = (ignited ? "Burning Honey Badger Poison Cloud (" : "Honey Badger Poison Cloud (")
            + Mathf.Max(0, remainingTurns) + " turns)";
        if (remainingTurns <= 0) Expire();
        return hits;
    }

    private void CreateParticles()
    {
        GameObject visual = new GameObject("Poison Fog");
        visual.SetActive(false);
        visual.transform.SetParent(transform, false);
        particles = visual.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.18f, damageRadius * 0.28f), Mathf.Max(0.32f, damageRadius * 0.58f));
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.22f, 0.9f, 0.12f, 0.75f),
            new Color(0.42f, 1f, 0.22f, 0.92f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 42;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = damageRadius * 0.62f;

        ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
        colors.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.75f, 1f, 0.68f), 0f),
                new GradientColorKey(new Color(0.42f, 0.9f, 0.35f), 0.55f),
                new GradientColorKey(new Color(0.22f, 0.58f, 0.17f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.22f),
                new GradientAlphaKey(0.6f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colors.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.18f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.18f;
        noise.frequency = 0.7f;

        ParticleSystemRenderer renderer = visual.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 24;
        if (puffSprite != null)
        {
            ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
            animation.enabled = true;
            animation.mode = ParticleSystemAnimationMode.Sprites;
            animation.AddSprite(puffSprite);
        }
        Material material = GetPoisonMaterial(puffSprite);
        if (material != null) renderer.sharedMaterial = material;
        visual.SetActive(true);
    }

    private void CreateIgnitedFlames()
    {
        GameObject visual = new GameObject("Ignited Poison Fire");
        visual.SetActive(false);
        visual.transform.SetParent(transform, false);
        flames = visual.AddComponent<ParticleSystem>();
        flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = flames.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.68f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 110;

        ParticleSystem.EmissionModule emission = flames.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(damageRadius * 38f, 34f, 72f);

        ParticleSystem.ShapeModule shape = flames.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = damageRadius * 0.82f;

        ParticleSystem.ColorOverLifetimeModule colors = flames.colorOverLifetime;
        colors.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.58f),
                new GradientColorKey(new Color(1f, 0.82f, 0.7f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.72f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        colors.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.24f, 1.15f),
                new Keyframe(1f, 0.12f)));

        ParticleSystem.NoiseModule noise = flames.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.2f;
        noise.frequency = 1.15f;

        ParticleSystemRenderer renderer = visual.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 28;
        Material material = GetPoisonMaterial();
        if (material != null) renderer.sharedMaterial = material;
        FireflyFlameArt.Apply(flames, renderer);

        visual.SetActive(true);
        flames.Play(true);
        flames.Emit(Mathf.Clamp(Mathf.RoundToInt(damageRadius * 28f), 24, 56));
    }

    private static Material GetPoisonMaterial(Sprite sprite = null)
    {
        Material cached = sprite != null ? sharedPuffMaterial : sharedPoisonMaterial;
        if (cached != null) return cached;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) return null;
        Material material = new Material(shader) {
            name = sprite != null ? "Honey Badger Poison Puffs" : "Honey Badger Poison Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (sprite != null)
        {
            material.mainTexture = sprite.texture;
            sharedPuffMaterial = material;
        }
        else sharedPoisonMaterial = material;
        return material;
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        if (initialized && particles != null) particles.Play(true);
    }

    private void OnDisable()
    {
        All.Remove(this);
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (flames != null)
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Expire()
    {
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (LastSpawned == this) LastSpawned = null;
    }
}

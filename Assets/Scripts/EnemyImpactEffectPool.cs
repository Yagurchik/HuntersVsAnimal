using System.Collections.Generic;
using UnityEngine;

// Lightweight pooled fallback used by enemies that do not have a custom impact prefab yet.
[DisallowMultipleComponent]
public sealed class EnemyImpactEffectPool : MonoBehaviour
{
    private const int MaximumPoolSize = 24;
    private const float EffectLifetime = 0.65f;
    private const float MaximumSplashScale = 1.25f;
    private const float MaximumLiquidParticleSize = 0.3f;
    private const float MaximumParticleScreenSize = 0.06f;
    private static readonly Queue<EnemyImpactEffectPool> Pool = new Queue<EnemyImpactEffectPool>();
    private static Transform poolRoot;
    private static Material sharedMaterial;
    private static Sprite bogDropSprite;
    private static Material bogFireMaterial;
    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private float releaseAt;
    private bool activeEffect;
    private bool liquidBurst;
    private float liquidStartedAt;
    private float liquidGravity;
    private float liquidTravelRadius;
    private int liquidDropCount;
    private readonly LiquidDrop[] liquidDrops = new LiquidDrop[32];

    private sealed class LiquidDrop
    {
        public SpriteRenderer Renderer;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Lifetime;
    }

    public static int ActiveEffectCount { get; private set; }
    public static EnemyImpactEffectPool LastPlayed { get; private set; }
    public int SortingOrder => particleRenderer != null ? particleRenderer.sortingOrder : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Pool.Clear();
        poolRoot = null;
        sharedMaterial = null;
        bogDropSprite = null;
        bogFireMaterial = null;
        ActiveEffectCount = 0;
        LastPlayed = null;
    }

    public static void PlayWhiteDust(Vector3 worldPosition, Vector2 surfaceNormal,
        Color color, int particleCount, float scale)
    {
        EnemyImpactEffectPool effect = Acquire();
        effect.EmitWhiteDust(worldPosition, surfaceNormal, color,
            Mathf.Clamp(particleCount, 1, 32), Mathf.Max(0.1f, scale));
    }

    public static void PlayBogSplash(Vector3 worldPosition, Vector2 surfaceNormal,
        int particleCount, float scale)
    {
        EnemyImpactEffectPool effect = Acquire();
        effect.EmitBogSplash(worldPosition, surfaceNormal,
            Mathf.Clamp(particleCount, 1, 32), float.IsNaN(scale) ? 0.9f
                : Mathf.Clamp(scale, 0.1f, MaximumSplashScale));
    }

    public static void PlayBogExplosion(Vector3 worldPosition, bool burning, float radius)
    {
        EnemyImpactEffectPool effect = Acquire();
        effect.EmitBogExplosion(worldPosition, burning, float.IsNaN(radius) ? 2f
            : Mathf.Clamp(radius, 0.1f, 4f));
    }

    private static EnemyImpactEffectPool Acquire()
    {
        while (Pool.Count > 0)
        {
            EnemyImpactEffectPool pooled = Pool.Dequeue();
            if (pooled != null) return pooled;
        }

        GameObject effectObject = new GameObject("Enemy Impact (pooled)");
        effectObject.SetActive(false);
        effectObject.transform.SetParent(GetPoolRoot(), false);
        effectObject.AddComponent<ParticleSystem>();
        return effectObject.AddComponent<EnemyImpactEffectPool>();
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null) return poolRoot;
        GameObject root = new GameObject("Enemy Impact Effects (runtime)");
        DontDestroyOnLoad(root);
        poolRoot = root.transform;
        return poolRoot;
    }

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        particleRenderer = GetComponent<ParticleSystemRenderer>();
        ConfigureParticles();
    }

    private void ConfigureParticles()
    {
        // A ParticleSystem added at runtime may briefly enter the playing state even
        // on an inactive GameObject. Duration can only be changed after a full stop.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = EffectLifetime;
        main.startLifetime = 0.4f;
        main.startSpeed = 1f;
        main.startSize = 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.82f, 0.86f, 0.9f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.38f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.65f), new Keyframe(0.28f, 1.1f), new Keyframe(1f, 1.45f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.16f;
        noise.frequency = 0.85f;
        noise.scrollSpeed = 0.2f;

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 20;
        Material material = GetSharedMaterial();
        if (material != null) particleRenderer.sharedMaterial = material;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null) return sharedMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) return null;
        sharedMaterial = new Material(shader) {
            name = "Runtime Enemy Impact Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedMaterial;
    }

    private static Material GetBogFireMaterial()
    {
        if (bogFireMaterial == null)
            bogFireMaterial = Resources.Load<Material>("Effects/SoulGlow");
        return bogFireMaterial != null ? bogFireMaterial : GetSharedMaterial();
    }

    private void PrepareEffect(Vector3 worldPosition)
    {
        transform.SetParent(GetPoolRoot(), false);
        transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        transform.localScale = Vector3.one;
        ClearLiquidDrops();
        gameObject.SetActive(true);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleRenderer.enabled = true;

        // Reset state on every reuse, rather than relying on the previous burst's settings.
        ParticleSystem.MainModule main = particles.main;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startSize3D = false;
        main.startSize = 0.12f;
        main.startRotation3D = false;
        main.startRotation = 0f;
        main.simulationSpeed = 1f;
        main.maxParticles = 64;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        // A close camera must never turn one droplet into a screen-sized billboard.
        particleRenderer.minParticleSize = 0f;
        particleRenderer.maxParticleSize = MaximumParticleScreenSize;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.lengthScale = 0f;
        particleRenderer.velocityScale = 0f;
        particleRenderer.cameraVelocityScale = 0f;
    }

    private void EmitWhiteDust(Vector3 worldPosition, Vector2 surfaceNormal,
        Color color, int particleCount, float scale)
    {
        PrepareEffect(worldPosition);
        ParticleSystem.MainModule main = particles.main;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.NoiseModule dustNoise = particles.noise;
        dustNoise.enabled = true;
        ParticleSystem.SizeOverLifetimeModule dustSize = particles.sizeOverLifetime;
        dustSize.enabled = true;
        dustSize.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.65f), new Keyframe(0.28f, 1.1f),
                new Keyframe(1f, 1.45f)));
        ParticleSystem.ColorOverLifetimeModule dustColor = particles.colorOverLifetime;
        dustColor.color = DustFade();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = GetSharedMaterial();
        particles.Play(true);

        Vector2 normal = surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized : Vector2.up;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        for (int i = 0; i < particleCount; i++)
        {
            float side = Random.Range(-1f, 1f);
            Vector2 direction = (normal * Random.Range(0.2f, 0.9f)
                + tangent * side + Random.insideUnitCircle * 0.45f).normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = normal;

            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams {
                position = Random.insideUnitCircle * (0.08f * scale),
                velocity = direction * Random.Range(0.55f, 1.45f) * scale,
                startLifetime = Random.Range(0.25f, 0.48f),
                startSize = Random.Range(0.08f, 0.2f) * scale,
                startColor = color,
                rotation = Random.Range(0f, 360f)
            };
            particles.Emit(particle, 1);
        }

        releaseAt = Time.time + EffectLifetime;
        activeEffect = true;
        ActiveEffectCount++;
        LastPlayed = this;
    }

    private void EmitBogSplash(Vector3 worldPosition, Vector2 surfaceNormal,
        int particleCount, float scale)
    {
        PrepareEffect(worldPosition);
        BeginLiquidBurst(0.45f * Physics2D.gravity.y, 0.65f);

        Vector2 normal = surfaceNormal.sqrMagnitude > 0.0001f
            ? surfaceNormal.normalized : Vector2.up;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 direction = normal * Random.Range(0.5f, 1.1f)
                + tangent * Random.Range(-0.9f, 0.9f)
                + Vector2.up * Random.Range(0.15f, 0.5f);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

            EmitLiquidDrop(Random.insideUnitCircle * (0.035f * scale),
                direction.normalized * Random.Range(0.9f, 1.8f) * scale,
                Random.Range(0.32f, 0.54f), Random.Range(0.12f, 0.22f) * scale);
        }

        releaseAt = Time.time + EffectLifetime;
        activeEffect = true;
        ActiveEffectCount++;
        LastPlayed = this;
    }

    private void EmitBogExplosion(Vector3 worldPosition, bool burning, float radius)
    {
        PrepareEffect(worldPosition);
        if (!burning)
        {
            BeginLiquidBurst(0f, radius);
            for (int i = 0; i < 32; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / 32;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                EmitLiquidDrop(direction * Random.Range(0f, 0.09f),
                    direction * Random.Range(0.8f, 1f) * radius / 0.54f,
                    0.54f, Random.Range(0.14f, 0.26f));
            }
            releaseAt = Time.time + EffectLifetime;
            activeEffect = true;
            ActiveEffectCount++;
            LastPlayed = this;
            return;
        }
        ParticleSystem.MainModule main = particles.main;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = burning;
        if (burning)
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.18f, 1.3f),
                    new Keyframe(1f, 0.35f)));
        ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
        colors.color = LiquidFade();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = GetBogFireMaterial();
        particles.Play(true);

        int count = burning ? 38 : 32;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams {
                position = direction * Random.Range(0f, 0.09f),
                velocity = direction * Random.Range(0.8f, 1f) * radius / 0.54f,
                startLifetime = 0.54f,
                startSize = burning ? Random.Range(0.24f, 0.4f) : Random.Range(0.14f, 0.26f),
                startColor = burning
                    ? new Color(1f, Random.Range(0.28f, 0.65f), 0.08f, 1f)
                    : Color.white,
                rotation = Random.Range(0f, 360f)
            };
            particles.Emit(particle, 1);
        }

        releaseAt = Time.time + EffectLifetime;
        activeEffect = true;
        ActiveEffectCount++;
        LastPlayed = this;
    }

    private static Gradient LiquidFade()
    {
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f) });
        return fade;
    }

    private static Gradient DustFade()
    {
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.82f, 0.86f, 0.9f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.55f, 0.38f),
                new GradientAlphaKey(0f, 1f) });
        return fade;
    }

    private void Update()
    {
        if (activeEffect && Time.time >= releaseAt) Release();
    }

    private void BeginLiquidBurst(float gravity, float travelRadius)
    {
        // Liquid uses fixed-size sprites, never ParticleSystem billboard size or
        // GetParticles/SetParticles state shared with dust and fire bursts.
        particleRenderer.enabled = false;
        liquidBurst = true;
        liquidStartedAt = Time.time;
        liquidGravity = gravity;
        liquidTravelRadius = travelRadius;
        if (bogDropSprite == null) bogDropSprite = Resources.Load<Sprite>("BogDrop");
    }

    private void EmitLiquidDrop(Vector2 origin, Vector2 velocity, float lifetime, float diameter)
    {
        if (bogDropSprite == null || liquidDropCount >= liquidDrops.Length) return;
        int index = liquidDropCount++;
        LiquidDrop drop = liquidDrops[index];
        if (drop == null)
        {
            var visual = new GameObject("Bog drop");
            visual.transform.SetParent(transform, false);
            drop = liquidDrops[index] = new LiquidDrop {
                Renderer = visual.AddComponent<SpriteRenderer>()
            };
            drop.Renderer.sharedMaterial = GetSharedMaterial();
            drop.Renderer.sortingOrder = RoomDarkness.WorldOverlayOrder + 20;
        }
        drop.Renderer.sprite = bogDropSprite;
        drop.Renderer.color = Color.white;
        drop.Renderer.enabled = true;
        // Convert texture/sprite dimensions to a bounded diameter in world units.
        float spriteDiameter = Mathf.Max(bogDropSprite.bounds.size.x, bogDropSprite.bounds.size.y);
        drop.Renderer.transform.localScale = Vector3.one
            * (Mathf.Clamp(diameter, 0.01f, MaximumLiquidParticleSize) / Mathf.Max(0.001f, spriteDiameter));
        drop.Origin = origin;
        drop.Velocity = velocity;
        drop.Lifetime = lifetime;
        UpdateLiquidDrop(drop, 0f);
    }

    private void UpdateLiquidDrop(LiquidDrop drop, float age)
    {
        drop.Renderer.enabled = age < drop.Lifetime;
        if (!drop.Renderer.enabled) return;
        Vector2 position = drop.Origin + drop.Velocity * age
            + Vector2.up * (0.5f * liquidGravity * age * age);
        position = Vector2.ClampMagnitude(position, liquidTravelRadius);
        drop.Renderer.transform.localPosition = new Vector3(position.x, position.y, -0.01f);
        Vector2 velocity = drop.Velocity + Vector2.up * (liquidGravity * age);
        if (velocity.sqrMagnitude > 0.0001f)
            drop.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 225f);
        float fade = Mathf.InverseLerp(drop.Lifetime, drop.Lifetime * 0.65f, age);
        drop.Renderer.color = new Color(1f, 1f, 1f, fade);
    }

    private void ClearLiquidDrops()
    {
        foreach (LiquidDrop drop in liquidDrops)
            if (drop != null && drop.Renderer != null) drop.Renderer.enabled = false;
        liquidDropCount = 0;
        liquidBurst = false;
    }

    private void LateUpdate()
    {
        if (!activeEffect || !liquidBurst) return;
        float age = Time.time - liquidStartedAt;
        for (int i = 0; i < liquidDropCount; i++) UpdateLiquidDrop(liquidDrops[i], age);
    }

    private void Release()
    {
        if (!activeEffect) return;
        activeEffect = false;
        ActiveEffectCount = Mathf.Max(0, ActiveEffectCount - 1);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ClearLiquidDrops();
        gameObject.SetActive(false);
        if (Pool.Count < MaximumPoolSize) Pool.Enqueue(this);
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (activeEffect) ActiveEffectCount = Mathf.Max(0, ActiveEffectCount - 1);
        if (LastPlayed == this) LastPlayed = null;
    }
}

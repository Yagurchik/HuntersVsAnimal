using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RabbitReplication : ProjectileDamageModifier, IProjectileHitResolved
{
    [SerializeField, Min(1)] private int enemyHitsToReplicate = 5;
    [SerializeField, Min(1)] private int maximumReplicationHits = 10;
    [SerializeField, Min(1)] private int maximumLifetimeHits = 20;
    [SerializeField, Min(1)] private int maxLivingRabbits = 32;
    [SerializeField, Range(0f, 0.5f), Tooltip("Rabbit body and collider growth after each resolved enemy hit.")]
    private float sizeGrowthPerHit = 0.05f;
    [SerializeField, Range(1f, 60f)] private float replicaAngle = 18f;
    [FormerlySerializedAs("deathExplosionRadius")]
    [SerializeField, Min(0.1f)] private float fluffCloudRadius = 1f;
    [SerializeField, Min(0f)] private float ultimateFluffDamage = 10f;

    private static readonly List<RabbitReplication> ActiveRabbits =
        new List<RabbitReplication>();
    private static int sharedUltimateHitCount;

    private GameObject projectilePrefab;
    private float fallbackSpeed;
    private int hitsSinceReplica;
    private int totalEnemyHits;
    private int replicasCreated;
    private bool countedAsLiving;
    private bool lifetimeEnded;
    private float packDamagePerRabbit;
    private bool ultimateEnabled;
    private Vector3 originalScale;
    private bool originalScaleCaptured;

    public int RequiredHitsForNextReplica => Mathf.Clamp(enemyHitsToReplicate, 1,
        Mathf.Max(1, maximumReplicationHits));
    public int TotalEnemyHits => totalEnemyHits;
    public int MaximumLifetimeHits => Mathf.Max(1, maximumLifetimeHits);
    public int MaximumReplicationHits => Mathf.Max(1, maximumReplicationHits);
    public float FluffCloudRadius => Mathf.Max(0.1f, fluffCloudRadius);
    public float PackDamagePerRabbit => Mathf.Max(0f, packDamagePerRabbit);
    public float SizeGrowthPerHit => Mathf.Clamp(sizeGrowthPerHit, 0f, 0.5f);
    public float CurrentPackDamageBonus => PackDamagePerRabbit * LivingRabbitCount;
    public bool HasUltimate => ultimateEnabled;
    public static int LivingRabbitCount
    {
        get
        {
            RemoveDestroyedRabbits();
            return ActiveRabbits.Count;
        }
    }
    public static int UltimateHitProgress => sharedUltimateHitCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLivingCount()
    {
        ActiveRabbits.Clear();
        sharedUltimateHitCount = 0;
    }

    private void OnEnable()
    {
        if (!originalScaleCaptured)
        {
            originalScale = transform.localScale;
            originalScaleCaptured = true;
        }
        if (!countedAsLiving)
        {
            ActiveRabbits.Add(this);
            countedAsLiving = true;
        }
    }

    private void OnDisable()
    {
        if (countedAsLiving)
        {
            ActiveRabbits.Remove(this);
            countedAsLiving = false;
            if (ActiveRabbits.Count == 0) sharedUltimateHitCount = 0;
        }
    }

    public void Configure(GameObject sourcePrefab, int requiredEnemyHits, float launchSpeed,
        float damagePerRabbit = 0f, int maximumHitsToReplicate = 10,
        bool hasUltimate = false)
    {
        projectilePrefab = sourcePrefab;
        maximumReplicationHits = Mathf.Max(1, maximumHitsToReplicate);
        enemyHitsToReplicate = Mathf.Clamp(requiredEnemyHits, 1, MaximumReplicationHits);
        fallbackSpeed = Mathf.Max(0.1f, launchSpeed);
        packDamagePerRabbit = Mathf.Max(0f, damagePerRabbit);
        ultimateEnabled = hasUltimate;
    }

    public override float ModifyDamage(Brick enemy, float baseDamage)
    {
        return Mathf.Max(0f, baseDamage) + CurrentPackDamageBonus;
    }

    public void OnProjectileHitResolved(Brick enemy, Vector2 impactPoint, Vector2 impactNormal)
    {
        if (lifetimeEnded) return;
        hitsSinceReplica++;
        totalEnemyHits++;
        // Scale the physics root so the visible rabbit and its collider grow together.
        transform.localScale = originalScale * Mathf.Pow(1f + SizeGrowthPerHit,
            hitsSinceReplica);

        bool triggerUltimate = false;
        if (ultimateEnabled)
        {
            sharedUltimateHitCount++;
            if (sharedUltimateHitCount >= 10)
            {
                sharedUltimateHitCount %= 10;
                triggerUltimate = true;
            }
        }

        if (hitsSinceReplica >= RequiredHitsForNextReplica
            && projectilePrefab != null && LivingRabbitCount < maxLivingRabbits)
        {
            hitsSinceReplica = 0;
            int nextRequirement = Mathf.Min(MaximumReplicationHits,
                RequiredHitsForNextReplica + 1);
            enemyHitsToReplicate = nextRequirement;
            SpawnReplica(impactPoint, nextRequirement);
            transform.localScale = originalScale;
        }

        if (triggerUltimate) TriggerUltimateFluffClouds();

        if (totalEnemyHits >= MaximumLifetimeHits)
        {
            lifetimeEnded = true;
            Destroy(gameObject);
        }
    }

    private void SpawnReplica(Vector2 impactPoint, int replicaHitRequirement)
    {
        Rigidbody2D sourceBody = GetComponent<Rigidbody2D>();
        Vector2 sourceVelocity = sourceBody != null ? sourceBody.velocity : Vector2.zero;
        float speed = sourceVelocity.magnitude > 0.1f ? sourceVelocity.magnitude : fallbackSpeed;
        Vector2 direction = sourceVelocity.sqrMagnitude > 0.01f
            ? sourceVelocity.normalized
            : Vector2.up;

        float angle = replicasCreated % 2 == 0 ? replicaAngle : -replicaAngle;
        replicasCreated++;
        Vector2 replicaDirection = Quaternion.Euler(0f, 0f, angle) * direction;

        Vector3 spawnPosition = impactPoint + replicaDirection * 0.25f;
        spawnPosition.z = transform.position.z;
        Animal sourceAnimal = GetComponent<Animal>();
        KeepReplicaInsideBattlefield(sourceAnimal, ref spawnPosition, ref replicaDirection);

        GameObject replica = Instantiate(projectilePrefab, spawnPosition, transform.rotation);
        Animal replicaAnimal = replica.GetComponent<Animal>();
        if (sourceAnimal != null && replicaAnimal != null)
        {
            replicaAnimal.SetRuntimeDamage(sourceAnimal.dmg);
            replicaAnimal.SetRuntimeHealth(sourceAnimal.Health);
            replicaAnimal.SetComboVolley(sourceAnimal.ComboVolleyId);
            replicaAnimal.SetCombatSource(sourceAnimal.CombatSource);
            if (sourceAnimal.ReturnPlayer != null)
                replicaAnimal.SetReturnTarget(sourceAnimal.ReturnPlayer.gameObject);
        }

        RabbitReplication replication = replica.GetComponent<RabbitReplication>();
        if (replication == null)
        {
            replication = replica.AddComponent<RabbitReplication>();
        }

        replication.Configure(projectilePrefab, replicaHitRequirement, fallbackSpeed,
            PackDamagePerRabbit, MaximumReplicationHits, ultimateEnabled);

        Rigidbody2D replicaBody = replica.GetComponent<Rigidbody2D>();
        if (replicaBody != null)
        {
            ProjectilePhysics2D projectilePhysics = replica.GetComponent<ProjectilePhysics2D>();
            if (projectilePhysics == null)
            {
                projectilePhysics = replica.AddComponent<ProjectilePhysics2D>();
            }
            projectilePhysics.Launch(replicaDirection, speed);
        }
    }

    private void KeepReplicaInsideBattlefield(Animal sourceAnimal, ref Vector3 spawnPosition,
        ref Vector2 replicaDirection)
    {
        Stage stage = sourceAnimal != null && sourceAnimal.ReturnPlayer != null
            ? sourceAnimal.ReturnPlayer.GetComponentInParent<Stage>() : null;
        if (stage == null) return;

        Bounds field = stage.GetViewBounds();
        float radius = Mathf.Max(0.02f, ProjectilePhysics2D.GetCollisionRadius(projectilePrefab));
        float minX = field.min.x + radius;
        float maxX = field.max.x - radius;
        float minY = field.min.y + radius;
        float maxY = field.max.y - radius;
        if (minX > maxX || minY > maxY) return;

        Vector2 desired = spawnPosition;
        bool crossedLeft = desired.x < minX;
        bool crossedRight = desired.x > maxX;
        bool crossedBottom = desired.y < minY;
        bool crossedTop = desired.y > maxY;
        spawnPosition.x = Mathf.Clamp(desired.x, minX, maxX);
        spawnPosition.y = Mathf.Clamp(desired.y, minY, maxY);

        if ((crossedLeft && replicaDirection.x < 0f)
            || (crossedRight && replicaDirection.x > 0f))
            replicaDirection.x = -replicaDirection.x;
        if ((crossedBottom && replicaDirection.y < 0f)
            || (crossedTop && replicaDirection.y > 0f))
            replicaDirection.y = -replicaDirection.y;
        if (replicaDirection.sqrMagnitude > 0.0001f) replicaDirection.Normalize();
    }

    private void TriggerUltimateFluffClouds()
    {
        RemoveDestroyedRabbits();
        RabbitReplication[] snapshot = ActiveRabbits.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            RabbitReplication rabbit = snapshot[i];
            if (rabbit == null || !rabbit.isActiveAndEnabled) continue;
            RabbitFluffExplosion.Create(rabbit.transform.position, rabbit.FluffCloudRadius,
                Mathf.Max(0f, rabbit.ultimateFluffDamage),
                rabbit.GetComponentInChildren<SpriteRenderer>(true));
        }
    }

    private static void RemoveDestroyedRabbits()
    {
        for (int i = ActiveRabbits.Count - 1; i >= 0; i--)
            if (ActiveRabbits[i] == null) ActiveRabbits.RemoveAt(i);
    }

    private void OnValidate()
    {
        maximumReplicationHits = Mathf.Max(1, maximumReplicationHits);
        enemyHitsToReplicate = Mathf.Clamp(enemyHitsToReplicate, 1, maximumReplicationHits);
        maximumLifetimeHits = Mathf.Max(1, maximumLifetimeHits);
        maxLivingRabbits = Mathf.Max(1, maxLivingRabbits);
        sizeGrowthPerHit = Mathf.Clamp(sizeGrowthPerHit, 0f, 0.5f);
        replicaAngle = Mathf.Clamp(replicaAngle, 1f, 60f);
        fluffCloudRadius = Mathf.Max(0.1f, fluffCloudRadius);
        ultimateFluffDamage = Mathf.Max(0f, ultimateFluffDamage);
    }
}

public sealed class RabbitFluffExplosion : MonoBehaviour
{
    private const int PuffCount = 18;
    private const int PuffSides = 6;
    private const float Duration = 0.48f;
    private static readonly Collider2D[] HitBuffer = new Collider2D[96];
    private readonly HashSet<Brick> damagedEnemies = new HashSet<Brick>();
    private readonly Vector2[] directions = new Vector2[PuffCount];
    private readonly float[] sizes = new float[PuffCount];
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Vector3[] vertices;
    private Color32[] colors;
    private int[] triangles;
    private float radius;
    private float elapsed;
    private bool ownsMaterial;

    public static RabbitFluffExplosion Create(Vector3 position, float radius,
        float damage, SpriteRenderer source)
    {
        GameObject effectObject = new GameObject("Rabbit Fluff Explosion");
        effectObject.transform.position = position;
        RabbitFluffExplosion effect = effectObject.AddComponent<RabbitFluffExplosion>();
        effect.Initialize(Mathf.Max(0.1f, radius), Mathf.Max(0f, damage), source);
        return effect;
    }

    private void Initialize(float targetRadius, float damage, SpriteRenderer source)
    {
        radius = targetRadius;
        BuildMesh(source);
        for (int i = 0; i < PuffCount; i++)
        {
            float angle = i * 137.508f * Mathf.Deg2Rad;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            sizes[i] = 0.7f + (i % 5) * 0.075f;
        }
        DamageEnemies(damage);
        UpdateMesh(0f);
    }

    private void DamageEnemies(float damage)
    {
        if (damage <= 0f) return;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius, HitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = HitBuffer[i];
            Brick enemy = hit != null ? hit.GetComponentInParent<Brick>() : null;
            if (enemy != null && !enemy.IsDefeated && damagedEnemies.Add(enemy))
                enemy.TakeDamage(damage, CombatDamageSource.Rabbit);
            HitBuffer[i] = null;
        }
    }

    private void BuildMesh(SpriteRenderer source)
    {
        int verticesPerPuff = PuffSides + 1;
        vertices = new Vector3[PuffCount * verticesPerPuff];
        colors = new Color32[vertices.Length];
        triangles = new int[PuffCount * PuffSides * 3];
        for (int puff = 0; puff < PuffCount; puff++)
        {
            int vertexStart = puff * verticesPerPuff;
            int triangleStart = puff * PuffSides * 3;
            for (int side = 0; side < PuffSides; side++)
            {
                int next = (side + 1) % PuffSides;
                int index = triangleStart + side * 3;
                triangles[index] = vertexStart;
                triangles[index + 1] = vertexStart + 1 + side;
                triangles[index + 2] = vertexStart + 1 + next;
            }
        }

        mesh = new Mesh { name = "Rabbit fluff burst" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2.4f));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        if (source != null)
        {
            meshRenderer.sharedMaterial = source.sharedMaterial;
            meshRenderer.sortingLayerID = source.sortingLayerID;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                meshRenderer.sharedMaterial = new Material(shader);
                ownsMaterial = true;
            }
        }
        meshRenderer.sortingOrder = Mathf.Max(source != null ? source.sortingOrder : 0,
            RoomDarkness.WorldOverlayOrder + 18);
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", Texture2D.whiteTexture);
        properties.SetColor("_Color", Color.white);
        properties.SetColor("_RendererColor", Color.white);
        meshRenderer.SetPropertyBlock(properties);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Duration);
        UpdateMesh(progress);
        if (progress >= 1f) Destroy(gameObject);
    }

    private void UpdateMesh(float progress)
    {
        float expansion = 1f - Mathf.Pow(1f - progress, 3f);
        float opacity = 1f - Mathf.SmoothStep(0.55f, 1f, progress);
        int verticesPerPuff = PuffSides + 1;
        for (int puff = 0; puff < PuffCount; puff++)
        {
            float layer = 0.52f + (puff % 4) * 0.13f;
            Vector2 center = directions[puff] * radius * expansion * layer;
            float puffRadius = radius * 0.11f * sizes[puff]
                * Mathf.Lerp(0.45f, 1.15f, Mathf.Sin(progress * Mathf.PI));
            int start = puff * verticesPerPuff;
            Color32 color = puff % 3 == 0
                ? new Color32(255, 244, 218, (byte)(255 * opacity))
                : new Color32(244, 232, 211, (byte)(240 * opacity));
            vertices[start] = center;
            colors[start] = color;
            for (int side = 0; side < PuffSides; side++)
            {
                float angle = side * Mathf.PI * 2f / PuffSides + puff * 0.31f;
                vertices[start + 1 + side] = center +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * puffRadius;
                colors[start + 1 + side] = color;
            }
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2.4f));
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (ownsMaterial && meshRenderer != null && meshRenderer.sharedMaterial != null)
            Destroy(meshRenderer.sharedMaterial);
    }
}

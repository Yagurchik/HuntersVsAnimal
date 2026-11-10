using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pooled burst of warm, two-tone quills. A single small mesh renders all
/// needles; the expanding damage radius stays independent of visual geometry.
/// </summary>
public class HedgehogExplosionPulse : MonoBehaviour
{
    private const int HitBufferSize = 64;
    private const int QuillCount = 16;
    private const int VerticesPerQuill = 9;
    private static readonly Collider2D[] HitBuffer = new Collider2D[HitBufferSize];
    private static readonly Stack<HedgehogExplosionPulse> Pool = new Stack<HedgehogExplosionPulse>();

    private readonly HashSet<Brick> damagedEnemies = new HashSet<Brick>();
    private static int burstSequence;
    private static readonly Color32 Outline = new Color32(40, 26, 19, 255);
    private static readonly Color32 QuillLight = new Color32(241, 210, 157, 255);
    private static readonly Color32 QuillShade = new Color32(166, 111, 56, 255);

    private MeshRenderer pulseRenderer;
    private Mesh quillMesh;
    private readonly Vector3[] vertices = new Vector3[QuillCount * VerticesPerQuill];
    private readonly Color32[] colors = new Color32[QuillCount * VerticesPerQuill];
    private readonly Vector2[] directions = new Vector2[QuillCount];
    private float damage;
    private float radius;
    private float duration;
    private float elapsed;
    private bool returnedToPool;
    private int comboVolleyId;
    private bool comboEffectRegistered;
    private float damageGrowthPerHit;
    private int damageTurnNumber;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool()
    {
        Pool.Clear();
        burstSequence = 0;
    }

    public static HedgehogExplosionPulse Create(
        SpriteRenderer source,
        Vector3 position,
        float damage,
        float radius,
        float duration,
        int comboVolleyId = 0,
        float quillDamageGrowthPerHit = 0f,
        int turnNumber = 0)
    {
        HedgehogExplosionPulse pulse = null;
        while (Pool.Count > 0 && pulse == null)
        {
            pulse = Pool.Pop();
        }

        if (pulse == null)
        {
            GameObject pulseObject = new GameObject("Hedgehog Quill Burst");
            pulse = pulseObject.AddComponent<HedgehogExplosionPulse>();
        }

        pulse.transform.position = position;
        pulse.transform.rotation = Quaternion.identity;
        pulse.transform.localScale = Vector3.one;
        pulse.gameObject.SetActive(true);
        pulse.Initialize(source, damage, radius, duration,
            quillDamageGrowthPerHit, turnNumber);
        pulse.comboVolleyId = comboVolleyId;
        pulse.comboEffectRegistered = ProjectileComboUI.Instance != null
            && ProjectileComboUI.Instance.BeginEffect(comboVolleyId);
        return pulse;
    }

    private void Initialize(SpriteRenderer source, float areaDamage, float targetRadius,
        float lifetime, float quillDamageGrowthPerHit, int turnNumber)
    {
        damage = Mathf.Max(0f, areaDamage);
        radius = Mathf.Max(0.1f, targetRadius);
        duration = Mathf.Max(0.05f, lifetime);
        elapsed = 0f;
        returnedToPool = false;
        damageGrowthPerHit = Mathf.Max(0f, quillDamageGrowthPerHit);
        damageTurnNumber = turnNumber;
        damagedEnemies.Clear();

        EnsureMesh(source);
        pulseRenderer.enabled = true;
        if (source != null)
        {
            pulseRenderer.sharedMaterial = source.sharedMaterial;
        }

        pulseRenderer.sortingLayerID = source != null ? source.sortingLayerID : 0;
        pulseRenderer.sortingOrder = Mathf.Max(
            source != null ? source.sortingOrder : 0,
            RoomDarkness.WorldOverlayOrder + 10);

        // Do not consume UnityEngine.Random: VFX must not affect wave generation.
        float phase = (burstSequence++ % 360) * 137.508f * Mathf.Deg2Rad;
        for (int i = 0; i < QuillCount; i++)
        {
            float angle = phase + i * Mathf.PI * 2f / QuillCount;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        UpdateQuills(radius * 0.04f, 1f);
    }

    private void EnsureMesh(SpriteRenderer source)
    {
        if (quillMesh != null)
        {
            return;
        }

        quillMesh = new Mesh { name = "Hedgehog quills (pooled)" };
        quillMesh.MarkDynamic();
        int[] triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = i;
        }

        quillMesh.vertices = vertices;
        quillMesh.colors32 = colors;
        quillMesh.uv = new Vector2[vertices.Length];
        quillMesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = quillMesh;
        pulseRenderer = gameObject.AddComponent<MeshRenderer>();
        pulseRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pulseRenderer.receiveShadows = false;
        if (source != null)
        {
            pulseRenderer.sharedMaterial = source.sharedMaterial;
        }
        // Reuse the projectile's sprite material, but do not sample its texture.
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", Texture2D.whiteTexture);
        properties.SetColor("_Color", Color.white);
        properties.SetColor("_RendererColor", Color.white);
        pulseRenderer.SetPropertyBlock(properties);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float expansion = 1f - Mathf.Pow(1f - progress, 3f);
        float currentRadius = radius * expansion;

        UpdateQuills(currentRadius, 1f - Mathf.SmoothStep(0f, 1f, progress));
        DamageNewEnemies(currentRadius);

        if (progress >= 1f)
        {
            ReturnToPool();
        }
    }

    private void UpdateQuills(float currentRadius, float opacity)
    {
        byte alpha = (byte)(255 * Mathf.Clamp01(opacity));
        Color32 dark = Outline, light = QuillLight, shade = QuillShade;
        dark.a = light.a = shade.a = alpha;
        for (int i = 0; i < QuillCount; i++)
        {
            Vector2 direction = directions[i];
            Vector2 sideways = new Vector2(-direction.y, direction.x);
            float length = Mathf.Min(currentRadius * 0.8f,
                Mathf.Min(0.32f, radius * 0.28f) * (0.85f + (i % 3) * 0.075f));
            float halfWidth = length * 0.14f;
            Vector2 tip = direction * currentRadius;
            Vector2 tail = tip - direction * length;
            int start = i * VerticesPerQuill;
            SetTriangle(start, tip, tail + sideways * halfWidth, tail - sideways * halfWidth, dark);
            Vector2 innerTip = tip - direction * length * 0.1f;
            Vector2 innerTail = tail + direction * length * 0.1f;
            SetTriangle(start + 3, innerTip, innerTail + sideways * halfWidth * 0.66f, innerTail, light);
            SetTriangle(start + 6, innerTip, innerTail, innerTail - sideways * halfWidth * 0.66f, shade);
        }
        quillMesh.vertices = vertices;
        quillMesh.colors32 = colors;
        quillMesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2.1f, radius * 2.1f, 0.1f));
    }

    private void SetTriangle(int index, Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        vertices[index] = a;
        vertices[index + 1] = b;
        vertices[index + 2] = c;
        colors[index] = colors[index + 1] = colors[index + 2] = color;
    }

    private void DamageNewEnemies(float currentRadius)
    {
        if (damage <= 0f || currentRadius <= 0f)
        {
            return;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, currentRadius, HitBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = HitBuffer[i];
            Brick enemy = hit != null ? hit.GetComponentInParent<Brick>() : null;
            if (enemy != null && !enemy.IsDefeated && damagedEnemies.Add(enemy))
            {
                float dealtDamage = damage;
                if (damageGrowthPerHit > 0f)
                {
                    HedgehogQuillStacks stacks = enemy.GetComponent<HedgehogQuillStacks>();
                    if (stacks == null)
                    {
                        stacks = enemy.gameObject.AddComponent<HedgehogQuillStacks>();
                    }

                    dealtDamage += stacks.RegisterExplosion(
                        damageTurnNumber, damageGrowthPerHit);
                }
                enemy.TakeProjectileDamage(dealtDamage, comboVolleyId,
                    CombatDamageSource.Hedgehog, -1, false);
            }
            HitBuffer[i] = null;
        }
    }

    private void ReturnToPool()
    {
        if (returnedToPool)
        {
            return;
        }

        returnedToPool = true;
        if (pulseRenderer != null)
        {
            pulseRenderer.enabled = false;
        }

        gameObject.SetActive(false);
        Pool.Push(this);
    }

    private void OnDestroy()
    {
        if (quillMesh != null)
        {
            Destroy(quillMesh);
        }
    }

    private void OnDisable()
    {
        if (comboEffectRegistered)
        {
            ProjectileComboUI.Instance?.EndEffect(comboVolleyId);
        }

        comboEffectRegistered = false;
    }
}

[DisallowMultipleComponent]
public sealed class HedgehogQuillStacks : MonoBehaviour
{
    private int registeredTurn = int.MinValue;
    private int explosionHitsThisTurn;

    public int ExplosionHitsThisTurn => Mathf.Max(0, explosionHitsThisTurn);
    public int RegisteredTurn => registeredTurn;

    public float RegisterExplosion(int turnNumber, float damageGrowthPerHit)
    {
        if (registeredTurn != turnNumber)
        {
            registeredTurn = turnNumber;
            explosionHitsThisTurn = 0;
        }

        float bonus = ExplosionHitsThisTurn * Mathf.Max(0f, damageGrowthPerHit);
        explosionHitsThisTurn++;
        return bonus;
    }
}

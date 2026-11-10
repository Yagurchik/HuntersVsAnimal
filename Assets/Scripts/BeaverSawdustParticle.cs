using System.Collections.Generic;
using UnityEngine;

// Pool instances of the authored sprite prefab; each launch resets its complete visual state.
[DisallowMultipleComponent]
public sealed class BeaverSawdustParticle : MonoBehaviour
{
    private const int MaximumCachedPerPrefab = 128;
    private static readonly Dictionary<int, Stack<BeaverSawdustParticle>> Pools = new Dictionary<int, Stack<BeaverSawdustParticle>>();
    private static Transform poolRoot;
    [SerializeField, Min(0.05f)] private float lifetime = 0.55f;
    private Vector2 velocity;
    private float age;
    private SpriteRenderer artwork;
    private Color initialColor;
    private Vector3 initialScale;
    private int sourceKey;
    private bool returned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool() { Pools.Clear(); poolRoot = null; }
    private static Transform Root
    {
        get { if (poolRoot == null) poolRoot = new GameObject("Beaver Sawdust Pool").transform; return poolRoot; }
    }
    private void Awake()
    {
        artwork = GetComponent<SpriteRenderer>();
        initialColor = artwork != null ? artwork.color : Color.white;
        initialScale = transform.localScale;
    }
    public static BeaverSawdustParticle Spawn(BeaverSawdustParticle source, Vector2 origin, Vector2 direction, float speed)
    {
        if (source == null) return null;
        int key = source.GetInstanceID();
        if (!Pools.TryGetValue(key, out var pool)) { pool = new Stack<BeaverSawdustParticle>(32); Pools.Add(key, pool); }
        BeaverSawdustParticle particle = null;
        while (pool.Count > 0 && particle == null) particle = pool.Pop();
        if (particle == null) particle = Instantiate(source, Root);
        particle.sourceKey = key;
        particle.returned = false;
        particle.Launch(origin, direction, speed);
        particle.gameObject.SetActive(true);
        return particle;
    }
    public void Launch(Vector2 origin, Vector2 direction, float speed)
    {
        age = 0f;
        transform.position = origin;
        velocity = direction.normalized * speed;
        transform.localScale = initialScale * Random.Range(0.75f, 1.2f);
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        if (artwork != null) artwork.color = initialColor;
    }
    private void Update()
    {
        if (returned) return;
        age += Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= Mathf.Max(0f, 1f - 4f * Time.deltaTime);
        if (artwork != null)
        {
            Color color = initialColor;
            color.a *= 1f - Mathf.Clamp01(age / lifetime);
            artwork.color = color;
        }
        if (age < lifetime) return;
        returned = true;
        if (sourceKey == 0 || !Pools.TryGetValue(sourceKey, out var pool) || pool.Count >= MaximumCachedPerPrefab)
        { Destroy(gameObject); return; }
        gameObject.SetActive(false);
        pool.Push(this);
    }
}

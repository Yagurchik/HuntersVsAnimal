using UnityEngine;

// Visual only. The health transfer is committed when the boss lands, not at the
// end of this animation, so combat and occupancy never depend on particle timing.
[DisallowMultipleComponent]
public sealed class SoulAbsorptionEffect : MonoBehaviour
{
    private const string ResourcePath = "Effects/SoulAbsorption";

    [SerializeField] private ParticleSystem wisps;
    [SerializeField] private ParticleSystem arrivalBurst;
    [SerializeField, Min(0.1f)] private float travelSeconds = 0.72f;
    [SerializeField, Min(0f)] private float arcHeight = 0.65f;
    [SerializeField, Min(0f)] private float swayWidth = 0.24f;
    [SerializeField, Min(0f)] private float lingerSeconds = 0.55f;

    private Transform target;
    private Vector3 sourcePosition;
    private Vector3 lastTargetPosition;
    private float startedAt;
    private float releaseAt;
    private bool arrived;
    private bool running;

    public static int ActiveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => ActiveCount = 0;

    public static void Play(Vector3 source, Transform boss)
    {
        if (boss == null) return;
        SoulAbsorptionEffect prefab = Resources.Load<SoulAbsorptionEffect>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("Soul Absorption prefab is missing from Resources/Effects.");
            return;
        }
        SoulAbsorptionEffect effect = Instantiate(prefab, source, Quaternion.identity);
        effect.Begin(source, boss);
    }

    private void Begin(Vector3 source, Transform boss)
    {
        sourcePosition = source;
        target = boss;
        lastTargetPosition = boss.position;
        startedAt = Time.time;
        arrived = false;
        running = true;
        if (wisps != null) wisps.Play(true);
        ActiveCount++;
    }

    private void Update()
    {
        if (target != null) lastTargetPosition = target.position;
        if (!arrived)
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.1f, travelSeconds));
            float arch = Mathf.Sin(progress * Mathf.PI);
            float sway = Mathf.Sin(progress * Mathf.PI * 3f) * arch * swayWidth;
            transform.position = Vector3.Lerp(sourcePosition, lastTargetPosition, progress)
                + new Vector3(sway, arcHeight * arch, 0f);
            if (progress < 1f) return;

            arrived = true;
            if (wisps != null) wisps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (arrivalBurst != null) arrivalBurst.Play(true);
            releaseAt = Time.time + lingerSeconds;
        }
        else if (Time.time >= releaseAt)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (running) ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }
}

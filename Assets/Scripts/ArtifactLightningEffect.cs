using System.Collections.Generic;
using UnityEngine;

// The line, material and sorting order remain on the editable native prefab.
public sealed class ArtifactLightningEffect : MonoBehaviour
{
    private const int MaximumCached = 64;
    private static readonly Stack<ArtifactLightningEffect> Pool = new Stack<ArtifactLightningEffect>(32);
    private static Transform poolRoot;
    [SerializeField] private LineRenderer beam;
    [SerializeField, Min(0.05f)] private float duration = 0.18f;
    private float authoredDuration;
    private float elapsed;
    private bool returned;
    private Color tint;
    private static ArtifactLightningEffect prefab;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool() { Pool.Clear(); poolRoot = null; prefab = null; }
    private void Awake() => authoredDuration = duration;
    public static void Flash(Vector3 from, Vector3 to, bool arc = false)
    {
        if (prefab == null) prefab = Resources.Load<ArtifactLightningEffect>("Effects/Artifacts/Lightning");
        if (prefab == null) return;
        ArtifactLightningEffect effect = null;
        while (Pool.Count > 0 && effect == null) effect = Pool.Pop();
        if (effect == null)
        {
            if (poolRoot == null) poolRoot = new GameObject("Artifact Lightning Pool").transform;
            effect = Instantiate(prefab, poolRoot);
        }
        effect.returned = false; effect.elapsed = 0f;
        effect.tint = arc ? new Color(1f, 0.86f, 0.28f, 1f) : new Color(0.45f, 0.87f, 1f, 1f);
        effect.duration = arc ? 0.1f : effect.authoredDuration;
        effect.beam.startColor = effect.beam.endColor = effect.tint;
        Vector3 perpendicular = new Vector3(-(to - from).y, (to - from).x, 0).normalized;
        for (int i = 0; i < effect.beam.positionCount; i++)
        {
            float t = i / (float)(effect.beam.positionCount - 1);
            float offset = i == 0 || i == effect.beam.positionCount - 1 ? 0 : (i % 2 == 0 ? 0.1f : -0.1f);
            effect.beam.SetPosition(i, Vector3.Lerp(from, to, t) + (arc ? Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.35f : perpendicular * offset));
        }
        effect.gameObject.SetActive(true);
    }
    private void Update()
    {
        if (returned) return;
        elapsed += Time.deltaTime;
        Color color = tint; color.a = Mathf.Clamp01(1f - elapsed / duration);
        beam.startColor = beam.endColor = color;
        if (elapsed < duration) return;
        returned = true;
        if (Pool.Count >= MaximumCached) { Destroy(gameObject); return; }
        gameObject.SetActive(false); Pool.Push(this);
    }
}

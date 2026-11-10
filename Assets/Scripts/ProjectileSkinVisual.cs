using UnityEngine;

// Authored on each projectile prefab, so replicas inherit the selected cosmetic too.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class ProjectileSkinVisual : MonoBehaviour
{
    [SerializeField, Range(0, 7)] private int projectileIndex;
    public int ProjectileIndex => projectileIndex;

    private void Awake() => Apply();
    private void Start() => Apply();

    public void Apply()
    {
        Sprite artwork = ProjectileSkins.EquippedArtwork(projectileIndex);
        if (artwork == null) return;
        SpriteRenderer source = GetComponent<SpriteRenderer>();
        ProjectileFlightVisual flight = GetComponent<ProjectileFlightVisual>();
        SpriteRenderer target = flight != null && flight.Renderer != null ? flight.Renderer : source;
        if (target == null) return;
        // Import matches the original sprite's world canvas and pivot, preserving
        // the physics body, collider, rotation center and authored visual scale.
        target.sprite = artwork;
        target.color = Color.white;
        if (source != null && source != target)
        {
            source.sprite = artwork;
            source.color = Color.white;
        }
    }
}

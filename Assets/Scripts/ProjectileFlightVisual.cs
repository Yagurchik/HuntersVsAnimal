using UnityEngine;

/// <summary>Spins artwork without modifying the interpolated physics body or its collider.</summary>
[DisallowMultipleComponent]
public sealed class ProjectileFlightVisual : MonoBehaviour
{
    [SerializeField, Min(0f)] private float spinDegreesPerSecond = 540f;

    private ProjectilePhysics2D flight;
    private SpriteRenderer visual;

    public SpriteRenderer Renderer => visual;
    public float SpinDegreesPerSecond => spinDegreesPerSecond;

    public static ProjectileFlightVisual Ensure(GameObject projectile, float spinSpeed)
    {
        ProjectileFlightVisual component = projectile.GetComponent<ProjectileFlightVisual>();
        if (component == null)
        {
            component = projectile.AddComponent<ProjectileFlightVisual>();
            component.spinDegreesPerSecond = Mathf.Max(0f, spinSpeed);
        }
        // Existing prefab-authored settings win over a legacy effect's fallback.
        component.CreateVisual();
        return component;
    }

    public void ConfigureSpin(float spinSpeed)
    {
        spinDegreesPerSecond = Mathf.Max(0f, spinSpeed);
        CreateVisual();
        if (spinDegreesPerSecond <= 0f && visual != null)
        {
            visual.transform.localRotation = Quaternion.identity;
        }
    }

    public void SetFacingDirection(Vector2 direction)
    {
        CreateVisual();
        if (visual == null || direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        // Directional projectile art is authored head-up in a strict top view.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        visual.flipX = false;
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Awake()
    {
        flight = GetComponent<ProjectilePhysics2D>();
        CreateVisual();
    }

    private void CreateVisual()
    {
        if (visual != null)
        {
            return;
        }

        SpriteRenderer source = GetComponent<SpriteRenderer>();
        if (source == null)
        {
            return;
        }

        GameObject child = new GameObject("Projectile Rotating Visual");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        visual = child.AddComponent<SpriteRenderer>();
        visual.sprite = source.sprite;
        visual.sharedMaterial = source.sharedMaterial;
        visual.color = source.color;
        visual.flipX = source.flipX;
        visual.flipY = source.flipY;
        visual.drawMode = SpriteDrawMode.Simple;
        if (source.sprite != null && source.drawMode != SpriteDrawMode.Simple)
        {
            Vector2 naturalSize = source.sprite.bounds.size;
            child.transform.localScale = new Vector3(
                source.size.x / Mathf.Max(0.001f, naturalSize.x),
                source.size.y / Mathf.Max(0.001f, naturalSize.y), 1f);
        }
        visual.sortingLayerID = source.sortingLayerID;
        visual.sortingOrder = source.sortingOrder;
        visual.maskInteraction = source.maskInteraction;
        visual.spriteSortPoint = source.spriteSortPoint;
        // Keep the authored root renderer for prefab icons; only its runtime copy renders.
        source.enabled = false;
    }

    private void Update()
    {
        if (visual == null || spinDegreesPerSecond <= 0f)
        {
            return;
        }

        if (flight == null)
        {
            flight = GetComponent<ProjectilePhysics2D>();
        }

        if (flight == null || !flight.IsFlying)
        {
            return;
        }

        visual.transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}

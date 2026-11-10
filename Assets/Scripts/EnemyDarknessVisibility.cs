using UnityEngine;

// Visual concealment only: never deactivate the enemy, its collider or its animator.
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(900)]
public sealed class EnemyDarknessVisibility : MonoBehaviour
{
    private Brick enemy;
    private RoomDarkness parentDarkness;
    private Renderer[] visuals;
    private CanvasRenderer[] healthGraphics;
    private float[] previousAlpha;

    public bool IsHidden { get; private set; }

    private void OnEnable()
    {
        CacheVisuals();
        parentDarkness = GetComponentInParent<RoomDarkness>(true);
        RefreshVisibility();
    }

    private void OnTransformParentChanged()
    {
        parentDarkness = GetComponentInParent<RoomDarkness>(true);
        if (isActiveAndEnabled)
        {
            RefreshVisibility();
        }
    }

    private void OnTransformChildrenChanged()
    {
        RestoreVisuals();
        CacheVisuals();
        if (isActiveAndEnabled)
        {
            RefreshVisibility();
        }
    }

    private void CacheVisuals()
    {
        enemy = GetComponent<Brick>();
        visuals = GetComponentsInChildren<Renderer>(true);
        healthGraphics = GetComponentsInChildren<CanvasRenderer>(true);
        previousAlpha = new float[healthGraphics.Length];
    }

    private void LateUpdate() => RefreshVisibility();

    public void RefreshVisibility()
    {
        if (visuals == null)
        {
            CacheVisuals();
        }

        RoomDarkness darkness = enemy != null && enemy.levelGenerator != null
            ? enemy.levelGenerator.Darkness : parentDarkness;
        bool hide = isActiveAndEnabled && darkness != null && !darkness.IsVisible(transform.position);
        if (!hide)
        {
            RestoreVisuals();
            return;
        }

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] == null)
            {
                continue;
            }

            visuals[i].forceRenderingOff = true;
        }
        for (int i = 0; i < healthGraphics.Length; i++)
        {
            if (healthGraphics[i] == null)
            {
                continue;
            }

            if (!IsHidden)
            {
                previousAlpha[i] = healthGraphics[i].GetAlpha();
            }
            // CanvasRenderer alpha is transient: editor previews do not save disabled HP UI.
            healthGraphics[i].SetAlpha(0f);
        }
        IsHidden = true;
    }

    private void OnDisable() => RestoreVisuals();

    private void RestoreVisuals()
    {
        if (visuals == null)
        {
            return;
        }
        // This is the only component that uses forceRenderingOff on enemies.
        // Always clear it when visible: a cached flag can survive a room transfer
        // or a visual child being rebuilt while the enemy is concealed.
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null)
            {
                visuals[i].forceRenderingOff = false;
            }
        }

        if (!IsHidden)
        {
            return;
        }

        for (int i = 0; i < healthGraphics.Length; i++)
        {
            if (healthGraphics[i] != null)
            {
                healthGraphics[i].SetAlpha(previousAlpha[i]);
            }
        }

        IsHidden = false;
    }
}

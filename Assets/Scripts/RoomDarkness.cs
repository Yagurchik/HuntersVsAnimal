using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Dim the world with one translucent mesh; enemies hide their own visuals separately.
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(850)]
public sealed class RoomDarkness : MonoBehaviour
{
    [Header("Room lighting")]
    [SerializeField, Tooltip("Use 100%, 80%, 50%, 30% for defense lines 1–4.")]
    private bool useLineLighting = true;
    [SerializeField, Range(0f, 1f), Tooltip("Fraction where enemies are visible, measured from the player's side. Used when Use Line Lighting is off.")]
    private float litFraction = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Soft edge width in cells, inside the dark area.")]
    private float edgeSoftness = 0.5f;
    [SerializeField] private Color darknessColor = new Color(0.025f, 0.035f, 0.065f, 1f);
    [SerializeField, Range(0f, 0.98f), Tooltip("Dim scenery and projectiles without hiding them. Does not affect enemy concealment.")]
    private float environmentOpacity = 0.55f;
    [SerializeField, Range(0.05f, 0.5f), Tooltip("Soft edge outside an illuminated enemy's one-cell light radius.")]
    private float localLightSoftness = 0.2f;

    public const int WorldOverlayOrder = 90;

    private Stage room;
    private RoomDarknessGraphic graphic;
    private bool needsRefresh = true;
    private Vector2Int previousSize;
    private float previousLighting = -1f;
    private readonly List<EnemyIllumination> lightSources = new List<EnemyIllumination>();
    private readonly List<RoomDarknessGraphic.LightDisc> renderLights = new List<RoomDarknessGraphic.LightDisc>();
    private readonly List<Vector3> shadowPositions = new List<Vector3>();
    private readonly List<RoomDarknessGraphic.ShadowDisc> renderShadows = new List<RoomDarknessGraphic.ShadowDisc>();
    private int previousShadowCount;

    // Runtime lighting chosen by a game mode (clock siege eras and storms).
    // A negative value means the authored setting is used.
    private float litOverride = -1f;
    public bool HasLitOverride => litOverride >= 0f;
    public void SetLitOverride(float fraction)
    {
        litOverride = Mathf.Clamp01(fraction);
        needsRefresh = true;
    }
    public void ClearLitOverride()
    {
        litOverride = -1f;
        needsRefresh = true;
    }

    public float LitFraction => litOverride >= 0f ? litOverride
        : useLineLighting ? LightingForLine(Room != null ? Room.DefenseLine : 1) : Mathf.Clamp01(litFraction);
    public float LightBoundaryY => transform.position.y + (LitFraction - 0.5f) * BoardSize.y;
    public bool HasDarkness => isActiveAndEnabled && LitFraction < 1f;
    public RoomDarknessGraphic Overlay => graphic;
    public float EnvironmentOpacity => Mathf.Clamp(environmentOpacity, 0f, 0.98f);
    public int ActiveLightCount => lightSources.Count;
    private Stage Room => room != null ? room : room = GetComponent<Stage>();
    private Vector2Int BoardSize => Room != null ? Room.BattlefieldSize : Vector2Int.one;

    public static float LightingForLine(int line)
    {
        switch (line)
        {
            case 2: return 0.8f;
            case 3: return 0.5f;
            case 4: return 0.3f;
            default: return 1f;
        }
    }

    public bool IsVisible(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled)
        {
            return true;
        }

        if (!HasDarkness)
        {
            return true;
        }

        for (int i = lightSources.Count - 1; i >= 0; i--)
        {
            EnemyIllumination source = lightSources[i];
            if (source == null) { lightSources.RemoveAt(i); continue; }
            if (source.Illuminates(this, worldPosition))
            {
                return true;
            }
        }
        float boundary = transform.position.y
            + (LitFraction - 0.5f) * BoardSize.y;
        return worldPosition.y <= boundary + 0.0001f;
    }

    // The preview must not reveal collision positions or reflections behind the fog.
    public Vector2 ClipToVisibleArea(Vector2 start, Vector2 end)
    {
        if (!HasDarkness || IsVisible(end))
        {
            return end;
        }

        if (start.y >= LightBoundaryY)
        {
            return start;
        }

        return Vector2.Lerp(start, end, (LightBoundaryY - start.y) / (end.y - start.y));
    }

    public void Refresh()
    {
        needsRefresh = false;
        if (Room == null || !gameObject.scene.IsValid())
        {
            return;
        }

        previousSize = BoardSize;
        previousLighting = LitFraction;
        bool hasShadows = EnemySpecialistTrait.HasActiveShadow(this);
        if (!HasDarkness && !hasShadows)
        {
            previousShadowCount = 0;
            if (graphic != null)
            {
                graphic.gameObject.SetActive(false);
            }

            return;
        }
        if (graphic == null)
        {
            Transform existing = transform.Find("Room Darkness (generated)");
            if (existing != null)
            {
                graphic = existing.GetComponent<RoomDarknessGraphic>();
            }
        }
        if (graphic == null)
        {
            GameObject overlay = new GameObject("Room Darkness (generated)", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasRenderer), typeof(RoomDarknessGraphic));
            // The settings live on the room; this derived mesh is rebuilt after scene loads.
            overlay.hideFlags = HideFlags.HideAndDontSave;
            overlay.transform.SetParent(transform, false);
            graphic = overlay.GetComponent<RoomDarknessGraphic>();
            graphic.raycastTarget = false;
        }
        Canvas canvas = graphic.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerID = 0;
        // Below the HUD (100+) and modal panels (300), including camera-space UI.
        canvas.sortingOrder = WorldOverlayOrder;
        RectTransform rect = graphic.rectTransform;
        // Legacy room roots share the camera's Z; the playable objects have a different depth.
        LevelGenerator generator = Room.GetComponentInChildren<LevelGenerator>(true);
        float boardDepth = generator != null ? generator.transform.position.z : transform.position.z;
        rect.position = new Vector3(transform.position.x, transform.position.y, boardDepth - 0.1f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = BoardSize;
        graphic.gameObject.SetActive(true);
        Color tint = darknessColor;
        tint.a = EnvironmentOpacity;
        CollectRenderLights();
        CollectRenderShadows();
        previousShadowCount = renderShadows.Count;
        graphic.Configure(LitFraction, edgeSoftness, tint, renderLights, renderShadows);
    }

    public void RegisterLight(EnemyIllumination source)
    {
        if (source == null || lightSources.Contains(source))
        {
            return;
        }

        lightSources.Add(source);
        needsRefresh = true;
    }

    public void UnregisterLight(EnemyIllumination source)
    {
        if (source != null && lightSources.Remove(source))
        {
            needsRefresh = true;
        }
    }

    public void NotifyLightChanged(EnemyIllumination source)
    {
        if (source == null)
        {
            return;
        }

        if (!lightSources.Contains(source))
        {
            lightSources.Add(source);
        }

        needsRefresh = true;
    }

    private void CollectRenderLights()
    {
        renderLights.Clear();
        if (graphic == null)
        {
            return;
        }

        for (int i = lightSources.Count - 1; i >= 0; i--)
        {
            EnemyIllumination source = lightSources[i];
            if (source == null) { lightSources.RemoveAt(i); continue; }
            if (!source.Illuminates(this))
            {
                continue;
            }

            Vector3 local = graphic.rectTransform.InverseTransformPoint(source.transform.position);
            renderLights.Add(new RoomDarknessGraphic.LightDisc(
                new Vector2(local.x, local.y), source.LightRadius, localLightSoftness));
        }
    }

    private void CollectRenderShadows()
    {
        renderShadows.Clear();
        EnemySpecialistTrait.CollectShadowPositions(this, shadowPositions);
        for (int i = 0; i < shadowPositions.Count; i++)
        {
            Vector3 local = graphic.rectTransform.InverseTransformPoint(shadowPositions[i]);
            renderShadows.Add(new RoomDarknessGraphic.ShadowDisc(
                new Vector2(local.x, local.y), 1f, localLightSoftness));
        }
    }

    private void OnEnable() { needsRefresh = true; }
    private void OnValidate() { needsRefresh = true; }

    private void LateUpdate()
    {
        if (needsRefresh || previousSize != BoardSize || !Mathf.Approximately(previousLighting, LitFraction)
            || previousShadowCount > 0 || EnemySpecialistTrait.HasActiveShadow(this))
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (graphic != null)
        {
            graphic.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (graphic == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(graphic.gameObject);
        }
        else
        {
            DestroyImmediate(graphic.gameObject);
        }
    }
}

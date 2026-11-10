using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameplayBackdropUI : MonoBehaviour
{
    public static readonly Color CameraColor = new Color(0.105f, 0.038f, 0.018f, 1f);
    private const string RootName = "Pumpkin Backdrop HUD";
    private int shownLocation;
    private Camera gameplayCamera;

    public static GameplayBackdropUI Ensure(Camera gameplayCamera)
    {
        if (gameplayCamera == null) return null;
        gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
        gameplayCamera.backgroundColor = CameraColor;

        GameplayBackdropUI existing = FindObjectOfType<GameplayBackdropUI>(true);
        if (existing != null)
        {
            existing.gameplayCamera = gameplayCamera;
            existing.Build();
            return existing;
        }

        GameObject root = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GameplayBackdropUI));
        GameplayBackdropUI backdrop = root.GetComponent<GameplayBackdropUI>();
        backdrop.gameplayCamera = gameplayCamera;
        backdrop.Build();
        return backdrop;
    }

    private void Awake()
    {
        gameplayCamera = Camera.main;
        Build();
        RefreshLocation();
    }

    private void Update() => RefreshLocation();

    private void LateUpdate()
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        CameraBehaviour focus = gameplayCamera != null
            ? gameplayCamera.GetComponent<CameraBehaviour>() : null;
        Stage room = focus != null && focus.CurrentRoom != null
            ? focus.CurrentRoom.GetComponent<Stage>() : null;
        if (room == null || gameplayCamera == null) return;

        Bounds bounds = room.GetViewBounds();
        Vector3 lower = gameplayCamera.WorldToViewportPoint(bounds.min);
        Vector3 upper = gameplayCamera.WorldToViewportPoint(bounds.max);
        // A tiny overlap prevents a one-pixel seam from camera rounding.
        const float overlap = 0.002f;
        Vector2 min = new Vector2(Mathf.Clamp01(lower.x + overlap),
            Mathf.Clamp01(lower.y + overlap));
        Vector2 max = new Vector2(Mathf.Clamp01(upper.x - overlap),
            Mathf.Clamp01(upper.y - overlap));
        GetComponentInChildren<LocationBackdropGraphic>(true)?.SetBoardWindow(min, max);
        GetComponentInChildren<PumpkinBackdropGraphic>(true)?.SetBoardWindow(min, max);
    }

    private void RefreshLocation()
    {
        int location = LevelProgression.Instance != null
            && (GameManager.Instance == null || !GameManager.Instance.IsSurvivalMode)
            ? LevelProgression.Instance.CurrentLocationIndex : 1;
        if (shownLocation == location) return;
        shownLocation = location;
        LocationBackdropGraphic exterior = GetComponentInChildren<LocationBackdropGraphic>(true);
        PumpkinBackdropGraphic ornament = GetComponentInChildren<PumpkinBackdropGraphic>(true);
        exterior?.ShowLocation(location);
        ornament?.ShowLocation(location);
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            gameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            Color edge = ornament != null ? ornament.CurrentEdgeColor : CameraColor;
            gameplayCamera.backgroundColor = new Color(edge.r, edge.g, edge.b, 1f);
        }
    }

    public void Build()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        // Overlay canvases always cover world sprites, regardless of sortingOrder.
        // Put scenery in the camera's sorting stack so the floor and animal frame
        // remain visible outside the rectangular gameplay window as well.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = gameplayCamera;
        if (gameplayCamera != null)
            canvas.planeDistance = Mathf.Lerp(gameplayCamera.nearClipPlane,
                gameplayCamera.farClipPlane, 0.9f);
        canvas.sortingLayerID = 0;
        canvas.sortingOrder = -200;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        Transform art = transform.Find("Pumpkin And Vine Pattern");
        if (art == null)
        {
            GameObject artObject = new GameObject(
                "Pumpkin And Vine Pattern",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(PumpkinBackdropGraphic));
            artObject.transform.SetParent(transform, false);
            art = artObject.transform;
        }

        RectTransform artRect = art as RectTransform;
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        PumpkinBackdropGraphic graphic = art.GetComponent<PumpkinBackdropGraphic>();
        graphic.raycastTarget = false;
        graphic.SetVerticesDirty();
    }
}

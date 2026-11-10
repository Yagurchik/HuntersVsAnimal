using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraRenderer : MonoBehaviour
{
    public Vector2 DefaultResolution = new Vector2(720, 1280);
    [Range(0f, 1f)] public float WidthOrHeight = 0;

    [Header("Battlefield framing")]
    [SerializeField, Range(0f, 0.1f)] private float sidePadding = 0.015f;
    [SerializeField, Range(0f, 0.3f)] private float topHudSpace = 0.15f;
    [SerializeField, Range(0f, 0.3f)] private float bottomHudSpace = 0.15f;

    [Header("Room transition")]
    [SerializeField, Min(0f)] private float roomTransitionDuration = 0.42f;

    private Camera componentCamera;
    private float initialSize;
    private float targetAspect;
    private float initialFov;
    private float horizontalFov;
    private Stage currentRoom;
    private BattlefieldForestStyle currentRoomStyle;
    private Bounds roomBounds;
    private bool initialized;
    private bool transitioning;
    private float transitionElapsed;
    private Vector3 transitionStartPosition;
    private float transitionStartSize;

    public bool IsTransitioning => transitioning;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        componentCamera = GetComponent<Camera>();
        GameplayBackdropUI.Ensure(componentCamera);
        transform.rotation = Quaternion.identity;
        initialSize = componentCamera.orthographicSize;
        targetAspect = DefaultResolution.x / Mathf.Max(1f, DefaultResolution.y);
        initialFov = componentCamera.fieldOfView;
        horizontalFov = CalcVerticalFov(initialFov, 1f / targetAspect);
        initialized = true;
    }

    public void FrameRoom(Stage room)
    {
        FrameRoom(room, false);
    }

    public void FrameRoom(Stage room, bool animated)
    {
        Initialize();
        currentRoom = room;
        currentRoomStyle = room != null ? room.GetComponent<BattlefieldForestStyle>() : null;
        transitioning = false;
        if (room == null)
        {
            return;
        }

        roomBounds = GetPresentationBounds();
        if (animated && Application.isPlaying && roomTransitionDuration > 0f
            && TryGetRoomFraming(out _, out _))
        {
            transitionStartPosition = transform.position;
            transitionStartSize = componentCamera.orthographicSize;
            transitionElapsed = 0f;
            transitioning = true;
            return;
        }
        ApplyRoomFraming();
    }

    private void LateUpdate()
    {
        if (currentRoom != null && componentCamera.orthographic)
        {
            roomBounds = GetPresentationBounds();
            if (transitioning && TryGetRoomFraming(out Vector3 targetPosition, out float targetSize))
            {
                transitionElapsed += Time.deltaTime;
                float progress = roomTransitionDuration > 0f
                    ? Mathf.Clamp01(transitionElapsed / roomTransitionDuration)
                    : 1f;
                float eased = progress * progress * (3f - 2f * progress);
                transform.position = Vector3.LerpUnclamped(transitionStartPosition, targetPosition, eased);
                componentCamera.orthographicSize = Mathf.LerpUnclamped(transitionStartSize, targetSize, eased);
                if (progress >= 1f)
                {
                    transitioning = false;
                }

                return;
            }
            transitioning = false;
            ApplyRoomFraming();
            return;
        }

        float aspect = Mathf.Max(0.01f, componentCamera.aspect);
        if (componentCamera.orthographic)
        {
            float constantWidthSize = initialSize * (targetAspect / aspect);
            componentCamera.orthographicSize = Mathf.Lerp(constantWidthSize, initialSize, WidthOrHeight);
        }
        else
        {
            float constantWidthFov = CalcVerticalFov(horizontalFov, aspect);
            componentCamera.fieldOfView = Mathf.Lerp(constantWidthFov, initialFov, WidthOrHeight);
        }
    }

    private Bounds GetPresentationBounds()
    {
        Bounds gameplay = currentRoom.GetViewBounds();
        return currentRoomStyle != null ? currentRoomStyle.GetFramingBounds(gameplay) : gameplay;
    }

    private void ApplyRoomFraming()
    {
        if (!TryGetRoomFraming(out Vector3 position, out float orthographicSize))
        {
            return;
        }

        componentCamera.orthographicSize = orthographicSize;
        transform.position = position;
    }

    private bool TryGetRoomFraming(out Vector3 position, out float orthographicSize)
    {
        // Every defense line uses the same upright X/Y board axes.
        transform.rotation = Quaternion.identity;
        Rect viewport = componentCamera.pixelRect;
        position = transform.position;
        orthographicSize = componentCamera.orthographicSize;
        if (viewport.width < 1f || viewport.height < 1f)
        {
            return false;
        }

        Rect safe = componentCamera.targetTexture != null ? viewport : Screen.safeArea;
        if (safe.width < 1f || safe.height < 1f)
        {
            safe = new Rect(0f, 0f, Screen.width, Screen.height);
        }
        float left = Mathf.Max(0f, (safe.xMin - viewport.xMin) / viewport.width) + sidePadding;
        float right = Mathf.Min(1f, (safe.xMax - viewport.xMin) / viewport.width) - sidePadding;
        float bottom = Mathf.Max(bottomHudSpace, (safe.yMin - viewport.yMin) / viewport.height);
        float top = Mathf.Min(1f - topHudSpace, (safe.yMax - viewport.yMin) / viewport.height);
        float availableWidth = Mathf.Max(0.1f, right - left);
        float availableHeight = Mathf.Max(0.1f, top - bottom);

        Vector3 boundsSize = roomBounds.size;
        float aspect = Mathf.Max(0.01f, componentCamera.aspect);
        orthographicSize = Mathf.Max(
            boundsSize.x / (2f * aspect * availableWidth),
            boundsSize.y / (2f * availableHeight));

        float viewHeight = orthographicSize * 2f;
        float offsetX = ((left + right) * 0.5f - 0.5f) * viewHeight * aspect;
        float offsetY = ((bottom + top) * 0.5f - 0.5f) * viewHeight;
        position = roomBounds.center - new Vector3(offsetX, offsetY, 0f);
        position.z = transform.position.z;
        return true;
    }

    private static float CalcVerticalFov(float horizontalFov, float aspectRatio)
    {
        return 2f * Mathf.Atan(Mathf.Tan(horizontalFov * Mathf.Deg2Rad * 0.5f) / aspectRatio) * Mathf.Rad2Deg;
    }
}

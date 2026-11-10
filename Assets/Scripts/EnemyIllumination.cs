using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(800)]
public sealed class EnemyIllumination : MonoBehaviour
{
    [SerializeField] private bool illuminated;
    [SerializeField, Min(0.1f)] private float lightRadius = 1f;

    private Brick enemy;
    private RoomDarkness registeredRoom;
    private Vector2Int previousLightPosition;
    private float previousLightRadius;

    public bool IsIlluminated => illuminated;
    public float LightRadius => Mathf.Max(0.1f, lightRadius);

    private void Awake() => enemy = GetComponent<Brick>();

    private void OnEnable()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Brick>();
        }

        SyncRoom();
    }

    private void OnValidate()
    {
        lightRadius = Mathf.Max(0.1f, lightRadius);
        if (isActiveAndEnabled)
        {
            SyncRoom();
            registeredRoom?.NotifyLightChanged(this);
        }
    }

    private void LateUpdate()
    {
        SyncRoom();
        if (registeredRoom == null)
        {
            return;
        }

        Vector2Int position = LightPosition();
        if (position == previousLightPosition && LightRadius == previousLightRadius)
        {
            return;
        }

        previousLightPosition = position;
        previousLightRadius = LightRadius;
        registeredRoom.NotifyLightChanged(this);
    }

    private Vector2Int LightPosition() => new Vector2Int(
        Mathf.RoundToInt(transform.position.x * 100f), Mathf.RoundToInt(transform.position.y * 100f));

    public void Illuminate(float radius)
    {
        float nextRadius = Mathf.Max(lightRadius, radius, 0.1f);
        bool changed = !illuminated || lightRadius != nextRadius;
        illuminated = true;
        lightRadius = nextRadius;
        SyncRoom();
        if (changed)
        {
            registeredRoom?.NotifyLightChanged(this);
        }

        GetComponent<EnemyDarknessVisibility>()?.RefreshVisibility();
    }

    public void Extinguish()
    {
        if (!illuminated)
        {
            return;
        }

        illuminated = false;
        registeredRoom?.UnregisterLight(this);
        registeredRoom = null;
        GetComponent<EnemyDarknessVisibility>()?.RefreshVisibility();
    }

    public bool Illuminates(RoomDarkness room)
    {
        return illuminated && isActiveAndEnabled && gameObject.activeInHierarchy && registeredRoom == room;
    }

    public bool Illuminates(RoomDarkness room, Vector3 worldPosition)
    {
        return Illuminates(room) && ((Vector2)transform.position - (Vector2)worldPosition).sqrMagnitude
            <= LightRadius * LightRadius + 0.0001f;
    }

    private void SyncRoom()
    {
        RoomDarkness current = null;
        if (illuminated && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            if (enemy == null)
            {
                enemy = GetComponent<Brick>();
            }

            current = enemy != null && enemy.levelGenerator != null
                ? enemy.levelGenerator.Darkness
                : GetComponentInParent<RoomDarkness>(true);
        }
        if (current == registeredRoom)
        {
            return;
        }

        registeredRoom?.UnregisterLight(this);
        registeredRoom = current;
        registeredRoom?.RegisterLight(this);
        previousLightPosition = LightPosition();
        previousLightRadius = LightRadius;
    }

    private void OnDisable()
    {
        registeredRoom?.UnregisterLight(this);
        registeredRoom = null;
    }

    private void OnDestroy()
    {
        registeredRoom?.UnregisterLight(this);
        registeredRoom = null;
    }
}

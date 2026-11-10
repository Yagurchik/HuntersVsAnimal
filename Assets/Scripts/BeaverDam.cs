using UnityEngine;

// One persistent, room-local barrier. Enemy movement is grid based, so breaches
// are resolved by DefenseLineRuntime rather than by a physics collider.
[DisallowMultipleComponent]
public sealed class BeaverDam : MonoBehaviour, IDefenseHealthTarget
{
    private const float BarricadeHealthFraction = 0.25f;
    [SerializeField] private SpriteRenderer artwork;
    private float health;
    private float maximumHealth;
    private bool healthBarRegistered;
    private Stage ownerRoom;

    public float Health => health;
    public float MaximumHealth => maximumHealth;
    public float DefenseHealth => health;
    public float DefenseMaxHealth => maximumHealth;
    public Vector3 DefenseHealthAnchor => ownerRoom != null
        ? ownerRoom.DefenseHealthAnchor : transform.position;

    public void AddHealth(float amount, Stage room)
    {
        if (amount < 0f || room == null)
        {
            return;
        }

        ownerRoom = room;
        maximumHealth = ResolveHealthLimit(room);
        health = Mathf.Min(maximumHealth, health + amount);
        float width = room.BattlefieldSize.x - 0.4f;
        LevelGenerator battlefield = room.GetComponentInChildren<LevelGenerator>(true);
        float fieldDepth = battlefield != null ? battlefield.transform.position.z
            : room.PlayerObject != null ? room.PlayerObject.transform.position.z
            : room.transform.position.z;
        transform.SetParent(room.transform, false);
        transform.position = new Vector3(room.transform.position.x,
            room.DefenseBoundaryY + 0.25f, fieldDepth);
        if (artwork == null)
        {
            artwork = GetComponent<SpriteRenderer>();
        }

        if (artwork != null && artwork.sprite != null)
        {
            artwork.sortingOrder = BattlefieldForestStyle.DefenseStructureSortingOrder;
            Vector2 source = artwork.sprite.bounds.size;
            transform.localScale = new Vector3(width / Mathf.Max(0.01f, source.x),
                1.5f / Mathf.Max(0.01f, source.y), 1f);
        }
        RefreshVisual();
        if (!healthBarRegistered && Application.isPlaying)
        {
            EnemyCombatTextUI.Ensure().RegisterDefense(this);
            healthBarRegistered = true;
        }
    }

    private static float ResolveHealthLimit(Stage room)
    {
        DefenseLineRuntime front = GameManager.Instance?.Front;
        if (front == null)
        {
            front = room.GetComponentInParent<DefenseLineRuntime>();
        }

        MainRoomBarricade barricade = room.GetComponent<MainRoomBarricade>();
        float barricadeMaximum = front != null ? front.BarricadeMaxHealth
            : barricade != null ? barricade.MaxHealth
            : MainRoomBarricade.DefaultMaxHealth + MetaProgression.BarricadeHealthBonus;
        return Mathf.Max(0f, barricadeMaximum) * BarricadeHealthFraction;
    }

    // Returns damage that penetrates the dam.
    public float Absorb(float damage)
    {
        float incoming = Mathf.Max(0f, damage);
        float mitigation = ArtifactEquipmentBonuses.DefenseDamageMultiplier;
        float absorbed = Mathf.Min(health, incoming * mitigation);
        health -= absorbed;
        RefreshVisual();
        if (health <= 0f)
        {
            Destroy(gameObject);
        }

        return Mathf.Max(0f, incoming - absorbed / mitigation);
    }

    private void RefreshVisual()
    {
        if (artwork == null)
        {
            artwork = GetComponent<SpriteRenderer>();
        }

        if (artwork == null)
        {
            return;
        }

        float ratio = maximumHealth > 0f ? health / maximumHealth : 0f;
        artwork.color = Color.Lerp(new Color(0.75f, 0.62f, 0.48f, 0.95f),
            Color.white, Mathf.Clamp01(ratio));
    }

    private void OnDestroy()
    {
        if (healthBarRegistered && EnemyCombatTextUI.Instance != null)
        {
            EnemyCombatTextUI.Instance.UnregisterDefense(this);
        }
    }
}

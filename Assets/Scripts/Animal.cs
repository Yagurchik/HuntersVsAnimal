using UnityEngine;
using UnityEngine.Serialization;

public class Animal : MonoBehaviour
{
    [FormerlySerializedAs("speed")]
    [SerializeField, Min(0.1f)] private float returnSpeed = 40f;
    public const float RecallTimeout = 2f;
    [SerializeField] public float dmg;
    [SerializeField] float hp;
    private GameObject player;
    private Player returnPlayer;

    private Collider2D[] objectColliders;
    private Rigidbody2D _rb;
    private ProjectilePhysics2D projectilePhysics;
    private HoneyBadgerProjectile honeyBadger;
    private BoarProjectile boar;
    private BeaverProjectile beaver;
    private RabbitReplication rabbit;
    private float returnDeadline;
    private bool completedAtBottom;
    public bool IsReturning { get; private set; }
    public Player ReturnPlayer => returnPlayer;
    public int ComboVolleyId { get; private set; }
    public CombatDamageSource CombatSource { get; private set; }
    public float Health => Mathf.Max(0f, hp);

    public void SetComboVolley(int volleyId) => ComboVolleyId = volleyId;
    public void SetCombatSource(CombatDamageSource source) => CombatSource = source;

    public void SetReturnTarget(GameObject target)
    {
        player = target;
        returnPlayer = target != null ? target.GetComponent<Player>() : null;
    }

    public void ApplyPermanentHealthMultiplier(float multiplier)
    {
        hp *= Mathf.Max(0f, multiplier);
    }

    public void SetRuntimeDamage(float damage)
    {
        dmg = Mathf.Max(0f, damage);
    }

    public void SetRuntimeHealth(float health)
    {
        hp = Mathf.Max(0f, health);
    }

    public bool TakeEnemyDamage(float damage)
    {
        if (hp <= 0f) return true;
        hp = Mathf.Max(0f, hp - Mathf.Max(0f, damage));
        return hp <= 0f;
    }
    private void Awake()
    {
        if (!Player.animalsCollect.Contains(this))
        {
            Player.animalsCollect.Add(this);
        }

        player = GameObject.FindWithTag("Player");
        returnPlayer = player != null ? player.GetComponent<Player>() : null;
        objectColliders = GetComponentsInChildren<Collider2D>(true);
        _rb = GetComponent<Rigidbody2D>();
        projectilePhysics = GetComponent<ProjectilePhysics2D>();
        honeyBadger = GetComponent<HoneyBadgerProjectile>();
        boar = GetComponent<BoarProjectile>();
        beaver = GetComponent<BeaverProjectile>();
        if (player == null)
        {
            Debug.LogError("Player not found!");
        }
    }

    public virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            GhostPhasing ghost = collision.gameObject.GetComponentInParent<GhostPhasing>();
            if (ghost != null && ghost.TryPassThrough(this)) return;
            // Special projectiles own their enemy-contact and lifetime rules.
            if (rabbit == null) rabbit = GetComponent<RabbitReplication>();
            if (honeyBadger != null || boar != null || beaver != null || rabbit != null
                || GetComponent<FrogProjectile>() != null) return;
            Brick brick = collision.gameObject.GetComponent<Brick>();
            if (hp > 0)
            {
                hp -= brick.ProjectileContactDamage;
            }
            if (hp <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
    void Update()
    {
        // Only recalled projectiles have a timeout; ordinary ricochets can continue.
        if (IsReturning && Time.time >= returnDeadline)
        {
            Destroy(gameObject);
            return;
        }
        if (player == null || completedAtBottom || (beaver != null && beaver.IsFlying)) return;

        if (transform.position.y < player.transform.position.y - 0.5)
        {
            if (IsReturning)
            {
                Destroy(gameObject);
                return;
            }
            if (boar != null)
            {
                boar.RescueAbove(player.transform.position.y - 0.25f);
                return;
            }
            CompleteAtBottom();
        }
    }

    public void CompleteAtBottom()
    {
        if (completedAtBottom || IsReturning) return;
        completedAtBottom = true;
        returnPlayer?.TryCommitProjectileLanding(transform.position.x);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Player.animalsCollect.Remove(this);
        // Reset projectile-phase fast-forward before the last projectile can
        // open an upgrade window or hand control to the enemy turn.
        GameManager.Instance?.NotifyProjectileCollectionChanged();

        // When every projectile was destroyed by enemies before reaching the exit,
        // the last impact still becomes the firing position for the next volley.
        if (Application.isPlaying && !IsReturning && !completedAtBottom && returnPlayer != null)
        {
            returnPlayer.NotifyProjectileDestroyed(transform.position.x);
        }

        if (Application.isPlaying && returnPlayer != null)
        {
            returnPlayer.NotifyProjectileResolved();
        }

        if (Player.animalsCollect.Count == 0)
        {
            GameManager.Instance?.TryCompletePlayerTurn();
        }
    }
    public void DropDown()
    {
        DropDown(Time.time + RecallTimeout);
    }

    public void DropDown(float deadline)
    {
        if (honeyBadger == null) honeyBadger = GetComponent<HoneyBadgerProjectile>();
        if (honeyBadger != null && honeyBadger.PreventsRecall) return;
        if (boar == null) boar = GetComponent<BoarProjectile>();
        if (boar != null && boar.PreventsRecall) return;
        boar?.PrepareForRecall();
        if (beaver == null) beaver = GetComponent<BeaverProjectile>();
        beaver?.PrepareForRecall();
        // The scene button has several listeners. Repeated calls must not extend the wait.
        if (IsReturning) return;
        IsReturning = true;
        returnDeadline = deadline;
        returnPlayer?.CancelProjectileLandingForRecall();
        foreach (Collider2D objectCollider in objectColliders)
            if (objectCollider != null) objectCollider.enabled = false;

        // Aim below the exit, not at the player's centre (which may be almost horizontal).
        Vector2 target = player != null ? (Vector2)player.transform.position : (Vector2)transform.position;
        target.y = Mathf.Min(target.y, transform.position.y) - 1f;
        if (projectilePhysics == null) projectilePhysics = GetComponent<ProjectilePhysics2D>();
        float speed = Mathf.Max(returnSpeed,
            projectilePhysics != null ? projectilePhysics.TargetSpeed * 2f : 0f);
        if (projectilePhysics != null)
        {
            projectilePhysics.ReturnTo(target, speed);
        }
        else if (_rb != null)
        {
            Vector2 direction = (target - _rb.position).normalized;
            _rb.velocity = direction * speed;
        }
    }
}

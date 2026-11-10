using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animal), typeof(Rigidbody2D), typeof(ProjectilePhysics2D))]
public sealed class BeaverProjectile : MonoBehaviour
{
    [Header("Burrowing flight")]
    [SerializeField, Min(1f)] private float enemySpeedDivider = 5f;
    [SerializeField, Min(0.01f)] private float damageInterval = 0.05f;
    [SerializeField, Min(0.05f)] private float sweepRadius = 0.24f;
    [SerializeField, Min(1f)] private float maximumFlightSeconds = 20f;
    [Header("Authored effects")]
    [SerializeField] private BeaverDam damPrefab;
    [SerializeField] private BeaverSawdustParticle sawdustPrefab;
    [SerializeField] private int visualSortingOrder = 80;
    [SerializeField] private float visualAngleOffset = 180f;

    private readonly Dictionary<Brick, float> timeInside = new Dictionary<Brick, float>();
    private readonly HashSet<Brick> touching = new HashSet<Brick>();
    private readonly Collider2D[] overlapBuffer = new Collider2D[64];
    private readonly List<Brick> stale = new List<Brick>();
    private Animal animal;
    private Rigidbody2D body;
    private ProjectilePhysics2D flight;
    private Stage room;
    private BeaverDam dam;
    private Vector2 heading = Vector2.up;
    private float speed;
    private float flightTime;
    private float damageDealt;
    private int remainingPumpkinBounces;
    private float damConversion = 0.01f;
    private bool launched;
    private Brick firstEnemy;
    private bool encounteredFirstEnemy;
    private bool passedFirstEnemy;
    private bool requestedFastForward;

    public bool IsFlying => launched;
    public float DamageDealt => damageDealt;
    public bool HasPassedFirstEnemy => passedFirstEnemy;

    private void Awake()
    {
        animal = GetComponent<Animal>();
        body = GetComponent<Rigidbody2D>();
        flight = GetComponent<ProjectilePhysics2D>();
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = visualSortingOrder;
        }
    }

    public void ConfigureRunUpgrades(int defenseHealthUpgradeLevel, float bonusDamage, bool ultimate)
    {
        remainingPumpkinBounces = ultimate ? 1 : 0;
        damConversion = (1 + Mathf.Clamp(defenseHealthUpgradeLevel, 0, 3)) * 0.01f
           ;
        if (animal != null)
        {
            animal.SetRuntimeDamage(animal.dmg + Mathf.Max(0f, bonusDamage));
        }
    }

    public void Launch(Vector2 direction, float launchSpeed)
    {
        room = animal != null && animal.ReturnPlayer != null
            ? animal.ReturnPlayer.GetComponentInParent<Stage>() : null;
        heading = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
        FaceFlightDirection();
        if (room != null)
        {
            Vector3 launchPosition = transform.position;
            launchPosition.y = Mathf.Max(launchPosition.y, room.FieldBottomY + 0.35f);
            transform.position = launchPosition;
        }
        speed = Mathf.Max(1f, launchSpeed);
        flightTime = 0f;
        damageDealt = 0f;
        firstEnemy = null;
        encounteredFirstEnemy = passedFirstEnemy = requestedFastForward = false;
        timeInside.Clear();
        if (room != null && room.DefenseLine > 1)
        {
            EnsureDam();
        }

        flight?.StopFlight();
        if (flight != null)
        {
            flight.enabled = false;
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
        launched = true;
    }

    public void PrepareForRecall()
    {
        if (!launched)
        {
            return;
        }

        launched = false;
        if (flight != null)
        {
            flight.enabled = true;
        }

        if (body != null)
        {
            body.simulated = true;
        }
    }

    private void FixedUpdate()
    {
        if (!launched || animal == null || animal.IsReturning)
        {
            return;
        }

        flightTime += Time.fixedDeltaTime;
        if (flightTime >= maximumFlightSeconds || room == null)
        {
            Finish();
            return;
        }

        int beforeCount = FindTouching(transform.position);
        UpdateFirstEnemyPassage();
        float travelSpeed = speed / (beforeCount > 0 ? enemySpeedDivider : 1f);
        Vector2 next = (Vector2)transform.position + heading * (travelSpeed * Time.fixedDeltaTime);
        Vector2 center = room.transform.position;
        Vector2Int size = room.BattlefieldSize;
        float left = center.x - size.x * 0.5f + sweepRadius;
        float right = center.x + size.x * 0.5f - sweepRadius;
        float top = center.y + size.y * 0.5f - sweepRadius;
        float bottom = room.FieldBottomY + 0.32f;
        bool crossingSide = (heading.x < 0f && next.x <= left) || (heading.x > 0f && next.x >= right);
        bool crossingTop = heading.y > 0f && next.y >= top;
        bool crossingBottom = heading.y < 0f && next.y <= bottom;
        next.x = Mathf.Clamp(next.x, left, right);
        next.y = Mathf.Clamp(next.y, bottom, top);
        // Stop at the first enemy crossed this step so a fast shot cannot skip
        // a thin collider before the fivefold slowdown takes effect.
        if (beforeCount == 0)
        {
            next = StopAtFirstEnemyOnPath(transform.position, next);
        }
        // Sweeping can stop this step inside an enemy before the predicted wall.
        // Reflect and request acceleration only when the actual position reaches it.
        bool bouncedOffWall = false;
        if (crossingSide && (next.x <= left + 0.0001f || next.x >= right - 0.0001f))
        {
            heading.x = -heading.x;
            bouncedOffWall = true;
        }
        if (crossingTop && next.y >= top - 0.0001f)
        {
            heading.y = -Mathf.Abs(heading.y);
            bouncedOffWall = true;
        }
        if (crossingBottom && next.y <= bottom + 0.0001f)
        {
            // The pumpkin field is separate from the side and upper walls.
            if (remainingPumpkinBounces > 0)
            {
                remainingPumpkinBounces--;
                heading.y = Mathf.Abs(heading.y);
            }
            else
            {
                transform.position = next;
                Finish();
                return;
            }
        }
        transform.position = next;
        FaceFlightDirection();
        FindTouching(next);
        DamageTouchingEnemies(Time.fixedDeltaTime);
        UpdateFirstEnemyPassage();
        if (bouncedOffWall && passedFirstEnemy && !requestedFastForward)
        {
            requestedFastForward = true;
            GameManager.Instance?.RequestProjectileFastForward();
        }
    }

    private Vector2 StopAtFirstEnemyOnPath(Vector2 start, Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (sweepRadius * 1.5f)));
        for (int step = 1; step <= steps; step++)
        {
            Vector2 sample = Vector2.Lerp(start, end, (float)step / steps);
            if (FindTouching(sample) > 0)
            {
                return sample;
            }
        }

        return end;
    }

    private void UpdateFirstEnemyPassage()
    {
        if (!encounteredFirstEnemy || passedFirstEnemy)
        {
            return;
        }

        if (firstEnemy == null || firstEnemy.IsDefeated || !touching.Contains(firstEnemy))
        {
            passedFirstEnemy = true;
        }
    }

    private void FaceFlightDirection()
    {
        if (heading.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.FromToRotation(Vector3.up, heading)
                * Quaternion.Euler(0f, 0f, visualAngleOffset);
        }
    }

    private int FindTouching(Vector2 position)
    {
        touching.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(position, sweepRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = overlapBuffer[i];
            Brick enemy = collider != null ? collider.GetComponentInParent<Brick>() : null;
            if (enemy != null && !enemy.IsDefeated && enemy.levelGenerator != null
                && enemy.levelGenerator.GetComponentInParent<Stage>() == room)
            {
                touching.Add(enemy);
            }
        }
        return touching.Count;
    }

    private void DamageTouchingEnemies(float delta)
    {
        stale.Clear();
        foreach (KeyValuePair<Brick, float> pair in timeInside)
        {
            if (pair.Key == null || !touching.Contains(pair.Key))
            {
                stale.Add(pair.Key);
            }
        }

        foreach (Brick enemy in stale)
        {
            timeInside.Remove(enemy);
        }

        foreach (Brick enemy in touching)
        {
            if (enemy == null || enemy.IsDefeated)
            {
                continue;
            }
            // First bite is immediate; further bites occur every 0.05 seconds
            // while this same enemy still encloses the projectile.
            if (!timeInside.TryGetValue(enemy, out float elapsed))
            {
                Bite(enemy);
                elapsed = 0f;
            }
            elapsed += delta;
            while (elapsed >= damageInterval && enemy != null && !enemy.IsDefeated)
            {
                elapsed -= damageInterval;
                Bite(enemy);
            }
            if (enemy != null)
            {
                timeInside[enemy] = elapsed;
            }
        }
    }

    private void Bite(Brick enemy)
    {
        if (enemy == null || enemy.IsDefeated)
        {
            return;
        }

        if (!encounteredFirstEnemy)
        {
            encounteredFirstEnemy = true;
            firstEnemy = enemy;
        }
        float previousHealth = enemy.hp;
        Vector2 point = transform.position;
        enemy.ApplyProjectileHit(animal, point, -heading);
        EmitSawdust(point, heading);
        float actualDamage = Mathf.Max(0f, previousHealth - enemy.hp);
        if (actualDamage <= 0f)
        {
            return;
        }

        damageDealt += actualDamage;
        float reinforcement = actualDamage * damConversion;
        if (room != null && room.DefenseLine == 1)
        {
            MainRoomBarricade barricade = room.GetComponent<MainRoomBarricade>();
            if (barricade == null)
            {
                barricade = GameManager.Instance?.Front?.Barricade;
            }

            barricade?.AddBeaverHealth(reinforcement);
        }
        else
        {
            if (dam == null)
            {
                EnsureDam();
            }

            dam?.AddHealth(reinforcement, room);
        }
    }

    private void EmitSawdust(Vector2 point, Vector2 direction)
    {
        if (sawdustPrefab == null)
        {
            return;
        }

        Vector2 side = new Vector2(-direction.y, direction.x).normalized;
        for (int i = 0; i < 2; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            Vector2 spray = (side * sign + direction * Random.Range(-0.35f, 0.2f)).normalized;
            Vector2 origin = point + side * (sign * 0.27f);
            BeaverSawdustParticle.Spawn(sawdustPrefab, origin, spray, Random.Range(2.2f, 3.6f));
        }
    }

    private void Finish()
    {
        if (!launched)
        {
            return;
        }

        launched = false;
        animal.CompleteAtBottom();
    }

    private void EnsureDam()
    {
        if (room == null || room.DefenseLine == 1 || damPrefab == null)
        {
            return;
        }

        dam = room.GetComponentInChildren<BeaverDam>(true);
        if (dam == null)
        {
            dam = Instantiate(damPrefab, room.transform);
        }

        dam.AddHealth(0f, room);
    }
}

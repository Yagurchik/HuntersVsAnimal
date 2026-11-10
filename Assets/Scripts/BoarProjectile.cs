using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animal), typeof(Rigidbody2D), typeof(ProjectilePhysics2D))]
public sealed class BoarProjectile : ProjectileDamageModifier
{
    [SerializeField, Min(1f)] private float launchSpeedMultiplier = 1.5f;
    [SerializeField, Min(1)] private int maximumWallMisses = 7;
    [SerializeField, Min(0f)] private float wallBouncePauseDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float stunChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float bossStunChance = 0.05f;
    [SerializeField, Range(0.5f, 3f), Tooltip("Visual size while the boar is shown at a wall.")]
    private float wallDisplayScale = 1f;
    [SerializeField, Range(0f, 15f)] private float wallShakeAngle = 5f;
    [SerializeField, Range(0f, 0.15f)] private float wallShakeScale = 0.035f;
    [SerializeField, Range(0.5f, 3f), Tooltip("Multiplier applied to the complete projectile visual.")]
    private float visualScaleMultiplier = 1f;
    [SerializeField, Min(0f), Tooltip("Width of the damage sweep in world units. The boar has no physics collider.")]
    private float damageSweepRadius = 0.24f;

    private const float RayOriginInset = 0.002f;
    private const float CornerTolerance = 0.003f;
    private const float MaximumRayDistance = 200f;
    private const float MinimumCountedDiagonalSegmentLength = 1f;

    private readonly HashSet<Brick> hitEnemies = new HashSet<Brick>();
    private readonly HashSet<Brick> enemiesHitBefore = new HashSet<Brick>();
    private Rigidbody2D body;
    private ProjectilePhysics2D flight;
    private ProjectileFlightVisual flightVisual;
    private Animal animal;
    private Stage currentStage;
    private BoarChargeTrail chargeTrail;
    private Collider2D[] projectileColliders;
    private SpriteRenderer[] projectileRenderers;
    private bool[] rendererVisibility;
    private Coroutine trajectoryRoutine;
    private int wallsLayer = -1;
    private int wallBounceCount;
    private bool waitingAfterBounce;
    private float lastExitBounceTime = float.NegativeInfinity;
    private Vector2 currentDirection = Vector2.up;
    private Vector2 trajectoryPoint;
    private float trajectorySpeed = 18f;
    private float firstHitDamageMultiplier = 1f;
    private bool ultimateActive;
    private Vector3 authoredScale;

    private struct BoundaryHit
    {
        public Vector2 Point;
        public Vector2 Normal;
        public bool IsMirage;
    }

    public float LaunchSpeedMultiplier => Mathf.Max(1f, launchSpeedMultiplier);
    public int MaximumWallBounces => Mathf.Max(1, maximumWallMisses);
    public int WallBounceCount => wallBounceCount;
    public float WallBouncePauseDuration => Mathf.Max(0f, wallBouncePauseDuration);
    public float StunChance => Mathf.Clamp01(stunChance);
    public float BossStunChance => Mathf.Clamp01(bossStunChance);
    public bool PassesThroughEnemies => true;
    public bool IsWaitingAfterWallBounce => waitingAfterBounce;
    public bool PreventsRecall => waitingAfterBounce || trajectoryRoutine != null;
    public float FirstHitDamageMultiplier => Mathf.Max(1f, firstHitDamageMultiplier);
    public float VisualScaleMultiplier => Mathf.Clamp(visualScaleMultiplier, 0.5f, 3f);
    public float DamageSweepRadius => Mathf.Max(0f, damageSweepRadius);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        flight = GetComponent<ProjectilePhysics2D>();
        animal = GetComponent<Animal>();
        authoredScale = transform.localScale;
        transform.localScale = authoredScale * VisualScaleMultiplier;
        projectileColliders = GetComponentsInChildren<Collider2D>(true);
        wallsLayer = LayerMask.NameToLayer("walls");
        flightVisual = ProjectileFlightVisual.Ensure(gameObject, 0f);
        flightVisual.ConfigureSpin(0f);
        chargeTrail = BoarChargeTrail.Ensure(gameObject);
        CacheRenderers();
        SetBoarVisible(true);
        SetProjectileColliders(false);
    }

    private void OnEnable()
    {
        hitEnemies.Clear();
        enemiesHitBefore.Clear();
        wallBounceCount = 0;
        waitingAfterBounce = false;
    }

    public void ConfigureRunUpgrades(int maximumBounces, float firstHitMultiplier,
        bool hasUltimate)
    {
        maximumWallMisses = Mathf.Max(1, maximumBounces);
        firstHitDamageMultiplier = Mathf.Max(1f, firstHitMultiplier);
        ultimateActive = hasUltimate;
    }

    private void OnDisable()
    {
        chargeTrail?.CompleteSegment(transform.position);
    }

    public void Launch(Vector2 direction, float speed)
    {
        EnsurePhysics();
        if (trajectoryRoutine != null)
        {
            StopCoroutine(trajectoryRoutine);
        }

        hitEnemies.Clear();
        enemiesHitBefore.Clear();
        wallBounceCount = 0;
        waitingAfterBounce = false;
        currentDirection = SafeDirection(direction, Vector2.up);
        trajectorySpeed = Mathf.Max(1f, speed);
        trajectoryPoint = transform.position;
        currentStage = animal != null && animal.ReturnPlayer != null
            ? animal.ReturnPlayer.GetComponentInParent<Stage>() : null;
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
        SetProjectileColliders(false);
        SetBoarVisible(true);
        chargeTrail?.CompleteSegment(transform.position);
        UpdateFacing(currentDirection);
        trajectoryRoutine = StartCoroutine(FollowRayTrajectory());
    }

    public void PrepareForRecall()
    {
        if (trajectoryRoutine != null)
        {
            StopCoroutine(trajectoryRoutine);
            trajectoryRoutine = null;
        }
        waitingAfterBounce = false;
        chargeTrail?.CompleteSegment(transform.position);
        EnsurePhysics();
        if (flight != null)
        {
            flight.enabled = true;
        }

        if (body != null)
        {
            body.position = transform.position;
            body.simulated = true;
        }
    }

    private IEnumerator FollowRayTrajectory()
    {
        while (wallBounceCount < MaximumWallBounces)
        {
            Vector2 start = trajectoryPoint;
            if (!TryFindBoundary(start, currentDirection, out BoundaryHit boundary))
            {
                trajectoryRoutine = null;
                Destroy(gameObject);
                yield break;
            }

            hitEnemies.Clear();
            SetBoarVisible(true);
            chargeTrail?.BeginSegment(transform.position);
            yield return TravelAlongSegment(start, boundary.Point);
            trajectoryPoint = boundary.Point;
            chargeTrail?.CompleteSegment(transform.position);
            bool countsAsBounce = boundary.IsMirage
                || CountsAsBounce(start, boundary.Point, currentDirection);
            currentDirection = boundary.IsMirage
                ? ProjectilePhysics2D.ReturnToSenderDirection(currentDirection)
                : SafeDirection(Vector2.Reflect(currentDirection, boundary.Normal),
                    -currentDirection);
            UpdateFacing(currentDirection);
            SetPosition(ClampVisiblePosition(boundary.Point));
            if (countsAsBounce)
            {
                wallBounceCount++;
            }

            waitingAfterBounce = true;
            yield return ShakeAtWall();
            waitingAfterBounce = false;
            if (wallBounceCount >= MaximumWallBounces)
            {
                break;
            }
        }

        trajectoryRoutine = null;
        Destroy(gameObject);
    }

    private static bool CountsAsBounce(Vector2 start, Vector2 end, Vector2 direction)
    {
        bool diagonal = Mathf.Abs(direction.x) > 0.0001f
            && Mathf.Abs(direction.y) > 0.0001f;
        return !diagonal || Vector2.Distance(start, end) >= MinimumCountedDiagonalSegmentLength;
    }

    private IEnumerator TravelAlongSegment(Vector2 start, Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        if (distance <= 0.0001f)
        {
            yield break;
        }

        float duration = distance / Mathf.Max(1f, trajectorySpeed);
        float elapsed = 0f;
        Vector2 visibleStart = ClampVisiblePosition(start);
        Vector2 visibleEnd = ClampVisiblePosition(end);
        Vector2 previousVisiblePoint = visibleStart;
        SetPosition(visibleStart);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector2 visiblePoint = Vector2.Lerp(visibleStart, visibleEnd, progress);
            // The sprite is inset at the walls. Sweep that same visible path so
            // enemies the boar visibly crosses cannot be missed by the hit ray.
            DamageEnemiesAlong(previousVisiblePoint, visiblePoint);
            previousVisiblePoint = visiblePoint;
            SetPosition(visiblePoint);
            yield return null;
        }
        DamageEnemiesAlong(previousVisiblePoint, visibleEnd);
        SetPosition(visibleEnd);
    }

    private IEnumerator ShakeAtWall()
    {
        SetBoarVisible(true);
        Quaternion restingRotation = transform.localRotation;
        Vector3 restingScale = transform.localScale;
        Vector3 displayScale = restingScale * Mathf.Max(0.5f, wallDisplayScale);
        transform.localScale = displayScale;
        float duration = WallBouncePauseDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float angle = Random.Range(-wallShakeAngle, wallShakeAngle);
            float scalePulse = Random.Range(-wallShakeScale, wallShakeScale);
            transform.localRotation = restingRotation * Quaternion.Euler(0f, 0f, angle);
            transform.localScale = displayScale * (1f + scalePulse);
            yield return null;
        }
        transform.localRotation = restingRotation;
        transform.localScale = restingScale;
    }

    private bool TryFindBoundary(Vector2 start, Vector2 direction, out BoundaryHit boundary)
    {
        boundary = default;
        Vector2 rayStart = start + direction * RayOriginInset;
        RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, direction, MaximumRayDistance);
        float nearestDistance = float.PositiveInfinity;
        Vector2 point = default;
        Vector2 combinedNormal = Vector2.zero;
        bool isMirage = false;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            bool hitMirage = IsMirageBoundary(hit.collider);
            if (!hitMirage && !IsBoundary(hit.collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance - CornerTolerance)
            {
                nearestDistance = hit.distance;
                point = hit.point;
                combinedNormal = hit.normal;
                isMirage = hitMirage;
                found = true;
            }
            else if (Mathf.Abs(hit.distance - nearestDistance) <= CornerTolerance)
            {
                combinedNormal += hit.normal;
                isMirage |= hitMirage;
            }
        }
        if (!found)
        {
            return false;
        }

        boundary.Point = point;
        boundary.Normal = SafeDirection(combinedNormal, -direction);
        boundary.IsMirage = isMirage;
        return true;
    }

    private bool IsMirageBoundary(Collider2D candidate)
    {
        Brick enemy = candidate != null ? candidate.GetComponentInParent<Brick>() : null;
        return enemy != null && !enemy.IsDefeated
            && enemy.GetComponent<MirageCornerMirror>() != null
            && (currentStage == null || candidate.GetComponentInParent<Stage>() == currentStage);
    }

    private bool IsBoundary(Collider2D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (wallsLayer >= 0 && candidate.gameObject.layer == wallsLayer)
        {
            return true;
        }

        return candidate.GetComponentInParent<DestroyBallCollider>() != null;
    }

    private void DamageEnemiesAlong(Vector2 start, Vector2 end)
    {
        Vector2 travel = end - start;
        float distance = travel.magnitude;
        float sweepRadius = DamageSweepRadius;
        if (distance <= 0.000001f)
        {
            if (sweepRadius <= 0f)
            {
                return;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(start, sweepRadius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                TryDamageEnemy(overlaps[i], start, Vector2.up);
            }

            return;
        }

        Vector2 direction = travel / distance;
        RaycastHit2D[] hits = sweepRadius > 0f
            ? Physics2D.CircleCastAll(start, sweepRadius, direction, distance)
            : Physics2D.RaycastAll(start, direction, distance);
        for (int i = 0; i < hits.Length; i++)
        {
            TryDamageEnemy(hits[i].collider, hits[i].point, -direction);
        }
    }

    private void TryDamageEnemy(Collider2D collider, Vector2 impactPoint, Vector2 impactNormal)
    {
        Brick enemy = collider != null ? collider.GetComponentInParent<Brick>() : null;
        if (enemy == null || enemy.IsDefeated || !hitEnemies.Add(enemy))
        {
            return;
        }

        if (!enemy.ApplyProjectileHit(animal, impactPoint, impactNormal))
        {
            return;
        }

        if (!animal.gameObject.activeInHierarchy)
        {
            return;
        }

        float chance = IsBoss(enemy) ? BossStunChance : StunChance;
        if (!enemy.IsDefeated && Random.value < chance)
        {
            EnemyTurnStun.Apply(enemy);
        }
    }

    public override float ModifyDamage(Brick enemy, float baseDamage)
    {
        bool firstHit = enemy != null && enemiesHitBefore.Add(enemy);
        return baseDamage * (ultimateActive || firstHit ? FirstHitDamageMultiplier : 1f);
    }

    private static bool IsBoss(Brick enemy)
    {
        return enemy != null && (enemy.GetComponent<BossEnemyTurnController>() != null
            || enemy.Footprint.x > 1 || enemy.Footprint.y > 1);
    }

    public bool IsEnemyCollision(Collision2D collision) => collision != null
        && collision.gameObject != null
        && collision.gameObject.GetComponentInParent<Brick>() != null;

    public bool TryBeginWallBouncePause(Collision2D collision, Vector2 reflectedDirection)
        => false;

    public void BounceFromBottomZone(Collider2D zone, Collider2D projectileCollider)
    {
        float top = zone != null ? zone.bounds.max.y : transform.position.y;
        RescueAbove(top);
    }

    public void RescueAbove(float boundaryY)
    {
        if (Time.time - lastExitBounceTime < 0.08f)
        {
            return;
        }

        lastExitBounceTime = Time.time;
        if (trajectoryRoutine != null)
        {
            StopCoroutine(trajectoryRoutine);
        }

        Vector2 position = new Vector2(transform.position.x, boundaryY + RayOriginInset);
        trajectoryPoint = position;
        SetPosition(ClampVisiblePosition(position));
        chargeTrail?.CompleteSegment(transform.position);
        wallBounceCount++;
        if (wallBounceCount >= MaximumWallBounces)
        {
            Destroy(gameObject);
            return;
        }
        currentDirection = new Vector2(currentDirection.x,
            Mathf.Max(0.2f, Mathf.Abs(currentDirection.y))).normalized;
        UpdateFacing(currentDirection);
        SetPosition(ClampVisiblePosition(trajectoryPoint));
        trajectoryRoutine = StartCoroutine(FollowRayTrajectory());
    }

    private Vector3 TrajectoryWorldPoint()
    {
        return new Vector3(trajectoryPoint.x, trajectoryPoint.y, transform.position.z);
    }

    private Vector2 ClampVisiblePosition(Vector2 desired)
    {
        if (currentStage == null)
        {
            return desired;
        }

        Vector2 halfField = (Vector2)currentStage.BattlefieldSize * 0.5f;
        float shakePadding = Mathf.Max(0.5f, wallDisplayScale) * (1f + wallShakeScale
            + Mathf.Sin(wallShakeAngle * Mathf.Deg2Rad));
        Vector2 visualExtents = GetVisibleWorldExtents() * shakePadding;
        Vector2 center = currentStage.transform.position;
        float minX = center.x - halfField.x + visualExtents.x;
        float maxX = center.x + halfField.x - visualExtents.x;
        float minY = center.y - halfField.y + visualExtents.y;
        float maxY = center.y + halfField.y - visualExtents.y;
        return new Vector2(minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : center.x,
            minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : center.y);
    }

    private Vector2 GetVisibleWorldExtents()
    {
        Vector2 extents = Vector2.zero;
        if (projectileRenderers == null || rendererVisibility == null)
        {
            CacheRenderers();
        }

        for (int i = 0; i < projectileRenderers.Length; i++)
        {
            SpriteRenderer renderer = projectileRenderers[i];
            if (renderer == null || !rendererVisibility[i] || renderer.sprite == null)
            {
                continue;
            }

            Vector2 local = renderer.sprite.bounds.extents;
            Vector3 scale = renderer.transform.lossyScale;
            float halfWidth = local.x * Mathf.Abs(scale.x);
            float halfHeight = local.y * Mathf.Abs(scale.y);
            float radians = renderer.transform.eulerAngles.z * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(radians));
            float sine = Mathf.Abs(Mathf.Sin(radians));
            extents.x = Mathf.Max(extents.x, cosine * halfWidth + sine * halfHeight);
            extents.y = Mathf.Max(extents.y, sine * halfWidth + cosine * halfHeight);
        }
        return extents;
    }

    private void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (body != null)
        {
            body.position = position;
        }
    }

    private void SetProjectileColliders(bool enabled)
    {
        if (projectileColliders == null)
        {
            projectileColliders = GetComponentsInChildren<Collider2D>(true);
        }

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            if (projectileColliders[i] != null)
            {
                projectileColliders[i].enabled = enabled;
            }
        }
    }

    private void CacheRenderers()
    {
        projectileRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        rendererVisibility = new bool[projectileRenderers.Length];
        for (int i = 0; i < projectileRenderers.Length; i++)
        {
            rendererVisibility[i] = projectileRenderers[i] != null
                && projectileRenderers[i].enabled;
        }
    }

    private void SetBoarVisible(bool visible)
    {
        if (projectileRenderers == null || rendererVisibility == null)
        {
            CacheRenderers();
        }

        for (int i = 0; i < projectileRenderers.Length; i++)
        {
            if (projectileRenderers[i] != null)
            {
                projectileRenderers[i].enabled = visible && rendererVisibility[i];
            }
        }
    }

    private void UpdateFacing(Vector2 direction)
    {
        if (flightVisual == null)
        {
            flightVisual = ProjectileFlightVisual.Ensure(gameObject, 0f);
        }

        flightVisual.ConfigureSpin(0f);
        flightVisual.SetFacingDirection(direction);
    }

    private void EnsurePhysics()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (flight == null)
        {
            flight = GetComponent<ProjectilePhysics2D>();
        }
    }

    private static Vector2 SafeDirection(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude > 0.000001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector2.up;
    }

    private void OnValidate()
    {
        launchSpeedMultiplier = Mathf.Max(1f, launchSpeedMultiplier);
        maximumWallMisses = Mathf.Max(1, maximumWallMisses);
        wallBouncePauseDuration = Mathf.Max(0f, wallBouncePauseDuration);
        stunChance = Mathf.Clamp01(stunChance);
        bossStunChance = Mathf.Clamp01(bossStunChance);
        wallDisplayScale = Mathf.Clamp(wallDisplayScale, 0.5f, 3f);
        wallShakeAngle = Mathf.Clamp(wallShakeAngle, 0f, 15f);
        wallShakeScale = Mathf.Clamp(wallShakeScale, 0f, 0.15f);
        visualScaleMultiplier = Mathf.Clamp(visualScaleMultiplier, 0.5f, 3f);
        damageSweepRadius = Mathf.Max(0f, damageSweepRadius);
    }
}

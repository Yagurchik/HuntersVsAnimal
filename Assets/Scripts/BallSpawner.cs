using UnityEngine;
using System.Collections.Generic;

public class BallSpawner : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private float maxRayDistance = 20f;
    [SerializeField] private float reflectionLength = 2f;

    [SerializeField] private bool useRay;
    [SerializeField] private bool useLine;
    [SerializeField] public bool useDots;

    [SerializeField] private LineRenderer line;
    private RoomDarkness roomDarkness;
    private Player owner;
    private Stage room;
    private LevelGenerator roomGenerator;
    private readonly RaycastHit2D[] aimHits = new RaycastHit2D[64];

    private struct AimPath
    {
        public bool HasImpact;
        public bool ImpactVisible;
        public Vector2 PhysicalHitPoint;
        public Vector2 HitPoint;
        public Vector2 ReflectionEnd;
        public Brick HitEnemy;
    }

    public bool LastAimHadImpact { get; private set; }
    public bool LastAimEnteredDarkness { get; private set; }
    public Vector2 LastAimHitPoint { get; private set; }
    public Brick LastAimHitEnemy { get; private set; }
    public float LastAimProjectileRadius { get; private set; }

    // Player supplies the same world-space target used by the shot.
    public void ShowAim(Vector2 worldTarget)
    {
        useDots = true;
        Vector2 origin = transform.position;
        Vector2 aimDirection = worldTarget - origin;
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            HideAim();
            return;
        }

        if (owner == null)
        {
            owner = GetComponentInParent<Player>();
        }

        aimDirection = owner != null
            ? owner.ConstrainLaunchDirection(aimDirection)
            : Player.ClampUpwardLaunchDirection(aimDirection, 30f);
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        if (roomDarkness == null)
        {
            roomDarkness = GetComponentInParent<RoomDarkness>(true);
        }

        bool fireflyVision = owner != null && owner.IsAimingWithFirefly;
        LastAimEnteredDarkness = fireflyVision && roomDarkness != null && roomDarkness.HasDarkness;
        // Search across the whole room so locally illuminated targets can be found.
        // Hidden enemies are filtered below and never leak their position through the preview.
        GameObject selectedProjectile = owner != null ? owner.SelectedProjectilePrefab : null;
        BoarProjectile selectedBoar = selectedProjectile != null
            ? selectedProjectile.GetComponent<BoarProjectile>()
            : null;
        bool isBeaver = selectedProjectile != null
            && selectedProjectile.GetComponent<BeaverProjectile>() != null;
        float projectileRadius = selectedBoar != null || isBeaver
            ? 0f : ProjectilePhysics2D.GetCollisionRadius(selectedProjectile);
        LastAimProjectileRadius = projectileRadius;
        bool isBoar = selectedBoar != null;
        AimPath centralPath = CalculateAimPath(origin, aimDirection, projectileRadius,
            isBeaver || (isBoar && selectedBoar.PassesThroughEnemies), fireflyVision, isBoar);
        bool showReflection = !isBeaver && centralPath.HasImpact && centralPath.ImpactVisible;

        LastAimHadImpact = centralPath.HasImpact;
        LastAimHitPoint = centralPath.PhysicalHitPoint;
        LastAimHitEnemy = centralPath.HitEnemy;

        if (useRay)
        {
            Debug.DrawRay(origin, centralPath.HitPoint - origin, Color.yellow);
            if (showReflection)
            {
                Debug.DrawRay(centralPath.HitPoint,
                    centralPath.ReflectionEnd - centralPath.HitPoint, Color.green);
            }
        }

        UpdateLine(origin, centralPath.HitPoint, centralPath.ReflectionEnd, showReflection);

        if (useDots && Dots.instance != null)
        {
            if (owner != null && owner.IsAimingWithBoarUltimate)
            {
                var paths = new List<Dots.TrajectoryPath>(3);
                AddAimPath(paths, origin, centralPath);
                Vector2 leftDirection = RotateDirection(aimDirection,
                    -Player.BoarUltimateSpreadAngle);
                Vector2 rightDirection = RotateDirection(aimDirection,
                    Player.BoarUltimateSpreadAngle);
                AddAimPath(paths, origin, CalculateAimPath(origin, leftDirection, 0f,
                    true, fireflyVision, true));
                AddAimPath(paths, origin, CalculateAimPath(origin, rightDirection, 0f,
                    true, fireflyVision, true));
                Dots.instance.DrawTrajectories(this, paths, centralPath.HitPoint,
                    centralPath.ImpactVisible);
            }
            else
            {
                Dots.instance.DrawTrajectory(this, transform.position, centralPath.HitPoint,
                    centralPath.ReflectionEnd, showReflection, centralPath.ImpactVisible);
            }
        }
    }

    private AimPath CalculateAimPath(Vector2 origin, Vector2 direction,
        float projectileRadius, bool ignoreEnemies, bool fireflyVision, bool rawReflection)
    {
        Vector2 rayEnd = origin + direction * maxRayDistance;
        bool hasImpact = TryFindFirstImpact(origin, projectileRadius, ignoreEnemies, direction,
            Vector2.Distance(origin, rayEnd), out RaycastHit2D hit, out Brick hitEnemy);
        Vector2 physicalHitPoint = hasImpact
            ? (projectileRadius > 0.0001f ? hit.centroid : hit.point) : rayEnd;
        bool impactVisible = hasImpact && (roomDarkness == null || fireflyVision
            || roomDarkness.IsVisible(physicalHitPoint));
        Vector2 hitPoint = physicalHitPoint;
        if (roomDarkness != null && !fireflyVision && !roomDarkness.IsVisible(hitPoint))
        {
            hitPoint = roomDarkness.ClipToVisibleArea(origin, hitPoint);
        }

        Vector2 reflectionEnd = hitPoint;
        if (hasImpact && impactVisible)
        {
            Vector2 reflectionNormal = hit.normal;
            Vector2 reflectedDirection;
            bool mirageReflection = ProjectilePhysics2D.TryGetMirageReturnReflection(
                hitEnemy, hit.collider, direction, out Vector2 returnDirection);
            if (mirageReflection)
            {
                reflectedDirection = returnDirection;
                reflectionNormal = returnDirection;
            }
            else
            {
                if (hitEnemy != null && !ProjectilePhysics2D
                    .IsOctagonalBossCollider(hitEnemy, hit.collider))
                {
                    reflectionNormal = ProjectilePhysics2D
                        .StabilizeEnemyReflectionNormal(direction, hit.normal);
                }

                reflectedDirection = ProjectilePhysics2D
                    .ReflectDirection(direction, reflectionNormal);
            }
            if (!rawReflection && !mirageReflection)
            {
                reflectedDirection = ProjectilePhysics2D.KeepVerticalMotion(reflectedDirection,
                    ProjectilePhysics2D.DefaultMinimumRicochetVerticalAngle,
                    direction.y);
            }
            reflectionEnd = hitPoint + reflectedDirection * reflectionLength;
            if (roomDarkness != null && !fireflyVision)
            {
                reflectionEnd = roomDarkness.ClipToVisibleArea(hitPoint, reflectionEnd);
            }
        }
        return new AimPath
        {
            HasImpact = hasImpact,
            ImpactVisible = impactVisible,
            PhysicalHitPoint = physicalHitPoint,
            HitPoint = hitPoint,
            ReflectionEnd = reflectionEnd,
            HitEnemy = hitEnemy
        };
    }

    private static void AddAimPath(List<Dots.TrajectoryPath> paths, Vector2 origin,
        AimPath path)
    {
        paths.Add(new Dots.TrajectoryPath(origin, path.HitPoint, path.ReflectionEnd,
            path.HasImpact && path.ImpactVisible));
    }

    private static Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine).normalized;
    }

    private void OnDisable()
    {
        HideAim(true);
    }

    private void UpdateLine(
        Vector2 origin,
        Vector2 hitPoint,
        Vector2 reflectionEnd,
        bool hasReflection)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = useLine;
        if (!useLine)
        {
            return;
        }

        line.useWorldSpace = true;
        line.positionCount = hasReflection ? 3 : 2;
        float z = transform.position.z;
        line.SetPosition(0, new Vector3(origin.x, origin.y, z));
        line.SetPosition(1, new Vector3(hitPoint.x, hitPoint.y, z));
        if (hasReflection)
        {
            line.SetPosition(2, new Vector3(reflectionEnd.x, reflectionEnd.y, z));
        }
    }

    public void HideAim(bool immediate = false)
    {
        useDots = false;
        LastAimHadImpact = false;
        LastAimEnteredDarkness = false;
        LastAimHitEnemy = null;
        if (line != null)
        {
            line.enabled = false;
        }

        if (Dots.instance != null)
        {
            Dots.instance.Hide(this, immediate);
        }
    }

    private bool TryFindFirstImpact(Vector2 origin, float projectileRadius,
        bool ignoreEnemies, Vector2 direction, float distance,
        out RaycastHit2D closest, out Brick hitEnemy)
    {
        closest = default;
        hitEnemy = null;
        if (room == null)
        {
            room = GetComponentInParent<Stage>();
            if (room != null)
            {
                roomGenerator = room.GetComponentInChildren<LevelGenerator>(true);
            }
        }

        int enemyLayer = LayerMask.NameToLayer("enemy");
        int combinedMask = layerMask.value | (enemyLayer >= 0 ? 1 << enemyLayer : 0);
        int hitCount = projectileRadius > 0.0001f
            ? Physics2D.CircleCastNonAlloc(origin, projectileRadius, direction,
                aimHits, distance, combinedMask)
            : Physics2D.RaycastNonAlloc(origin, direction, aimHits, distance, combinedMask);
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D candidate = aimHits[i];
            if (candidate.collider == null)
            {
                continue;
            }

            Brick candidateEnemy = candidate.collider.GetComponentInParent<Brick>();
            if (candidateEnemy != null)
            {
                if (ignoreEnemies && candidateEnemy.GetComponent<MirageCornerMirror>() == null)
                {
                    continue;
                }

                if (candidateEnemy.IsDefeated || candidateEnemy.levelGenerator != roomGenerator)
                {
                    continue;
                }

                RoomDarkness enemyDarkness = candidateEnemy.levelGenerator.Darkness;
                if (enemyDarkness != null
                    && !enemyDarkness.IsVisible(candidateEnemy.transform.position))
                {
                    continue;
                }
            }
            else
            {
                // Only accept the authored wall layers belonging to this room.
                if ((layerMask.value & (1 << candidate.collider.gameObject.layer)) == 0)
                {
                    continue;
                }

                Stage candidateRoom = candidate.collider.GetComponentInParent<Stage>();
                if (room != null && candidateRoom != room)
                {
                    continue;
                }
            }
            if (candidate.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = candidate.distance;
            closest = candidate;
            hitEnemy = candidateEnemy;
        }
        return closest.collider != null;
    }
}

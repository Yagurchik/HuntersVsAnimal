using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ProjectilePhysics2D : MonoBehaviour
{
    public const float DefaultMinimumRicochetVerticalAngle = 5f;

    [Header("Stable flight")]
    [SerializeField] private bool preserveLaunchSpeed = true;
    [SerializeField, Min(0f)] private float collisionSeparation = 0.015f;
    [SerializeField, Range(0.001f, 0.25f)] private float stoppedSpeedRatio = 0.05f;
    [SerializeField, Range(0f, 20f), Tooltip("Prevents endless nearly-horizontal wall-to-wall ricochets.")]
    private float minimumRicochetVerticalAngle = DefaultMinimumRicochetVerticalAngle;

    private Rigidbody2D body;
    private Vector2 lastDirection = Vector2.up;
    private float targetSpeed;
    private bool flying;
    private bool preserveExactReflectionDirection;
    private bool restoreScriptedReflectionNextStep;
    private float lastReflectionFixedTime = float.NegativeInfinity;

    public bool IsFlying => flying;
    public Vector2 FlightDirection => lastDirection;
    public float TargetSpeed => targetSpeed;
    public float MinimumRicochetVerticalAngle => Mathf.Clamp(minimumRicochetVerticalAngle, 0f, 20f);

    public static float GetCollisionRadius(GameObject projectilePrefab)
    {
        if (projectilePrefab == null) return 0f;

        CircleCollider2D[] colliders = projectilePrefab.GetComponentsInChildren<CircleCollider2D>(true);
        float largestRadius = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            CircleCollider2D collider = colliders[i];
            if (collider == null || collider.isTrigger) continue;

            Vector3 scale = collider.transform.lossyScale;
            float worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            largestRadius = Mathf.Max(largestRadius, collider.radius * worldScale);
        }
        return largestRadius;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ConfigureBody();
    }

    private void OnEnable()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        ConfigureBody();
    }

    private void OnValidate()
    {
        stoppedSpeedRatio = Mathf.Clamp(stoppedSpeedRatio, 0.001f, 0.25f);
        collisionSeparation = Mathf.Max(0f, collisionSeparation);
        minimumRicochetVerticalAngle = Mathf.Clamp(minimumRicochetVerticalAngle, 0f, 20f);
        if (!Application.isPlaying)
        {
            body = GetComponent<Rigidbody2D>();
            ConfigureBody();
        }
    }

    public void Launch(Vector2 direction, float speed)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        ConfigureBody();

        targetSpeed = Mathf.Max(0.1f, speed);
        lastDirection = KeepVerticalMotion(SafeDirection(direction, lastDirection),
            MinimumRicochetVerticalAngle, direction.y);
        lastReflectionFixedTime = float.NegativeInfinity;
        preserveExactReflectionDirection = false;
        restoreScriptedReflectionNextStep = false;
        flying = true;
        body.velocity = lastDirection * targetSpeed;
    }

    public void ReturnTo(Vector2 target, float speed)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        ConfigureBody();

        flying = false;
        preserveExactReflectionDirection = false;
        restoreScriptedReflectionNextStep = false;
        targetSpeed = Mathf.Max(0.1f, speed);
        lastDirection = SafeDirection(target - body.position, Vector2.down);
        body.velocity = lastDirection * targetSpeed;
    }

    public void Redirect(Vector2 direction)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body == null) return;
        float speed = targetSpeed > 0.1f ? targetSpeed : Mathf.Max(0.1f, body.velocity.magnitude);
        lastDirection = KeepVerticalMotion(SafeDirection(direction, lastDirection),
            MinimumRicochetVerticalAngle, direction.y);
        targetSpeed = speed;
        preserveExactReflectionDirection = false;
        restoreScriptedReflectionNextStep = false;
        flying = true;
        body.velocity = lastDirection * targetSpeed;
    }

    // Ghost contact may arrive after the physics solver has already changed velocity.
    // Restore the pre-contact trajectory without treating it as a ricochet.
    public void ContinueThroughEnemy()
    {
        if (!flying || body == null || !body.simulated) return;
        body.velocity = lastDirection * targetSpeed;
        body.angularVelocity = 0f;
        preserveExactReflectionDirection = false;
        restoreScriptedReflectionNextStep = false;
    }

    public void StopFlight()
    {
        flying = false;
        preserveExactReflectionDirection = false;
        restoreScriptedReflectionNextStep = false;
    }

    private void FixedUpdate()
    {
        if (!flying || body == null || !body.simulated || !preserveLaunchSpeed) return;

        Vector2 velocity = body.velocity;
        float stoppedThreshold = targetSpeed * stoppedSpeedRatio;
        Vector2 direction = restoreScriptedReflectionNextStep
            ? lastDirection
            : velocity.sqrMagnitude > stoppedThreshold * stoppedThreshold
                ? velocity.normalized : lastDirection;
        restoreScriptedReflectionNextStep = false;

        lastDirection = preserveExactReflectionDirection
            ? SafeDirection(direction, lastDirection)
            : KeepVerticalMotion(SafeDirection(direction, lastDirection),
                MinimumRicochetVerticalAngle, lastDirection.y);
        body.velocity = lastDirection * targetSpeed;
        body.angularVelocity = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!flying || body == null || !body.simulated || collision.contactCount == 0) return;
        GhostPhasing ghost = collision.gameObject.GetComponentInParent<GhostPhasing>();
        if (ghost != null && ghost.TryPassThrough(GetComponent<Animal>()))
        {
            ContinueThroughEnemy();
            return;
        }
        BoarProjectile boar = GetComponent<BoarProjectile>();
        if (boar != null && boar.IsEnemyCollision(collision))
        {
            restoreScriptedReflectionNextStep = false;
            body.velocity = lastDirection * targetSpeed;
            body.angularVelocity = 0f;
            return;
        }
        // A wide projectile can touch two neighboring bricks during one physics
        // step. Reflect only once so two callbacks cannot undo or twist the bounce.
        if (lastReflectionFixedTime == Time.fixedTime) return;
        lastReflectionFixedTime = Time.fixedTime;

        // Rigidbody2D may already have applied its material response by the time
        // this callback runs, so use the direction captured before simulation.
        Vector2 incoming = SafeDirection(lastDirection, body.velocity);
        Vector2 normal = BestReflectionNormal(collision, incoming);
        Vector2 reflected;
        bool mirageReflection = TryGetMirageReturnReflection(collision, incoming,
            out Vector2 returnDirection);
        if (mirageReflection)
        {
            reflected = returnDirection;
            normal = returnDirection;
        }
        else
        {
            if (IsEnemyCollision(collision) && !HasOctagonalBossCollider(collision))
                normal = StabilizeEnemyReflectionNormal(incoming, normal);
            reflected = ReflectDirection(incoming, normal);
        }
        // A mathematically horizontal bounce keeps the incoming vertical sign
        // when adding the minimum angle; the bevel must not force it downward.
        if (!mirageReflection)
            reflected = KeepVerticalMotion(reflected, MinimumRicochetVerticalAngle,
                incoming.y);
        preserveExactReflectionDirection = mirageReflection;

        if (boar != null && boar.TryBeginWallBouncePause(collision, reflected))
        {
            lastDirection = reflected;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            if (collisionSeparation > 0f)
                body.position += normal * collisionSeparation;
            return;
        }

        lastDirection = reflected;
        restoreScriptedReflectionNextStep = mirageReflection;
        body.velocity = reflected * targetSpeed;
        body.angularVelocity = 0f;
        if (collisionSeparation > 0f)
            body.position += normal * collisionSeparation;
    }

    public static Vector2 ReflectDirection(Vector2 incoming, Vector2 normal)
    {
        incoming = SafeDirection(incoming, Vector2.up);
        normal = SafeDirection(normal, -incoming);
        Vector2 reflected = Vector2.Reflect(incoming, normal);
        return SafeDirection(reflected, -incoming);
    }

    // Circle-vs-box corner contacts produce a diagonal normal whose angle changes
    // with tiny differences in the hit point. Enemies occupy axis-aligned grid
    // cells, so resolve such contacts as a hit on one deterministic box face.
    // The dominant incoming component selects the face; an exact tie uses the
    // horizontal face (vertical normal), keeping identical shots reproducible.
    public static Vector2 StabilizeEnemyReflectionNormal(Vector2 incoming, Vector2 normal)
    {
        incoming = SafeDirection(incoming, Vector2.up);
        normal = SafeDirection(normal, -incoming);

        const float axisEpsilon = 0.001f;
        if (Mathf.Abs(normal.x) <= axisEpsilon || Mathf.Abs(normal.y) <= axisEpsilon)
            return normal;

        if (Mathf.Abs(incoming.x) > Mathf.Abs(incoming.y))
            return new Vector2(normal.x >= 0f ? 1f : -1f, 0f);

        return new Vector2(0f, normal.y >= 0f ? 1f : -1f);
    }

    public static Vector2 ReturnToSenderDirection(Vector2 incoming)
    {
        return -SafeDirection(incoming, Vector2.up);
    }

    public static bool TryGetMirageReturnReflection(Brick enemy, Collider2D enemyCollider,
        Vector2 incoming, out Vector2 reflectedDirection)
    {
        reflectedDirection = Vector2.zero;
        if (enemy == null || enemy.GetComponent<MirageCornerMirror>() == null
            || enemyCollider == null)
            return false;
        reflectedDirection = ReturnToSenderDirection(incoming);
        return true;
    }

    public static Vector2 KeepVerticalMotion(Vector2 direction, float minimumAngleDegrees,
        float preferredVerticalSign = 1f)
    {
        direction = SafeDirection(direction, Vector2.up);
        float angle = Mathf.Clamp(minimumAngleDegrees, 0f, 89f) * Mathf.Deg2Rad;
        float minimumY = Mathf.Sin(angle);
        if (Mathf.Abs(direction.y) >= minimumY) return direction;

        float verticalSign = Mathf.Abs(direction.y) > 0.0001f
            ? Mathf.Sign(direction.y)
            : Mathf.Abs(preferredVerticalSign) > 0.0001f ? Mathf.Sign(preferredVerticalSign) : 1f;
        float horizontalSign = Mathf.Abs(direction.x) > 0.0001f ? Mathf.Sign(direction.x) : 1f;
        return new Vector2(horizontalSign * Mathf.Cos(angle), verticalSign * minimumY).normalized;
    }

    private static Vector2 BestReflectionNormal(Collision2D collision, Vector2 incoming)
    {
        Vector2 normal = collision.GetContact(0).normal;
        float strongestOpposition = Vector2.Dot(incoming, normal);
        for (int i = 1; i < collision.contactCount; i++)
        {
            Vector2 candidate = collision.GetContact(i).normal;
            float opposition = Vector2.Dot(incoming, candidate);
            if (opposition < strongestOpposition)
            {
                strongestOpposition = opposition;
                normal = candidate;
            }
        }
        return SafeDirection(normal, -incoming);
    }

    private static bool IsEnemyCollision(Collision2D collision)
    {
        if (collision == null) return false;
        return collision.gameObject.GetComponentInParent<Brick>() != null
            || (collision.collider != null
                && collision.collider.GetComponentInParent<Brick>() != null)
            || (collision.otherCollider != null
                && collision.otherCollider.GetComponentInParent<Brick>() != null);
    }

    public static bool IsOctagonalBossCollider(Brick enemy, Collider2D collider)
    {
        return IsBoss(enemy) && collider is PolygonCollider2D;
    }

    private static bool HasOctagonalBossCollider(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (IsOctagonalBossCollider(contact.collider != null
                    ? contact.collider.GetComponentInParent<Brick>() : null, contact.collider)
                || IsOctagonalBossCollider(contact.otherCollider != null
                    ? contact.otherCollider.GetComponentInParent<Brick>() : null, contact.otherCollider))
                return true;
        }
        return false;
    }

    private static bool TryGetMirageReturnReflection(Collision2D collision, Vector2 incoming,
        out Vector2 reflectedDirection)
    {
        reflectedDirection = Vector2.zero;
        if (collision == null) return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (TryGetMirageReturnReflectionFromCollider(contact.collider, incoming,
                out reflectedDirection)
                || TryGetMirageReturnReflectionFromCollider(contact.otherCollider, incoming,
                    out reflectedDirection))
                return true;
        }
        return false;
    }

    private static bool TryGetMirageReturnReflectionFromCollider(Collider2D collider,
        Vector2 incoming, out Vector2 reflectedDirection)
    {
        reflectedDirection = Vector2.zero;
        if (collider == null) return false;
        Brick enemy = collider.GetComponentInParent<Brick>();
        return TryGetMirageReturnReflection(enemy, collider, incoming,
            out reflectedDirection);
    }

    private static bool IsBoss(Brick enemy)
    {
        return enemy != null && (enemy.GetComponent<BossEnemyTurnController>() != null
            || enemy.Footprint.x > 1 || enemy.Footprint.y > 1);
    }

    private static Vector2 SafeDirection(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude > 0.000001f) return direction.normalized;
        return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector2.up;
    }

    private void ConfigureBody()
    {
        if (body == null) return;
        body.gravityScale = 0f;
        body.drag = 0f;
        body.angularDrag = 0f;
        body.angularVelocity = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
    }
}

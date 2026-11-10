using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(950)]
public sealed class FrogRider : MonoBehaviour
{
    public const int MaximumPerEnemy = 4;
    public const int MaximumJumps = 5;
    private static readonly List<FrogRider> All = new List<FrogRider>();
    private sealed class JumpReaction
    {
        public readonly int VolleyId;
        public JumpReaction(int id) { VolleyId = id; }
    }
    private struct SeatBite
    {
        public Brick Enemy;
        public JumpReaction Reaction;
    }
    private static readonly Queue<SeatBite> PendingSeatBites = new Queue<SeatBite>();
    private static JumpReaction damageReaction;
    private static bool processingSeatBites;
    private static readonly Dictionary<LevelGenerator, float> RoomChorus = new Dictionary<LevelGenerator, float>();
    private static int chorusFrame = -1;
    public static void InvalidateRoomBonuses() => chorusFrame = -1;
    [SerializeField] private SpriteRenderer frogVisual;
    [SerializeField] private LineRenderer kvaRing;
    [SerializeField] private TrailRenderer jumpTrail;
    [SerializeField, Min(0.01f)] private float jumpSeconds = 0.1f;
    [SerializeField, Min(0f)] private float jumpHeight = 0.28f;
    [SerializeField, Min(1f)] private float fallSpeed = 7f;

    private Brick host;
    private LevelGenerator room;
    private float baseDamage = 15f;
    private float jumpRadius = 2f;
    private int chorusRank;
    private int volleyId;
    private int seatSlot;
    private int dislodgementCount;
    private int dislodgementVolleyId;
    private JumpReaction reaction;
    private bool ultimate;
    private bool seated;
    private bool jumping;
    private bool falling;
    private bool initialized;
    private bool resolvingLanding;
    private int lastTickTurn = -1;
    private float pulseUntil;
    private Vector3 pulseOrigin;
    private Transform seatAnchor;
    private Vector3 seatLocalOffset;
    private Vector3 seatAnchorScale;
    private Vector3 frogRestScale;
    private float trailExpiresAt;

    public Brick Host => host;
    public bool IsSeated => seated;
    public bool IsJumping => jumping;
    public bool IsFalling => falling;
    public LevelGenerator Room => host != null && host.levelGenerator != null ? host.levelGenerator : room;
    public float BaseDamage => baseDamage;
    public float JumpRadius => jumpRadius;
    public int DislodgementCount => dislodgementCount;

    public static bool HasActiveJumps
    {
        get { foreach (FrogRider frog in All) { if (frog != null && frog.jumping) { return true; } } return false; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        All.Clear();
        RoomChorus.Clear(); chorusFrame = -1;
        PendingSeatBites.Clear();
        processingSeatBites = false;
        damageReaction = null;
    }

    public static void BeginVolley(int id)
    {
        foreach (FrogRider frog in All)
        {
            if (frog == null)
            {
                continue;
            }

            frog.dislodgementCount = 0;
            frog.dislodgementVolleyId = id;
        }
    }

    private void Awake()
    {
        if (frogVisual == null)
        {
            frogVisual = GetComponent<SpriteRenderer>();
        }

        Sprite skin = ProjectileSkins.EquippedArtwork(AnimalProjectileQueue.FrogIndex);
        if (skin != null && frogVisual != null && frogVisual.sprite != null)
        {
            Vector2 previousSize = frogVisual.sprite.bounds.size;
            Vector2 skinSize = skin.bounds.size;
            frogVisual.transform.localScale = Vector3.Scale(frogVisual.transform.localScale,
                new Vector3(previousSize.x / Mathf.Max(0.001f, skinSize.x),
                    previousSize.y / Mathf.Max(0.001f, skinSize.y), 1f));
            frogVisual.sprite = skin;
            frogVisual.color = Color.white;
        }
        if (frogVisual != null)
        {
            frogRestScale = frogVisual.transform.localScale;
        }

        if (kvaRing != null)
        {
            kvaRing.enabled = false;
        }

        if (jumpTrail != null)
        {
            jumpTrail.emitting = false;
        }
    }

    public static int CountOn(Brick enemy)
    {
        int count = 0;
        foreach (FrogRider frog in All)
        {
            if (frog != null && !frog.falling && frog.host == enemy)
            {
                count++;
            }
        }

        return count;
    }

    public static float DamageMultiplier(LevelGenerator targetRoom)
    {
        if (targetRoom == null)
        {
            return 1f;
        }

        if (chorusFrame != Time.frameCount)
        {
            RoomChorus.Clear();
            foreach (FrogRider frog in All)
            {
                if (frog == null || frog.falling)
                {
                    continue;
                }

                var owner = frog.Room;
                if (owner == null)
                {
                    continue;
                }

                RoomChorus.TryGetValue(owner, out float percent);
                RoomChorus[owner] = percent + frog.chorusRank;
            }
            chorusFrame = Time.frameCount;
        }
        return 1f + (RoomChorus.TryGetValue(targetRoom, out float bonus) ? bonus * 0.01f : 0f);
    }

    public static void UpgradeAll(float radius, int supportRank, bool hasUltimate)
    {
        InvalidateRoomBonuses();
        foreach (FrogRider frog in All)
        {
            if (frog == null)
            {
                continue;
            }

            frog.jumpRadius = Mathf.Max(2f, radius);
            frog.chorusRank = Mathf.Clamp(supportRank, 0, 3);
            frog.ultimate = hasUltimate;
        }
    }

    public void Initialize(Brick firstTarget, float damage, float radius,
        int supportRank, bool hasUltimate, int comboVolleyId)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        baseDamage = Mathf.Max(0f, damage);
        jumpRadius = Mathf.Max(2f, radius);
        chorusRank = Mathf.Clamp(supportRank, 0, 3);
        ultimate = hasUltimate;
        volleyId = comboVolleyId;
        reaction = new JumpReaction(comboVolleyId);
        room = firstTarget != null ? firstTarget.levelGenerator : null;
        // Reserve the seat before the delayed landing damage to cap simultaneous shots.
        if (firstTarget == null || firstTarget.IsDefeated || CountOn(firstTarget) >= MaximumPerEnemy)
        { BeginFall(); return; }
        host = firstTarget;
        seatSlot = FindFreeSlot(firstTarget);
        All.Add(this);
        InvalidateRoomBonuses();
        jumping = true;
        StartCoroutine(InitialLanding(firstTarget));
    }

    private IEnumerator InitialLanding(Brick target)
    {
        yield return null;
        if (target == null || target.IsDefeated || Room == null || Room.IsLost)
        { BeginFall(); yield break; }
        Vector3 origin = target.transform.position;
        DealDamage(target, baseDamage, false, reaction);
        if (CanSit(target) && Sit(target))
        {
            yield break;
        }

        host = null;
        yield return JumpChain(origin, target, NextJumpCount(volleyId), reaction);
    }

    public static void NotifyEnemyDamaged(Brick enemy, int comboVolleyId = -1)
    {
        foreach (FrogRider frog in All)
        {
            if (frog == null || !frog.seated || frog.host != enemy)
            {
                continue;
            }

            frog.JumpFromHost(new JumpReaction(ResolveVolleyId(comboVolleyId)));
            return;
        }
    }

    public static void NotifyEnemyDefeated(Brick enemy, int comboVolleyId = -1)
    {
        JumpReaction cause = damageReaction ?? new JumpReaction(ResolveVolleyId(comboVolleyId));
        var snapshot = CombatSnapshotPool<FrogRider>.Rent(); snapshot.AddRange(All);
        try { foreach (FrogRider frog in snapshot)
            {
                if (frog != null && frog.seated && frog.host == enemy && !frog.resolvingLanding)
                {
                    frog.JumpFromHost(cause);
                }
            }
        }
        finally { CombatSnapshotPool<FrogRider>.Return(snapshot); }
    }

    private static int ResolveVolleyId(int id) => id >= 0 ? id : Player.CurrentVolleyId;

    private static void TriggerSeatedBites(Brick enemy, JumpReaction cause = null)
    {
        if (enemy == null || enemy.IsDefeated)
        {
            return;
        }

        PendingSeatBites.Enqueue(new SeatBite { Enemy = enemy, Reaction = cause ?? new JumpReaction(0) });
        if (processingSeatBites)
        {
            return;
        }

        processingSeatBites = true;
        try
        {
            while (PendingSeatBites.Count > 0)
            {
                SeatBite bite = PendingSeatBites.Dequeue();
                Brick target = bite.Enemy;
                if (target == null || target.IsDefeated)
                {
                    continue;
                }
                // Airborne reservations enforce capacity but are not seated frogs.
                // Snapshot before damage: KVA can kill hosts and release their riders.
                var snapshot = CombatSnapshotPool<FrogRider>.Rent(); snapshot.AddRange(All);
                try { foreach (FrogRider frog in snapshot)
                    {
                        if (frog != null && frog.seated && frog.host == target && !target.IsDefeated)
                        {
                            frog.DealDamage(target, frog.baseDamage, false, bite.Reaction);
                        }
                    }
                }
                finally { CombatSnapshotPool<FrogRider>.Return(snapshot); }
            }
        }
        finally { processingSeatBites = false; }
    }

    private void JumpFromHost(JumpReaction cause)
    {
        if (!seated || jumping || host == null)
        {
            return;
        }

        Vector3 origin = host.transform.position;
        Brick previous = LeaveSeat();
        reaction = cause;
        volleyId = cause.VolleyId;
        if (jumpTrail != null)
        {
            jumpTrail.Clear();
        }

        StartCoroutine(JumpChain(origin, previous, NextJumpCount(cause.VolleyId), cause));
        TriggerSeatedBites(previous, cause);
    }

    private int NextJumpCount(int id)
    {
        // Off-volley damage starts a fresh one-hop reaction, never a lifetime streak.
        if (id <= 0 || id != dislodgementVolleyId)
        {
            dislodgementCount = 0;
        }

        dislodgementVolleyId = id;
        dislodgementCount = Mathf.Min(MaximumJumps, dislodgementCount + 1);
        return dislodgementCount;
    }

    private Brick LeaveSeat()
    {
        Brick previous = host;
        room = Room;
        UpdateSeatPosition();
        seated = false;
        host = null;
        seatAnchor = null;
        if (frogVisual != null)
        {
            frogVisual.transform.localScale = frogRestScale;
        }

        jumping = true;
        InvalidateRoomBonuses();
        return previous;
    }

    private IEnumerator JumpChain(Vector3 origin, Brick previous, int jumpCount, JumpReaction cause)
    {
        // A damage callback may still be completing enemy death and collection removal.
        yield return null;
        var excluded = new HashSet<Brick>();
        if (previous != null)
        {
            excluded.Add(previous);
        }

        Brick nextTarget = null;
        float damage = baseDamage;
        for (int hop = 0; hop < MaximumJumps; hop++)
        {
            Brick target = nextTarget != null && !nextTarget.IsDefeated
                ? nextTarget : FindTarget(room, origin, excluded);
            nextTarget = null;
            if (target == null)
            {
                break;
            }
            // Only the enemy being left is excluded. Earlier enemies remain
            // valid random targets, even when just two enemies are nearby.
            excluded.Clear(); excluded.Add(target);
            damage *= 2f;
            // Reserve landing seats while airborne to enforce the four-frog cap.
            bool fullBeforeHit = CountOn(target) >= MaximumPerEnemy;
            if (!fullBeforeHit) { seatSlot = FindFreeSlot(target); host = target; }
            yield return TravelTo(target);
            origin = target != null ? target.transform.position : transform.position;
            if (target == null || target.IsDefeated) { host = null; continue; }
            DealDamage(target, damage, false, cause);
            bool landed = CanSit(target) && Sit(target, false);
            // A full target still receives every resident's bite on arrival,
            // while the incoming frog continues searching for its own seat.
            if (!landed)
            {
                TriggerSeatedBites(target, cause);
            }

            if (landed)
            {
                if (hop + 1 < jumpCount)
                {
                    nextTarget = FindTarget(room, origin, excluded);
                }

                if (hop + 1 >= jumpCount || nextTarget == null)
                {
                    CompleteJump();
                    yield break;
                }
                // An intermediate landing keeps the same chain and doubled damage.
                // Seat changes still trigger the existing chorus bites.
                Brick departed = LeaveSeat();
                TriggerSeatedBites(departed, cause);
            }
            host = null;
        }
        BeginFall();
    }

    public Brick FindTarget(LevelGenerator targetRoom, Vector2 origin, HashSet<Brick> excluded = null)
    {
        if (targetRoom == null || targetRoom.IsLost)
        {
            return null;
        }

        Brick selected = null;
        int candidateCount = 0;
        foreach (GameObject value in targetRoom.EnemyCollection)
        {
            if (value == null || !value.TryGetComponent(out Brick enemy) || enemy.IsDefeated
                || (excluded != null && excluded.Contains(enemy)))
            {
                continue;
            }

            Vector2 delta = (Vector2)enemy.transform.position - origin;
            float x = Mathf.Abs(delta.x), y = Mathf.Abs(delta.y);
            if (enemy.UsesBossHealth)
            {
                x = Mathf.Max(0f, x - (enemy.Footprint.x - 1) * 0.5f);
                y = Mathf.Max(0f, y - (enemy.Footprint.y - 1) * 0.5f);
            }
            if (Mathf.Max(x, y) > jumpRadius + 0.0001f)
            {
                continue;
            }
            // Every eligible enemy has equal probability, regardless of its
            // health, distance within the radius, or number of seated frogs.
            candidateCount++;
            if (Random.Range(0, candidateCount) == 0)
            {
                selected = enemy;
            }
        }
        return selected;
    }

    private IEnumerator TravelTo(Brick target)
    {
        Vector3 start = transform.position;
        if (jumpTrail != null)
        {
            trailExpiresAt = 0f;
            jumpTrail.emitting = true;
            jumpTrail.AddPosition(start);
        }
        float elapsed = 0f;
        while (elapsed < jumpSeconds && target != null && !target.IsDefeated)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpSeconds);
            transform.position = Vector3.Lerp(start, target.transform.position, t)
                + Vector3.up * (Mathf.Sin(t * Mathf.PI) * jumpHeight);
            yield return null;
        }
        if (target != null)
        {
            transform.position = target.transform.position;
        }

        StopJumpTrail();
    }

    private void StopJumpTrail()
    {
        if (jumpTrail == null)
        {
            return;
        }

        jumpTrail.emitting = false;
        trailExpiresAt = Time.time + jumpTrail.time;
    }

    private bool CanSit(Brick target)
    {
        return target != null && !target.IsDefeated && target.levelGenerator != null
            && !target.levelGenerator.IsLost
            && CountOn(target) < MaximumPerEnemy + (host == target ? 1 : 0);
    }

    private bool Sit(Brick target, bool completeChain = true)
    {
        InvalidateRoomBonuses();
        if (host != target)
        {
            seatSlot = FindFreeSlot(target);
        }

        host = target;
        room = target.levelGenerator;
        seated = true;
        CacheSeatAnchor();
        UpdateSeatPosition();
        resolvingLanding = true;
        try { TriggerSeatedBites(target, reaction); }
        finally { resolvingLanding = false; }
        if (target == null || target.IsDefeated)
        {
            // Continue the current chain when the arrival bite kills the host;
            // it must not reset the doubled damage or the five-hop limit.
            seated = false;
            jumping = true;
            host = null;
            seatAnchor = null;
            if (frogVisual != null)
            {
                frogVisual.transform.localScale = frogRestScale;
            }

            return false;
        }
        // Keep the turn occupied until arrival bites and their death callbacks finish.
        if (completeChain)
        {
            CompleteJump();
        }

        return true;
    }

    private void CompleteJump()
    {
        jumping = false;
        reaction = null;
        NotifyJumpResolved();
    }

    private void DealDamage(Brick target, float damage, bool periodic, JumpReaction cause = null)
    {
        if (target == null || target.IsDefeated)
        {
            return;
        }

        LevelGenerator attackRoom = target.levelGenerator;
        Vector2 center = target.transform.position;
        var victims = CombatSnapshotPool<Brick>.Rent(); victims.Add(target);
        if (ultimate && attackRoom != null)
        {
            foreach (GameObject value in attackRoom.EnemyCollection)
            {
                if (value != null && value.TryGetComponent(out Brick enemy)
                    && enemy != target && !enemy.IsDefeated
                    && Vector2.SqrMagnitude((Vector2)enemy.transform.position - center) <= 1.0001f)
                {
                    victims.Add(enemy);
                }
            }

            PulseKva(center);
        }
        JumpReaction previousReaction = damageReaction;
        damageReaction = cause ?? new JumpReaction(periodic ? 0 : volleyId);
        try { foreach (Brick enemy in victims)
        {
            if (enemy == null || enemy.IsDefeated)
                {
                    continue;
                }

                if (!periodic)
                {
                    enemy.PlayProjectileImpact(enemy.transform.position, Vector2.up);
                }

                enemy.TakeFrogDamage(damage, damageReaction.VolleyId, periodic, enemy == target);
        } }
        finally { damageReaction = previousReaction; CombatSnapshotPool<Brick>.Return(victims); }
    }

    public static void ProcessAllAfterEnemyTurn(int turnNumber)
    {
        var snapshot = CombatSnapshotPool<FrogRider>.Rent(); snapshot.AddRange(All);
        try { foreach (FrogRider frog in snapshot)
        {
            if (frog == null || !frog.seated || frog.host == null || frog.host.IsDefeated
                || turnNumber <= frog.lastTickTurn)
                {
                    continue;
                }

                frog.lastTickTurn = turnNumber;
            // The frog's own periodic bite doesn't make the riders jump off their host.
            frog.DealDamage(frog.host, frog.baseDamage, true);
        } }
        finally { CombatSnapshotPool<FrogRider>.Return(snapshot); }
    }

    private void PulseKva(Vector3 center)
    {
        pulseOrigin = center;
        pulseUntil = Time.time + 0.22f;
        if (kvaRing != null)
        {
            kvaRing.enabled = true;
        }
    }

    private void CacheSeatAnchor()
    {
        if (host == null)
        {
            return;
        }

        Transform visual = host.GetComponent<EnemyHitReaction>()?.VisualRoot;
        SpriteRenderer body = visual != null ? visual.GetComponentInChildren<SpriteRenderer>()
            : host.GetComponentInChildren<SpriteRenderer>();
        seatAnchor = body != null ? body.transform : host.transform;
        seatAnchorScale = seatAnchor.lossyScale;
        float x = Mathf.Max(0.24f, host.Footprint.x * 0.5f - 0.26f);
        float y = Mathf.Max(0.24f, host.Footprint.y * 0.5f - 0.26f);
        seatLocalOffset = new Vector3(
            (seatSlot % 2 == 0 ? -x : x) / Mathf.Max(0.001f, Mathf.Abs(seatAnchorScale.x)),
            (seatSlot < 2 ? y : -y) / Mathf.Max(0.001f, Mathf.Abs(seatAnchorScale.y)), 0f);
    }

    private void UpdateSeatPosition()
    {
        if (host == null)
        {
            return;
        }

        if (seatAnchor == null)
        {
            CacheSeatAnchor();
        }

        transform.position = seatAnchor.TransformPoint(seatLocalOffset) + Vector3.back * 0.02f;
        // The overhead sprite faces inward from each inset corner.
        if (frogVisual != null)
        {
            float angle = seatSlot < 2
                ? (seatSlot % 2 == 0 ? 225f : 135f)
                : (seatSlot % 2 == 0 ? 315f : 45f);
            frogVisual.transform.rotation = seatAnchor.rotation * Quaternion.Euler(0f, 0f, angle);
            Vector3 scale = seatAnchor.lossyScale;
            frogVisual.transform.localScale = Vector3.Scale(frogRestScale, new Vector3(
                scale.x / Mathf.Max(0.001f, Mathf.Abs(seatAnchorScale.x)),
                scale.y / Mathf.Max(0.001f, Mathf.Abs(seatAnchorScale.y)), 1f));
        }
    }

    private int FindFreeSlot(Brick target)
    {
        int mask = 0;
        foreach (FrogRider frog in All)
        {
            if (frog != null && frog != this && frog.host == target && !frog.falling)
            {
                mask |= 1 << frog.seatSlot;
            }
        }
        for (int i = 0; i < MaximumPerEnemy; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                return i;
            }
        }

        return 0;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (seated)
        {
            if (Room == null || Room.IsLost || host == null)
            {
                BeginFall();
            }
            else if (host.IsDefeated)
            {
                JumpFromHost(new JumpReaction(Player.CurrentVolleyId));
            }
            else
            {
                room = Room;
                if (transform.parent != room.transform)
                {
                    transform.SetParent(room.transform, true);
                }

                UpdateSeatPosition();
            }
        }
        if (falling)
        {
            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
            Stage stage = room != null ? room.GetComponentInParent<Stage>() : null;
            if (stage == null || transform.position.y < stage.FieldBottomY - 0.5f)
            {
                Destroy(gameObject);
            }
        }
        if (frogVisual != null)
        {
            frogVisual.enabled = Room == null || Room.Darkness == null || Room.Darkness.IsVisible(transform.position);
        }

        if (jumpTrail != null && frogVisual != null)
        {
            jumpTrail.enabled = frogVisual.enabled;
        }

        if (jumpTrail != null && trailExpiresAt > 0f && Time.time >= trailExpiresAt)
        {
            jumpTrail.Clear();
            trailExpiresAt = 0f;
        }
        if (kvaRing != null && kvaRing.enabled)
        {
            if (Time.time >= pulseUntil)
            {
                kvaRing.enabled = false;
            }
            else
            {
                kvaRing.transform.position = pulseOrigin;
                kvaRing.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f,
                    1f - (pulseUntil - Time.time) / 0.22f);
            }
        }
    }

    private void BeginFall()
    {
        Brick previous = seated ? host : null;
        room = Room;
        seated = false; jumping = false; falling = true; host = null;
        if (jumpTrail != null && jumpTrail.emitting)
        {
            StopJumpTrail();
        }

        All.Remove(this);
        InvalidateRoomBonuses();
        TriggerSeatedBites(previous);
        NotifyJumpResolved();
    }

    private static void NotifyJumpResolved()
    {
        GameManager.Instance?.NotifyProjectileCollectionChanged();
        GameManager.Instance?.TryCompletePlayerTurn();
    }

    private void OnDestroy()
    {
        bool wasJumping = jumping;
        All.Remove(this);
        InvalidateRoomBonuses();
        if (wasJumping && Application.isPlaying)
        {
            NotifyJumpResolved();
        }
    }
}

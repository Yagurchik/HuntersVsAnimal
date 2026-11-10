using System.Collections.Generic;
using UnityEngine;

// One roll per projectile contact. A successful roll disables only that
// projectile/enemy collider pair until the projectile has fully passed through.
[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class GhostPhasing : MonoBehaviour
{
    private sealed class Passage
    {
        public Collider2D[] Colliders;
        public Vector2 Direction;
    }

    [SerializeField, Range(0f, 1f)] private float passThroughChance = 0.2f;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private GhostVisualAnimator visual;

    private readonly Dictionary<Animal, bool> contactResults = new Dictionary<Animal, bool>();
    private readonly List<Animal> finishedContacts = new List<Animal>();
    private readonly Dictionary<Animal, Passage> ignoredColliders =
        new Dictionary<Animal, Passage>();

    public float PassThroughChance => Mathf.Clamp01(passThroughChance);

    private void Awake()
    {
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (visual == null)
        {
            visual = GetComponentInChildren<GhostVisualAnimator>();
        }
    }

    public bool TryPassThrough(Animal projectile)
    {
        WinterEnemyForm winter = GetComponent<WinterEnemyForm>();
        if (winter != null && !winter.IsFrozen)
        {
            return false;
        }

        if (winter != null && GetComponent<EnemyIllumination>()?.IsIlluminated == true)
        {
            return false;
        }

        if (projectile == null || bodyCollider == null)
        {
            return false;
        }

        if (contactResults.TryGetValue(projectile, out bool previous))
        {
            return previous;
        }

        bool passes = Random.value < PassThroughChance;
        contactResults.Add(projectile, passes);
        if (!passes)
        {
            return false;
        }

        Collider2D[] colliders = projectile.GetComponentsInChildren<Collider2D>();
        ProjectilePhysics2D flight = projectile.GetComponent<ProjectilePhysics2D>();
        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        Vector2 direction = flight != null && flight.IsFlying
            ? flight.FlightDirection : body != null ? body.velocity : Vector2.zero;
        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = (Vector2)(transform.position - projectile.transform.position);
        }

        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = Vector2.up;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, colliders[i], true);
            }
        }

        ignoredColliders.Add(projectile, new Passage {
            Colliders = colliders,
            Direction = direction.normalized
        });
        flight?.ContinueThroughEnemy();
        visual?.FlashPhase();
        return true;
    }

    public bool IsPassingThrough(Animal projectile)
    {
        return projectile != null && contactResults.TryGetValue(projectile, out bool passes)
            && passes;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Animal projectile = collision.gameObject.GetComponentInParent<Animal>();
        if (projectile != null && contactResults.TryGetValue(projectile, out bool passed)
            && !passed)
        {
            contactResults.Remove(projectile);
        }
    }

    private void FixedUpdate()
    {
        if (contactResults.Count == 0 || bodyCollider == null)
        {
            return;
        }

        finishedContacts.Clear();
        foreach (KeyValuePair<Animal, bool> contact in contactResults)
        {
            Animal projectile = contact.Key;
            if (projectile == null || !contact.Value)
            {
                if (projectile == null)
                {
                    finishedContacts.Add(projectile);
                }

                continue;
            }
            if (!ignoredColliders.TryGetValue(projectile, out Passage passage)
                || !HasEnabledCollider(passage.Colliders))
            {
                finishedContacts.Add(projectile);
                continue;
            }

            // Do not restore collision just because the first solver step briefly
            // leaves the entry edge. Keep the ghost intangible until the entire
            // projectile has cleared the far side of its physical collider.
            if (OverlapsAnyCollider(passage.Colliders))
            {
                continue;
            }

            if (HasClearedFarSide(passage) || HasRetreatedFromNearSide(projectile, passage))
            {
                finishedContacts.Add(projectile);
            }
        }
        for (int i = 0; i < finishedContacts.Count; i++)
        {
            Restore(finishedContacts[i]);
        }
    }

    private bool OverlapsAnyCollider(Collider2D[] colliders)
    {
        Bounds enemyBounds = bodyCollider.bounds;
        enemyBounds.Expand(0.03f);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled
                && enemyBounds.Intersects(colliders[i].bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEnabledCollider(Collider2D[] colliders)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasClearedFarSide(Passage passage)
    {
        Bounds ghost = bodyCollider.bounds;
        float farSide = Project(ghost.center, passage.Direction)
            + ProjectRadius(ghost.extents, passage.Direction) + 0.03f;
        for (int i = 0; i < passage.Colliders.Length; i++)
        {
            Collider2D collider = passage.Colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (Project(bounds.center, passage.Direction)
                - ProjectRadius(bounds.extents, passage.Direction) <= farSide)
            {
                return false;
            }
        }
        return true;
    }

    private bool HasRetreatedFromNearSide(Animal projectile, Passage passage)
    {
        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        if (body == null || Vector2.Dot(body.velocity, passage.Direction) >= -0.01f)
        {
            return false;
        }

        Bounds ghost = bodyCollider.bounds;
        float nearSide = Project(ghost.center, passage.Direction)
            - ProjectRadius(ghost.extents, passage.Direction) - 0.03f;
        for (int i = 0; i < passage.Colliders.Length; i++)
        {
            Collider2D collider = passage.Colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (Project(bounds.center, passage.Direction)
                + ProjectRadius(bounds.extents, passage.Direction) >= nearSide)
            {
                return false;
            }
        }
        return true;
    }

    private static float Project(Vector3 point, Vector2 direction) =>
        point.x * direction.x + point.y * direction.y;

    private static float ProjectRadius(Vector3 extents, Vector2 direction) =>
        Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y;

    private void Restore(Animal projectile)
    {
        if (ignoredColliders.TryGetValue(projectile, out Passage passage))
        {
            Collider2D[] colliders = passage.Colliders;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (bodyCollider != null && colliders[i] != null)
                {
                    Physics2D.IgnoreCollision(bodyCollider, colliders[i], false);
                }
            }

            ignoredColliders.Remove(projectile);
        }
        contactResults.Remove(projectile);
    }

    private void OnDisable()
    {
        finishedContacts.Clear();
        foreach (Animal projectile in ignoredColliders.Keys)
        {
            finishedContacts.Add(projectile);
        }

        for (int i = 0; i < finishedContacts.Count; i++)
        {
            Restore(finishedContacts[i]);
        }

        contactResults.Clear();
    }
}

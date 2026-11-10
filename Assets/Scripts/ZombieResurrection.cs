using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ZombieResurrection : MonoBehaviour, IEnemyDefeatHandler
{
    private static readonly List<ZombieResurrection> All = new List<ZombieResurrection>();

    [Header("Zombie visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite livingSprite;
    [SerializeField] private Sprite graveSprite;
    [SerializeField, Min(0.1f), Tooltip("Local visual size of the one-cell grave. The authored collider remains unchanged.")]
    private float graveVisualScale = 0.95f;

    [Header("Resurrection")]
    [SerializeField, Range(0.05f, 1f)] private float revivedHealthFraction = 0.5f;
    [SerializeField, Min(1), Tooltip("Complete player firing turns allowed to break the grave before resurrection.")]
    private int graveTurns = 1;

    private Brick enemy;
    private Animator spriteAnimator;
    private EnemyVisualHover visualHover;
    private Vector3 livingVisualScale;
    private Collider2D[] colliders;
    private bool[] colliderStates;
    private bool resurrectionConsumed;
    private bool awaitingResurrection;
    private int firstRevivalEnemyTurn = int.MaxValue;

    public bool BlocksCell => awaitingResurrection;
    public bool IsAwaitingResurrection => awaitingResurrection;
    public bool HasResurrected => resurrectionConsumed && !awaitingResurrection;
    public float GraveHealth => awaitingResurrection && enemy != null ? enemy.hp : 0f;
    public float RevivedHealthFraction => Mathf.Clamp(revivedHealthFraction, 0.05f, 1f);
    public int GraveTurns => Mathf.Max(1, graveTurns);
    public Sprite LivingSprite => livingSprite;
    public Sprite GraveSprite => graveSprite;
    public SpriteRenderer SpriteRenderer => spriteRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => All.Clear();

    private void Awake()
    {
        enemy = GetComponent<Brick>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        spriteAnimator = spriteRenderer != null ? spriteRenderer.GetComponent<Animator>() : null;
        visualHover = spriteRenderer != null ? spriteRenderer.GetComponent<EnemyVisualHover>() : null;
        livingVisualScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
        colliders = GetComponentsInChildren<Collider2D>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i] != null && colliders[i].enabled;
        }

        if (livingSprite == null && spriteRenderer != null)
        {
            livingSprite = spriteRenderer.sprite;
        }

        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }

        if (awaitingResurrection)
        {
            ShowGrave();
        }
    }

    public bool TryHandleDefeat(Brick defeatedEnemy)
    {
        if (resurrectionConsumed || awaitingResurrection || defeatedEnemy == null)
        {
            return false;
        }

        enemy = defeatedEnemy;
        resurrectionConsumed = true;
        awaitingResurrection = true;
        enemy.hp = Mathf.Max(1f, enemy.MaxHp);
        GameManager manager = GameManager.Instance;
        int currentEnemyTurn = manager != null ? manager.TurnNumber : 0;
        // Death during the player's firing phase is followed immediately by an
        // enemy turn. That turn is too early: the player has not shot the grave.
        // Death during enemy-turn effects already precedes the next player turn.
        firstRevivalEnemyTurn = currentEnemyTurn + GraveTurns
            + (manager != null && manager.IsResolvingEnemyTurn ? 0 : 1);
        ShowGrave();
        return true;
    }

    public static int ProcessAllAfterEnemyTurn(int turnNumber)
    {
        int revived = 0;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            ZombieResurrection zombie = All[i];
            if (zombie == null) { All.RemoveAt(i); continue; }
            if (!zombie.awaitingResurrection || !zombie.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (turnNumber < zombie.firstRevivalEnemyTurn)
            {
                continue;
            }

            if (zombie.Revive())
            {
                revived++;
            }
        }
        return revived;
    }

    private bool Revive()
    {
        if (!awaitingResurrection)
        {
            return false;
        }

        if (enemy == null)
        {
            enemy = GetComponent<Brick>();
        }

        if (enemy == null || enemy.levelGenerator == null || enemy.levelGenerator.IsLost)
        {
            return false;
        }

        awaitingResurrection = false;
        if (spriteRenderer != null)
        {
            if (livingSprite != null)
            {
                spriteRenderer.sprite = livingSprite;
            }

            spriteRenderer.enabled = true;
        }
        if (spriteAnimator != null)
        {
            spriteAnimator.enabled = true;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localScale = livingVisualScale;
        }

        if (visualHover != null)
        {
            visualHover.enabled = true;
        }

        RestoreColliders();
        enemy.Revive(RevivedHealthFraction);
        return true;
    }

    private void ShowGrave()
    {
        // A lethal hit starts the living zombie's damage pose before this call.
        // Stop it so its delayed restore cannot replace the grave with a zombie.
        GetComponent<EnemyHitReaction>()?.Cancel();
        if (spriteAnimator != null)
        {
            spriteAnimator.enabled = false;
        }

        if (visualHover != null)
        {
            visualHover.enabled = false;
        }

        if (spriteRenderer != null && graveSprite != null)
        {
            spriteRenderer.sprite = graveSprite;
            spriteRenderer.enabled = true;
            spriteRenderer.transform.localScale = Vector3.one * graveVisualScale;
        }
        // Keep the authored hitbox so every projectile can break the grave.
        RestoreColliders();
    }

    public void BreakGrave() => awaitingResurrection = false;

    private void RestoreColliders()
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = colliderStates != null && i < colliderStates.Length
                    ? colliderStates[i]
                    : true;
            }
        }
    }

    private void OnValidate()
    {
        revivedHealthFraction = Mathf.Clamp(revivedHealthFraction, 0.05f, 1f);
        graveTurns = Mathf.Max(1, graveTurns);
        graveVisualScale = Mathf.Max(0.1f, graveVisualScale);
    }

    private void OnDestroy() => All.Remove(this);
}

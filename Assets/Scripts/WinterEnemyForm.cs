using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Brick))]
public sealed class WinterEnemyForm : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite frozenSprite;
    [SerializeField] private Sprite thawedSprite;
    [SerializeField, Min(1)] private int quietTurnsToRefreeze = 2;

    private int quietTurns;
    private bool receivedFireSinceTurn;

    public bool IsFrozen { get; private set; } = true;

    private void Awake()
    {
        if (visual == null) visual = transform.Find("Visual")?.GetComponent<SpriteRenderer>();
        SetFrozen(true);
    }

    public void OnFireDamage()
    {
        receivedFireSinceTurn = true;
        quietTurns = 0;
        SetFrozen(false);
    }

    public void CompleteEnemyTurn()
    {
        if (IsFrozen) { receivedFireSinceTurn = false; return; }
        if (receivedFireSinceTurn) { receivedFireSinceTurn = false; return; }
        quietTurns++;
        if (quietTurns >= Mathf.Max(1, quietTurnsToRefreeze)) SetFrozen(true);
    }

    private void SetFrozen(bool value)
    {
        IsFrozen = value;
        if (visual != null)
        {
            Sprite sprite = value ? frozenSprite : thawedSprite;
            if (sprite != null)
            {
                GetComponent<EnemyHitReaction>()?.Cancel();
                visual.sprite = sprite;
                GetComponent<EnemyHitReaction>()?.SetDamageSprite(sprite);
            }
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyTurnStun : MonoBehaviour
{
    private static Material sharedMaterial;
    private LineRenderer ring;
    private ZombieResurrection resurrection;
    private int remainingTurns;

    public bool IsStunned => remainingTurns > 0;
    public int RemainingTurns => Mathf.Max(0, remainingTurns);

    public static EnemyTurnStun Apply(Brick enemy, int turns = 1)
    {
        if (enemy == null || enemy.IsDefeated) return null;
        EnemyTurnStun stun = enemy.GetComponent<EnemyTurnStun>();
        if (stun == null) stun = enemy.gameObject.AddComponent<EnemyTurnStun>();
        stun.remainingTurns = Mathf.Max(stun.remainingTurns, Mathf.Max(1, turns));
        stun.BuildVisual(enemy);
        return stun;
    }

    public bool TryConsumeEnemyTurn()
    {
        if (remainingTurns <= 0) return false;
        remainingTurns--;
        if (remainingTurns <= 0) Expire();
        return true;
    }

    private void Awake() => BuildVisual(GetComponent<Brick>());

    private void BuildVisual(Brick enemy)
    {
        if (ring != null || enemy == null) return;
        GameObject visual = new GameObject("One Turn Stun Ring");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.up * (enemy.Footprint.y * 0.58f);
        ring = visual.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 16;
        ring.widthMultiplier = 0.045f;
        ring.numCornerVertices = 2;
        ring.startColor = ring.endColor = new Color(1f, 0.78f, 0.08f, 0.95f);
        SpriteRenderer enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>(true);
        if (enemyRenderer != null) ring.sortingLayerID = enemyRenderer.sortingLayerID;
        ring.sortingOrder = Mathf.Max(RoomDarkness.WorldOverlayOrder + 32,
            enemyRenderer != null ? enemyRenderer.sortingOrder + 32 : 0);
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) sharedMaterial = new Material(shader) { name = "Runtime Stun Ring" };
        }
        ring.sharedMaterial = sharedMaterial;
        float radius = Mathf.Max(0.3f, enemy.Footprint.x * 0.25f);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.3f, 0f));
        }
    }

    private void Update()
    {
        if (ring == null) return;
        if (resurrection == null) resurrection = GetComponent<ZombieResurrection>();
        ring.enabled = resurrection == null || !resurrection.IsAwaitingResurrection;
        if (ring.enabled) ring.transform.Rotate(0f, 0f, 90f * Time.deltaTime);
    }

    private void Expire()
    {
        if (ring != null)
        {
            Destroy(ring.gameObject);
            ring = null;
        }
        Destroy(this);
    }
}

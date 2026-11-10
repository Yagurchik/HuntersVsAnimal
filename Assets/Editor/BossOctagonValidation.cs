using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossOctagonValidation
{
    private static readonly string[] Paths = {
        "Assets/Resources/Enemies/LichBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonBoss.prefab",
        "Assets/Resources/Enemies/BoneDragonMiniBoss.prefab",
        "Assets/Resources/Enemies/GhostlySorrowBoss.prefab",
        "Assets/Resources/Enemies/PoisonousGloomBoss.prefab",
        "Assets/Resources/Enemies/DrownedKing.prefab",
        "Assets/Resources/Enemies/DrownedMiniBoss.prefab",
        "Assets/Resources/Enemies/DrownedHeir.prefab",
        "Assets/Resources/Enemies/HiveHollowMiniBoss.prefab",
        "Assets/Resources/Enemies/GreenJudgeMiniBoss.prefab",
        "Assets/Resources/Enemies/BellRingerBoss.prefab",
        "Assets/Resources/Enemies/KnotBinderBoss.prefab",
        "Assets/Resources/Enemies/VolleyEaterBoss.prefab",
        "Assets/Resources/Enemies/ThreeSealIdolBoss.prefab",
        "Assets/Resources/Enemies/ImpostorBoss.prefab"
    };

    public static void RunBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int checks = 0;
        foreach (string path in Paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Missing boss: " + path);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                Brick brick = instance.GetComponent<Brick>();
                PolygonCollider2D collider = instance.GetComponent<PolygonCollider2D>();
                Require(brick != null && collider != null && collider.pathCount == 1
                    && collider.GetPath(0).Length == 8 && instance.GetComponent<BoxCollider2D>() == null,
                    "Boss needs one physical octagon: " + path);
                Vector2[] points = collider.GetPath(0);
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 edge = points[(i + 1) % points.Length] - points[i];
                    if (Mathf.Abs(edge.x) > 0.01f && Mathf.Abs(edge.y) > 0.01f)
                        Require(Mathf.Abs(Mathf.Abs(edge.x) - Mathf.Abs(edge.y)) < 0.001f,
                            "Boss bevel must be exactly 45 degrees: " + path);
                }
                checks++;

                Vector2 half = collider.bounds.extents;
                Require(Mathf.Abs(points[0].x - (-half.x + BossOctagonGeometry.CornerCut))
                    < 0.001f, "Boss bevel must be 0.15 cell wide: " + path);
                checks++;
                Physics2D.SyncTransforms();
                float bevelX = -half.x + BossOctagonGeometry.CornerCut * 0.5f;
                RaycastHit2D bevelHit = Physics2D.Raycast(
                    new Vector2(bevelX, -half.y - 1f), Vector2.up, 2f,
                    1 << instance.layer);
                Require(bevelHit.collider == collider
                    && Vector2.Dot(bevelHit.normal, new Vector2(-1f, -1f).normalized) > 0.99f
                    && Vector2.Dot(ProjectilePhysics2D.ReflectDirection(Vector2.up, bevelHit.normal),
                        Vector2.left) > 0.999f,
                    "A lower-left bevel reflects along its physical normal: " + path);
                checks++;
                float rightBevelX = half.x - BossOctagonGeometry.CornerCut * 0.5f;
                RaycastHit2D rightHit = Physics2D.Raycast(
                    new Vector2(rightBevelX, -half.y - 1f), Vector2.up, 2f,
                    1 << instance.layer);
                Require(rightHit.collider == collider
                    && Vector2.Dot(ProjectilePhysics2D.ReflectDirection(Vector2.up, rightHit.normal),
                        Vector2.right) > 0.999f,
                    "A lower-right bevel reflects along its physical normal: " + path);
                checks++;
                RaycastHit2D centerHit = Physics2D.Raycast(
                    new Vector2(0f, -half.y - 1f), Vector2.up, 2f,
                    1 << instance.layer);
                Require(centerHit.collider == collider
                    && Vector2.Dot(ProjectilePhysics2D.ReflectDirection(Vector2.up, centerHit.normal),
                        Vector2.down) > 0.999f,
                    "A flat-center hit must keep ordinary reflection: " + path);
                checks++;
                bool mirrored = ProjectilePhysics2D.TryGetMirageReturnReflection(brick, collider,
                    Vector2.up, out _);
                Require(!mirrored && instance.GetComponent<MirageCornerMirror>() == null,
                    "Ordinary octagonal bosses must not return shots to sender: " + path);
                checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        GameObject miragePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            BossOctagonAuthoring.MiragePrefab);
        Require(miragePrefab != null, "Missing Mirage prefab");
        GameObject mirage = PrefabUtility.InstantiatePrefab(miragePrefab) as GameObject;
        try
        {
            Brick brick = mirage.GetComponent<Brick>();
            BoxCollider2D square = mirage.GetComponent<BoxCollider2D>();
            Require(brick != null && square != null
                && mirage.GetComponent<PolygonCollider2D>() == null
                && mirage.GetComponent<MirageCornerMirror>() != null
                && brick.Footprint == new Vector2Int(3, 3)
                && mirage.GetComponentInChildren<SpriteRenderer>(true)?.sprite != null,
                "Mirage must have an editable 3x3 square body and return mirror");
            checks++;
            Require(!ProjectilePhysics2D.IsOctagonalBossCollider(brick, square),
                "Mirage keeps its square mirror collider");
            checks++;

            Physics2D.SyncTransforms();
            Vector2 leftCorner = new Vector2(square.bounds.min.x + 0.25f,
                square.bounds.min.y - 1f);
            RaycastHit2D cornerHit = Physics2D.Raycast(leftCorner, Vector2.up, 2f,
                1 << mirage.layer);
            Require(cornerHit.collider == square && cornerHit.normal == Vector2.down
                && ProjectilePhysics2D.TryGetMirageReturnReflection(brick, square,
                    Vector2.up, out Vector2 mirrored)
                && mirrored == Vector2.down,
                "Mirage's corner returns the shot to its sender");
            checks++;

            Vector2 middle = new Vector2(square.bounds.center.x,
                square.bounds.min.y - 1f);
            RaycastHit2D middleHit = Physics2D.Raycast(middle, Vector2.up, 2f,
                1 << mirage.layer);
            Require(middleHit.collider == square && middleHit.normal == Vector2.down
                && ProjectilePhysics2D.TryGetMirageReturnReflection(brick, square,
                    Vector2.up, out Vector2 centerReturn)
                && centerReturn == Vector2.down,
                "Mirage's flat center also returns the shot to its sender");
            checks++;
            Vector2 angledIncoming = new Vector2(0.6f, 0.8f);
            Require(ProjectilePhysics2D.TryGetMirageReturnReflection(brick, square,
                    angledIncoming, out Vector2 angledReturn)
                && Vector2.Dot(angledReturn, -angledIncoming) > 0.999f,
                "Mirage returns angled shots along their incoming path");
            checks++;
        }
        finally { UnityEngine.Object.DestroyImmediate(mirage); }
        Debug.Log("Boss octagon and Mirage validation passed: " + checks + " checks.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

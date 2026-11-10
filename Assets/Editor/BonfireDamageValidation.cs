using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BonfireDamageValidation
{
    [MenuItem("Tools/Forest Animal War/Tests/Check Bonfire Damage")]
    public static void Check()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/AnimalsBullet/BaseBullet.prefab");
        if (prefab == null || prefab.GetComponent<FireflyImpactEffect>() == null)
            throw new InvalidOperationException("Editable firefly prefab is missing.");

        FieldInfo recordedDamage = typeof(Brick).GetField("fireflyBonfireDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        for (int tier = 0; tier <= AnimalProjectileQueue.MaximumBranchLevel; tier++)
        {
            GameObject firefly = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject target = new GameObject("Bonfire damage validation target");
            target.SetActive(false);
            try
            {
                FireflyImpactEffect effect = firefly.GetComponent<FireflyImpactEffect>();
                Brick enemy = target.AddComponent<Brick>();
                target.GetComponent<EnemyBurnTint>().CaptureCurrentAsBaseColors();
                effect.ConfigureBurnDamage(999f, true, tier);
                for (int stack = 0; stack < 5; stack++) effect.ApplyTo(enemy);
                float expected = 10f * (tier + 1);
                float actual = (float)recordedDamage.GetValue(enemy);
                if (!Mathf.Approximately(effect.BonfireDamage, expected)
                    || !Mathf.Approximately(actual, expected)
                    || enemy.GetComponent<EnemyStatusEffects>().GetStackCount(
                        FireflyImpactEffect.BurnEffectId) != 5)
                    throw new InvalidOperationException("Bonfire tier " + tier
                        + " should deal " + expected + ", got " + actual + ".");
            }
            finally
            {
                Object.DestroyImmediate(firefly);
                Object.DestroyImmediate(target);
            }
        }
        Debug.Log("Bonfire damage validation passed: 10/20/30/40 independent of burn damage and ultimate.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Use an isolated batch-mode project.");
        try
        {
            Check();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}

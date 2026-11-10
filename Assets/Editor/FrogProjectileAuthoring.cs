using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FrogProjectileAuthoring
{
    private const string ArtPath = "Assets/MonsterSprite/ProjectileArt/Frog/frog-projectile.png";
    private const string SittingArtPath = "Assets/MonsterSprite/ProjectileArt/Frog/frog-seated.png";
    private const string UIArtPath = "Assets/MonsterSprite/ProjectileArt/Frog/frog-ui.png";
    private const string RiderPath = "Assets/AnimalsBullet/FrogRider.prefab";
    private const string ProjectilePath = "Assets/AnimalsBullet/Frog.prefab";

    [MenuItem("Forest Animal War/Projectiles/Bake frog prefabs and menu choice")]
    public static void Bake()
    {
        Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (art == null) throw new InvalidOperationException("Frog sprite is missing.");
        Sprite sittingArt = AssetDatabase.LoadAssetAtPath<Sprite>(SittingArtPath);
        Sprite uiArt = AssetDatabase.LoadAssetAtPath<Sprite>(UIArtPath);
        if (sittingArt == null || uiArt == null)
            throw new InvalidOperationException("Frog sitting sprite or UI icon is missing.");
        var riderObject = new GameObject("Frog Rider");
        try
        {
            FrogRider rider = riderObject.AddComponent<FrogRider>();
            var visual = new GameObject("Small Frog");
            visual.transform.SetParent(riderObject.transform, false);
            visual.transform.localScale = Vector3.one * 0.4f;
            SpriteRenderer sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sprite = sittingArt; sprite.sortingOrder = 85;
            var ringObject = new GameObject("KVA Radius Ring");
            ringObject.transform.SetParent(riderObject.transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            ring.widthMultiplier = 0.045f; ring.numCornerVertices = 2;
            ring.startColor = ring.endColor = new Color(0.65f, 1f, 0.3f, 0.8f);
            ring.sortingOrder = 84;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
            ring.enabled = false;
            var riderData = new SerializedObject(rider);
            riderData.FindProperty("frogVisual").objectReferenceValue = sprite;
            riderData.FindProperty("kvaRing").objectReferenceValue = ring;
            riderData.FindProperty("jumpSeconds").floatValue = 0.1f;
            riderData.FindProperty("jumpTrail").objectReferenceValue = AddJumpTrail(riderObject.transform);
            riderData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(riderObject, RiderPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(riderObject); }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/BaseBullet.prefab");
        GameObject projectile = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(projectile, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            projectile.name = "Frog";
            projectile.transform.position = Vector3.zero;
            projectile.transform.localScale = Vector3.one * 0.55f;
            CircleCollider2D collider = projectile.GetComponent<CircleCollider2D>();
            collider.radius = 0.2f;
            foreach (FireflyImpactEffect burn in projectile.GetComponentsInChildren<FireflyImpactEffect>())
                UnityEngine.Object.DestroyImmediate(burn);
            foreach (Transform child in projectile.transform)
                if (child.name == "Circle") UnityEngine.Object.DestroyImmediate(child.gameObject);
            SpriteRenderer sprite = projectile.GetComponent<SpriteRenderer>();
            sprite.sprite = art; sprite.color = Color.white; sprite.sortingOrder = 80;
            Animal animal = projectile.GetComponent<Animal>();
            animal.dmg = 15f; animal.SetRuntimeHealth(25f);
            FrogProjectile frog = projectile.AddComponent<FrogProjectile>();
            var data = new SerializedObject(frog);
            data.FindProperty("riderPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FrogRider>(RiderPath);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(projectile); }
        AssetDatabase.SaveAssets();
        BakeMenuChoice(uiArt);
        Debug.Log("FROG_AUTHORING_PASSED");
    }

    [MenuItem("Forest Animal War/Projectiles/Update frog combat visuals")]
    public static void UpdateCombatVisuals()
    {
        GameObject riderObject = PrefabUtility.LoadPrefabContents(RiderPath);
        try
        {
            FrogRider rider = riderObject.GetComponent<FrogRider>();
            var data = new SerializedObject(rider);
            var sprite = (SpriteRenderer)data.FindProperty("frogVisual").objectReferenceValue;
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SittingArtPath);
            data.FindProperty("jumpTrail").objectReferenceValue = AddJumpTrail(riderObject.transform);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(riderObject, RiderPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(riderObject); }
        GameObject projectile = PrefabUtility.LoadPrefabContents(ProjectilePath);
        try
        {
            projectile.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(projectile); }
        AssetDatabase.SaveAssets();
    }

    private static TrailRenderer AddJumpTrail(Transform root)
    {
        Transform existing = root.Find("Jump Trail");
        var trailObject = existing != null ? existing.gameObject : new GameObject("Jump Trail");
        trailObject.transform.SetParent(root, false);
        var trail = trailObject.GetComponent<TrailRenderer>();
        if (trail == null) trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        trail.time = 0.28f;
        trail.minVertexDistance = 0.025f;
        trail.widthMultiplier = 0.035f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(0.85f, 1f, 0.45f), 0f),
            new GradientColorKey(new Color(0.4f, 0.85f, 0.3f), 1f)
        }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.numCornerVertices = 3; trail.numCapVertices = 3;
        trail.sortingOrder = 84;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;
        trail.emitting = false;
        return trail;
    }

    private static void BakeMenuChoice(Sprite art)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        ProjectileLoadoutUI dialog = UnityEngine.Object.FindObjectOfType<ProjectileLoadoutUI>(true);
        if (dialog == null) throw new InvalidOperationException("Authored loadout dialog is missing.");
        var data = new SerializedObject(dialog);
        SerializedProperty choices = data.FindProperty("choices");
        if (choices.arraySize == 6)
        {
            SerializedProperty previous = choices.GetArrayElementAtIndex(5);
            Button previousButton = (Button)previous.FindPropertyRelative("button").objectReferenceValue;
            GameObject newChoice = UnityEngine.Object.Instantiate(previousButton.gameObject, previousButton.transform.parent);
            newChoice.name = "Frog Choice";
            choices.arraySize = 7;
            SerializedProperty choice = choices.GetArrayElementAtIndex(6);
            foreach (string field in new[] { "button", "background", "icon", "label", "status" })
            {
                var oldComponent = (Component)previous.FindPropertyRelative(field).objectReferenceValue;
                string path = AnimationUtility.CalculateTransformPath(oldComponent.transform, previousButton.transform);
                Transform target = string.IsNullOrEmpty(path) ? newChoice.transform : newChoice.transform.Find(path);
                choice.FindPropertyRelative(field).objectReferenceValue = target.GetComponent(oldComponent.GetType());
            }
        }
        if (choices.arraySize != 7 && choices.arraySize != AnimalProjectileQueue.AnimalTypeCount)
            throw new InvalidOperationException("Unexpected loadout choice count.");
        for (int i = 0; i < choices.arraySize; i++)
        {
            var choice = choices.GetArrayElementAtIndex(i);
            Button button = (Button)choice.FindPropertyRelative("button").objectReferenceValue;
            RectTransform rect = (RectTransform)button.transform;
            // Seven compact authored cards in four rows, with the last centred.
            rect.anchoredPosition = new Vector2(i == 6 && choices.arraySize == 7 ? 0f
                : (i % 2 == 0 ? -203f : 203f), 166f - i / 2 * 144f);
            rect.sizeDelta = new Vector2(388f, 132f);
            if (i == 6)
            {
                ((Image)choice.FindPropertyRelative("icon").objectReferenceValue).sprite = art;
                ((Text)choice.FindPropertyRelative("label").objectReferenceValue).text = "Лягушка";
                ((Text)choice.FindPropertyRelative("status").objectReferenceValue).text = "ВЫБРАТЬ";
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialog);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}

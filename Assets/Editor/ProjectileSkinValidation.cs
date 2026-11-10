using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ProjectileSkinValidation
{
    private const string Running = "ForestAnimalWar.ProjectileSkinValidation";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static double deadline;
    private static int lastFrame = -1;

    static ProjectileSkinValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            steps.Push(Checks());
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation")
            throw new Exception("Use an isolated validation project.");
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator Checks()
    {
        yield return null; yield return null;
        Require(ProjectileSkins.Count == 8, "Eight authored cosmetics available in the player build");
        for (int i = 0; i < 8; i++)
        {
            var skin = ProjectileSkins.Get(i);
            Require(skin.Artwork != null && skin.OriginalIcon != null && skin.DiamondPrice == 30,
                "Complete skin definition: " + skin.Name);
            Require(skin.ProjectilePrefab.GetComponent<ProjectileSkinVisual>().ProjectileIndex == i,
                "Prefab has its own stable cosmetic identity: " + i);
            Require(Vector2.Distance(skin.Artwork.bounds.size, skin.OriginalIcon.bounds.size) < 0.01f,
                "Skin retains original world canvas size: " + i);
            PlayerPrefs.DeleteKey("ForestAnimalWar.Skins.Owned." + skin.Id);
            PlayerPrefs.DeleteKey("ForestAnimalWar.Skins.Equipped." + skin.Id);
        }
        int gold = MetaProgression.Gold, seeds = MetaProgression.Seeds, acorns = MetaProgression.Acorns;
        PlayerPrefs.SetInt("ForestAnimalWar.Meta.Diamonds", 29);
        Require(!ProjectileSkins.TryBuy(0) && MetaProgression.Diamonds == 29 && !ProjectileSkins.IsOwned(0),
            "Insufficient diamonds never buy or charge a skin");
        Require(!ProjectileSkins.TryEquip(0, true) && !ProjectileSkins.TryBuy(-1)
            && !ProjectileSkins.TryBuy(8) && !MetaProgression.TrySpendDiamonds(0)
            && !MetaProgression.TrySpendDiamonds(-30), "Invalid requests cannot grant currency or cosmetics");

        var hub = Object.FindObjectOfType<MenuHubController>();
        Require(hub != null, "Authored village menu starts");
        hub.SelectTab(2);
        Transform page = hub.tabs[2].page.transform;
        var shop = page.GetComponentInChildren<ProjectileSkinShopUI>(true);
        Require(shop != null && page.Find("Forest Village Runtime/Viewport/Content/Village Field/Open Skin Shop") != null,
            "Village contains the authored shop and its entrance");
        yield return Capture(hub, "Logs/ProjectileSkinVillage.png");
        shop.Show();
        Require(hub.IsDialogOpen && shop.Cards.Length == 8 && shop.Cards.All(c => c.Preview.sprite != null),
            "Shop opens as a modal with eight previews");
        Require(!shop.Cards[0].Action.interactable, "Shop disables purchase when diamonds are missing");
        PlayerPrefs.SetInt("ForestAnimalWar.Meta.Diamonds", 240);
        shop.Refresh();
        for (int i = 0; i < 8; i++)
        {
            int before = MetaProgression.Diamonds;
            shop.AcquireOrEquip(i);
            Require(ProjectileSkins.IsOwned(i) && ProjectileSkins.IsEquipped(i)
                && MetaProgression.Diamonds == before - 30, "Buy and equip exactly once: " + i);
            Require(!ProjectileSkins.TryBuy(i) && MetaProgression.Diamonds == before - 30,
                "Repeated purchase cannot charge twice: " + i);
            shop.UseOriginal(i);
            Require(ProjectileSkins.IsOwned(i) && !ProjectileSkins.IsEquipped(i)
                && ProjectileSkins.EquippedArtwork(i) == null, "Ordinary appearance can be restored: " + i);
            shop.AcquireOrEquip(i);
            Require(ProjectileSkins.IsEquipped(i) && MetaProgression.Diamonds == before - 30,
                "Owned skin can be equipped free: " + i);
        }
        Require(MetaProgression.Diamonds == 0 && MetaProgression.Gold == gold
            && MetaProgression.Seeds == seeds && MetaProgression.Acorns == acorns,
            "Only diamonds pay for cosmetics; no other balance or rewards change");
        yield return Capture(hub, "Logs/ProjectileSkinShopTop.png");
        shop.List.verticalNormalizedPosition = 0f;
        yield return Capture(hub, "Logs/ProjectileSkinShopBottom.png");
        hub.CloseDialogs();
        Require(!shop.IsOpen && !hub.IsDialogOpen, "Global close dismisses the shop");
        PlayerPrefs.Save();
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/UIscene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null;
        Require(Enumerable.Range(0, 8).All(ProjectileSkins.IsEquipped) && MetaProgression.Diamonds == 0,
            "All purchases and equipped appearances survive scene reload");
        var catalogField = typeof(ProjectileSkins).GetField("catalogue", BindingFlags.Static | BindingFlags.NonPublic);
        catalogField.SetValue(null, null);
        Require(Enumerable.Range(0, 8).All(ProjectileSkins.IsEquipped), "Ownership survives catalogue cache reset");
        GameRunMode.PrepareCampaign(); LevelProgression.PrepareLevel(6);
        EditorSceneManager.LoadSceneInPlayMode("Assets/Asset.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null;
        GameManager manager = GameManager.Instance;
        Require(manager != null, "Native gameplay scene starts with equipped skins");
        manager.enabled = false;
        foreach (Player player in Object.FindObjectsOfType<Player>()) player.enabled = false;
        var queue = Object.FindObjectOfType<AnimalProjectileQueue>();
        Require(queue != null && queue.Animals.Count >= 8, "Native projectile queue is available");
        for (int i = 0; i < 8; i++)
        {
            var skin = ProjectileSkins.Get(i);
            Require(queue.Animals[i].GetIcon() == skin.Artwork && queue.Animals[i].GetIconColor() == Color.white,
                "Wheel, HUD, upgrades and pause use the selected icon: " + i);
            GameObject instance = Object.Instantiate(skin.ProjectilePrefab, new Vector3(0, 5, 0), Quaternion.identity);
            foreach (MonoBehaviour behaviour in instance.GetComponents<MonoBehaviour>())
                if (!(behaviour is ProjectileSkinVisual) && !(behaviour is ProjectileFlightVisual)) behaviour.enabled = false;
            if (instance.TryGetComponent(out Rigidbody2D body)) body.simulated = false;
            yield return null;
            var flight = instance.GetComponent<ProjectileFlightVisual>();
            SpriteRenderer visual = flight != null ? flight.Renderer : instance.GetComponent<SpriteRenderer>();
            Require(visual != null && visual.sprite == skin.Artwork && visual.color == Color.white,
                "Actual projectile renders the selected skin without prefab tint: " + i);
            Animal authored = skin.ProjectilePrefab.GetComponent<Animal>(), spawned = instance.GetComponent<Animal>();
            Require(Mathf.Approximately(authored.dmg, spawned.dmg) && Mathf.Approximately(authored.Health, spawned.Health)
                && instance.GetComponents<Collider2D>().Length == skin.ProjectilePrefab.GetComponents<Collider2D>().Length,
                "Appearance preserves projectile damage, health and collider count: " + i);
            Collider2D[] beforeColliders = skin.ProjectilePrefab.GetComponents<Collider2D>();
            Collider2D[] afterColliders = instance.GetComponents<Collider2D>();
            for (int c = 0; c < beforeColliders.Length; c++)
            {
                Require(beforeColliders[c].offset == afterColliders[c].offset,
                    "Collider offset is preserved: " + i);
                if (beforeColliders[c] is CircleCollider2D circle)
                    Require(Mathf.Approximately(circle.radius, ((CircleCollider2D)afterColliders[c]).radius),
                        "Collision radius is preserved: " + i);
                if (beforeColliders[c] is BoxCollider2D box)
                    Require(box.size == ((BoxCollider2D)afterColliders[c]).size, "Collision box is preserved: " + i);
            }
            Object.DestroyImmediate(instance);
        }
        var riderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimalsBullet/FrogRider.prefab");
        var rider = Object.Instantiate(riderPrefab);
        Require(rider.GetComponentInChildren<SpriteRenderer>().sprite == ProjectileSkins.Get(6).Artwork,
            "Seated and jumping frogs retain their purchased appearance");
        Object.DestroyImmediate(rider);
        Require(ProjectileSkins.TryEquip(2, false) && queue.Animals[2].GetIcon() != ProjectileSkins.Get(2).Artwork,
            "Switching to the ordinary rabbit restores its original HUD icon");
        ProjectileSkins.TryEquip(2, true);
        File.WriteAllLines("Logs/ProjectileSkinValidation.txt", report);
    }

    private static IEnumerator Capture(MenuHubController hub, string path)
    {
        const int width = 720, height = 1280;
        Screen.SetResolution(width, height, false);
        var camera = new GameObject("UI Capture", typeof(Camera)).GetComponent<Camera>();
        camera.backgroundColor = VillageUIStyle.Background;
        var canvas = hub.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectsOfType<Canvas>().OrderByDescending(c => c.sortingOrder).First();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var render = new RenderTexture(width, height, 24); render.Create(); camera.targetTexture = render;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        for (int i = 0; i < 5; i++) yield return null;
        Canvas.ForceUpdateCanvases(); camera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
        Object.Destroy(texture); render.Release(); Object.Destroy(render); Object.Destroy(camera.gameObject);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.Add("PASS: " + message);
    }
    private static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Skin shop validation timed out");
            while (steps.Count > 0)
            {
                IEnumerator current = steps.Peek();
                if (!current.MoveNext()) { steps.Pop(); continue; }
                if (current.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        if (error != null) { report.Add("FAIL: " + error); Debug.LogException(error); }
        File.WriteAllLines("Logs/ProjectileSkinValidation.txt", report);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

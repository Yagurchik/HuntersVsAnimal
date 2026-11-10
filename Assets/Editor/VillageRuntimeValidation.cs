using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// This suite deliberately uses test progress. Never run against the player's project.
[InitializeOnLoad]
public static class VillageRuntimeValidation
{
    private const string Running = "VillageRuntimeValidation.Running";
    private static IEnumerator checks;
    private static double deadline;
    private static int lastFrame = -1;
    private static int count;

    static VillageRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            checks = CheckRuntime();
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || Application.companyName != "CodexValidation"
            || Application.productName != "VillageEquipmentTests")
            throw new InvalidOperationException("Run only in the isolated village test project.");
        EditorSceneManager.OpenScene("Assets/Scenes/UIscene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static IEnumerator CheckRuntime()
    {
        yield return null;
        yield return null;
        var hub = Object.FindObjectOfType<MenuHubController>();
        Check(hub != null, "Menu starts in Play mode");
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
        {
            PlayerPrefs.DeleteKey("ForestAnimalWar.Village.Artifact." + MetaProgression.GetArtifactDefinition(i).Id);
            PlayerPrefs.DeleteKey("ForestAnimalWar.Hero.EquipmentSlot." + i);
        }
        PlayerPrefs.SetInt("ForestAnimalWar.Meta.Gold", 1000);
        PlayerPrefs.SetInt("ForestAnimalWar.Meta.Acorns", 100);
        PlayerPrefs.SetInt("ForestAnimalWar.Village.GreatTreeLevel", 1);
        PlayerPrefs.SetInt("ForestAnimalWar.Village.ForestShopLevel", 1);
        PlayerPrefs.DeleteKey("ForestAnimalWar.Village.AcornFraction");
        PlayerPrefs.DeleteKey("ForestAnimalWar.Village.GoldFraction");
        PlayerPrefs.DeleteKey("ForestAnimalWar.Village.PassiveStoredMinutes");
        PlayerPrefs.DeleteKey("ForestAnimalWar.Village.PendingAcorns");
        PlayerPrefs.DeleteKey("ForestAnimalWar.Village.PendingGold");
        PlayerPrefs.SetString("ForestAnimalWar.Village.PassiveTimestampUtc",
            (DateTime.UtcNow - TimeSpan.FromMinutes(60)).Ticks.ToString());
        hub.Refresh();
        hub.SelectTab(2);
        yield return null;
        Transform village = hub.tabs[2].page.transform;
        Transform content = village.Find("Forest Village Runtime/Viewport/Content");
        var tree = content.Find("Village Field/GreatTreeBuilding").GetComponent<VillageBuildingView>();
        Click(tree.openButton);
        Check(hub.IsDialogOpen, "Tree click opens menu");
        yield return null;
        var collect = village.Find("Building Menu/Panel/Collect Income").GetComponent<Button>();
        Click(collect);
        Check(MetaProgression.Gold == 1006 && MetaProgression.Acorns == 101
            && !MetaProgression.PendingPassiveIncome.HasIncome, "Tree button collects both buildings once");
        Check(hub.currencyText.text == ForestVillageUI.FormatAmount(MetaProgression.AcornsExact)
            && hub.goldText.text == ForestVillageUI.FormatAmount(MetaProgression.GoldExact)
            && hub.seedsText.text == MetaProgression.Seeds.ToString()
            && hub.diamondsText.text == MetaProgression.Diamonds.ToString(),
            "All four header counters refresh after collection");
        Check(!collect.interactable, "Tree collection cannot be repeated without new income");
        Click(village.Find("Building Menu/Panel/Upgrade Building").GetComponent<Button>());
        Check(MetaProgression.GreatTreeLevel == 2 && MetaProgression.Gold == 906, "Tree upgrade through UI");
        Click(village.Find("Building Menu/Panel/Close").GetComponent<Button>());
        Check(!hub.IsDialogOpen, "Close button dismisses tree");
        var shop = content.Find("Village Field/ForestShopBuilding").GetComponent<VillageBuildingView>();
        Click(shop.openButton);
        Check(hub.IsDialogOpen, "Shop click opens menu");
        yield return null;
        Click(village.Find("Building Menu/Panel/Upgrade Building").GetComponent<Button>());
        Check(MetaProgression.ForestShopLevel == 2 && MetaProgression.Acorns == 91, "Shop upgrade through UI");
        var spin = village.Find("Building Menu/Panel/Shop Offers/Spin Button").GetComponent<Button>();
        Click(spin);
        int artifact = -1;
        for (int i = 0; i < MetaProgression.ArtifactCount; i++)
            if (MetaProgression.IsArtifactOwned(i)) artifact = i;
        Check(artifact >= 0 && !MetaProgression.IsArtifactEquipped(artifact)
            && MetaProgression.Gold == 856 && MetaProgression.UnownedOrdinaryArtifactCount == MetaProgression.OrdinaryArtifactCount - 1,
            "Roulette awards one random artifact to the bag for fixed cost");
        Check(!spin.interactable, "Spin is disabled during reveal animation");
        float revealDeadline = Time.realtimeSinceStartup + 3f;
        while (!spin.interactable && Time.realtimeSinceStartup < revealDeadline) yield return null;
        Check(spin.interactable, "Spin is available again after reveal");
        hub.SelectTab(1);
        yield return null;
        Transform hero = hub.tabs[1].page.transform;
        Transform heroContent = hero.Find("Hero Equipment Runtime/Viewport/Content");
        var bagCell = (RectTransform)heroContent.Find("Backpack Cell 1");
        var equipCell = (RectTransform)heroContent.Find("Equipment Slot 2");
        Check(Mathf.Approximately(bagCell.rect.width, bagCell.rect.height)
            && Mathf.Approximately(equipCell.rect.width, equipCell.rect.height), "Editable cells are square");
        Click(heroContent.Find("Equipment Slot 2").GetComponent<Button>());
        var scroll = hero.Find("Hero Equipment Runtime").GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition = 0;
        yield return null;
        Click(heroContent.Find("Backpack Cell 1").GetComponent<Button>());
        Check(hub.IsDialogOpen, "Bag item opens details");
        yield return null;
        Click(hero.Find("Artifact Details/Panel/Equip").GetComponent<Button>());
        Check(MetaProgression.GetEquippedArtifact(1) == artifact && !hub.IsDialogOpen,
            "Equip roulette result into chosen slot");
        Check(!heroContent.Find("Backpack Cell 1").GetComponent<Button>().interactable, "Equipped item leaves bag");
        scroll.verticalNormalizedPosition = 1;
        yield return null;
        Click(heroContent.Find("Equipment Slot 2").GetComponent<Button>());
        yield return null;
        Click(hero.Find("Artifact Details/Panel/Unequip").GetComponent<Button>());
        Check(!MetaProgression.IsArtifactEquipped(artifact) && MetaProgression.IsArtifactOwned(artifact),
            "Unequip restores bag and disables bonus");
        scroll.verticalNormalizedPosition = 0;
        yield return null;
        Click(heroContent.Find("Backpack Cell 1").GetComponent<Button>());
        yield return null;
        Click(hero.Find("Artifact Details/Panel/Equip").GetComponent<Button>());
        Check(MetaProgression.GetEquippedArtifact(0) == artifact, "Automatic first empty slot");
        PlayerPrefs.Save();
        SceneManager.LoadScene("UIscene");
        yield return null;
        yield return null;
        hub = Object.FindObjectOfType<MenuHubController>();
        Check(hub != null && MetaProgression.GetEquippedArtifact(0) == artifact
            && MetaProgression.GreatTreeLevel == 2, "Progress survives menu reload");
        hub.SelectTab(1);
        Check(hub.tabs[1].page.transform.Find("Hero Equipment Runtime") != null, "Equipment page rebuilt after reload");
        Check(Object.FindObjectsOfType<MenuAnimalCard>(true).Length == 0, "No legacy permanent animal cards in scene");
    }

    private static void Click(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "Button ready: " + button?.name);
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "Button is reachable by pointer: " + button.name + " at " + point
            + "; top hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Runtime village validation: " + message);
        count++;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Village Play-mode test timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (checks.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Running, false);
        Directory.CreateDirectory("Logs");
        string report = error == null ? "PASS: " + count + " Play-mode checks including pointer raycasts and scene reload."
            : error.ToString();
        File.WriteAllText("Logs/VillageRuntimeValidation.txt", report);
        if (error != null) Debug.LogException(error);
        else Debug.Log(report);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

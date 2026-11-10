using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The editable menu dialog that chooses which projectiles can be unlocked in a run.</summary>
public sealed class ProjectileLoadoutUI : MonoBehaviour
{
    [Serializable]
    private sealed class ChoiceView
    {
        public Button button;
        public Image background;
        public Image icon;
        public Text label;
        public Text status;
    }

    [SerializeField] private ChoiceView[] choices;
    [SerializeField] private Text countText;
    [SerializeField] private Button startButton;
    [SerializeField] private Text startLabel;
    [SerializeField] private Button closeButton;

    private const int MaximumTypes = 5;
    private readonly HashSet<int> selected = new HashSet<int>();
    private Action<IReadOnlyList<int>> onConfirmed;
    private bool submitted;
    private int startingAnimal = -1;

    public bool IsOpen => gameObject.activeInHierarchy;

    public bool Show(Action<IReadOnlyList<int>> confirmed, int locationIndex = 0)
    {
        if (confirmed == null || choices == null
            || choices.Length != AnimalProjectileQueue.AnimalTypeCount
            || startButton == null || closeButton == null)
        {
            Debug.LogError("Projectile loadout prefab has missing authored controls.", this);
            return false;
        }
        for (int i = 0; i < choices.Length; i++)
        {
            ChoiceView choice = choices[i];
            if (choice == null || choice.button == null || choice.background == null
                || choice.icon == null || choice.label == null || choice.status == null)
            {
                Debug.LogError("Projectile loadout prefab is missing a choice control.", this);
                return false;
            }
        }
        onConfirmed = confirmed;
        submitted = false;
        gameObject.SetActive(true);
        GameTypography.ApplyTo(gameObject);
        int starter = AnimalProjectileQueue.StartingAnimalForLocation(locationIndex > 0
            ? locationIndex : LevelProgression.SelectedMenuLocation);
        if (startingAnimal != starter && startingAnimal >= 0)
        {
            selected.Remove(startingAnimal);
        }

        startingAnimal = starter;
        selected.Add(AnimalProjectileQueue.FireflyIndex);
        selected.Add(startingAnimal);

        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            ChoiceView choice = choices[i];
            choice.button.interactable = true;
            choice.button.onClick.RemoveAllListeners();
            choice.button.onClick.AddListener(() => Toggle(index));
        }
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(Confirm);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Close);
        Refresh();
        return true;
    }

    private void Toggle(int index)
    {
        if (index == AnimalProjectileQueue.FireflyIndex || index == startingAnimal)
        {
            return;
        }

        if (!selected.Remove(index) && selected.Count < MaximumTypes)
        {
            selected.Add(index);
        }

        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < choices.Length; i++)
        {
            ChoiceView choice = choices[i];
            if (choice == null || choice.button == null || !choice.button.gameObject.activeSelf)
            {
                continue;
            }

            bool included = selected.Contains(i);
            if (choice.background != null)
            {
                choice.background.color = included
                    ? new Color(0.43f, 0.30f, 0.13f, 1f)
                    : new Color(0.12f, 0.22f, 0.17f, 1f);
            }

            if (choice.status != null)
            {
                choice.status.text = i == AnimalProjectileQueue.FireflyIndex || i == startingAnimal
                    ? "СРАЗУ: " + AnimalProjectileQueue.StartingAmmoForAnimal(i) : included ? "В УЛУЧШЕНИЯХ"
                    : selected.Count == MaximumTypes ? "СНИМИ ОДИН" : "ВЫБРАТЬ";
            }
        }
        if (countText != null)
        {
            countText.text = "В НАБОРЕ: " + selected.Count + " / " + MaximumTypes;
        }

        bool ready = selected.Count == MaximumTypes;
        if (startButton != null)
        {
            startButton.interactable = ready;
        }

        if (startLabel != null)
        {
            startLabel.text = ready ? "НАЧАТЬ УРОВЕНЬ" : "ВЫБЕРИ 5 ВИДОВ";
        }
    }

    private void Confirm()
    {
        if (submitted || onConfirmed == null
            || selected.Count != MaximumTypes)
        {
            return;
        }

        List<int> loadout = new List<int>(selected);
        loadout.Sort();
        submitted = true;
        Action<IReadOnlyList<int>> callback = onConfirmed;
        Close();
        callback(loadout);
    }

    public void Close()
    {
        onConfirmed = null;
        gameObject.SetActive(false);
    }
}

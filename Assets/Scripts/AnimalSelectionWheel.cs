using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AnimalSelectionWheel : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
    IPointerUpHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [System.Serializable]
    private class WheelItem
    {
        public int index;
        public RectTransform rectTransform;
        public CanvasGroup canvasGroup;
        public RadialSegmentGraphic background;
        public Text label;
    }

    [SerializeField] private List<WheelItem> items = new List<WheelItem>();
    [SerializeField] private Color selectedItemColor = new Color(0.37f, 0.25f, 0.13f, 1f);
    [SerializeField] private Color normalItemColor = new Color(0.13f, 0.21f, 0.16f, 0.98f);
    [SerializeField] private Color ringColor = new Color(0.16f, 0.13f, 0.09f, 0.96f);
    [SerializeField] private Color selectionGlowColor = new Color(0.72f, 0.45f, 0.20f, 0.95f);
    [SerializeField] private Color barkRimColor = new Color(0.39f, 0.26f, 0.15f, 0.96f);
    [SerializeField] private Color mossInlayColor = new Color(0.28f, 0.36f, 0.23f, 0.9f);
    [SerializeField] private Color normalMedallionRimColor = new Color(0.39f, 0.31f, 0.20f, 0.96f);
    [SerializeField] private Color selectedMedallionRimColor = new Color(0.72f, 0.51f, 0.27f, 1f);
    [SerializeField] private Color volleyOffColor = new Color(0.17f, 0.26f, 0.18f, 0.98f);
    [SerializeField] private Color volleyOnColor = new Color(0.46f, 0.30f, 0.15f, 0.98f);
    [SerializeField, Range(76f, 120f)] private float itemSize = 96f;
    [SerializeField, Range(44f, 72f)] private float minimumAngleStep = 54f;
    [SerializeField, HideInInspector] private int layoutVersion;
    [SerializeField] private Button allVolleyToggle;
    [SerializeField] private Image allVolleyToggleBackground;
    [SerializeField] private Text allVolleyToggleLabel;

    private AnimalProjectileQueue queue;
    private RectTransform root;
    private float radius;
    private float angleStep;
    private float centerY;
    private float visualAngle;
    private float angleVelocity;
    private bool dragging;
    private bool previewAllAnimals;
    private float dragDistance;
    private readonly List<int> selectableIndices = new List<int>();

    private const float DragDegreesPerPixel = 0.45f;
    private const float VisibleAngle = 86f;
    private const float ClickDragThreshold = 12f;
    private const string HudCanvasName = "Animal Selection HUD";

    public Button AllVolleyButton => allVolleyToggle;

    public static AnimalSelectionWheel Create(AnimalProjectileQueue projectileQueue)
    {
        if (projectileQueue == null)
        {
            return null;
        }

        AnimalSelectionWheel existing = FindObjectOfType<AnimalSelectionWheel>(true);
        if (existing != null)
        {
            existing.Bind(projectileQueue);
            return existing;
        }

        Canvas canvas = GetOrCreateHudCanvas();

        GameObject wheelObject = new GameObject(
            "Animal Selection Wheel",
            typeof(RectTransform),
            typeof(AnimalSelectionWheel));

        wheelObject.transform.SetParent(canvas.transform, false);
        wheelObject.transform.SetAsLastSibling();

        AnimalSelectionWheel wheel = wheelObject.GetComponent<AnimalSelectionWheel>();
        wheel.Bind(projectileQueue);
        return wheel;
    }

    public void Preview(AnimalProjectileQueue projectileQueue, bool allAnimals)
    {
        if (Application.isPlaying) return;
        Bind(projectileQueue);
        previewAllAnimals = allAnimals;
        RefreshPositions();
    }

    private static Canvas GetOrCreateHudCanvas()
    {
        GameObject existingCanvasObject = GameObject.Find(HudCanvasName);
        if (existingCanvasObject != null)
        {
            Canvas existingCanvas = existingCanvasObject.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                existingCanvasObject.SetActive(true);
                return existingCanvas;
            }
        }

        GameObject canvasObject = new GameObject(
            HudCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 1028f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void Bind(AnimalProjectileQueue projectileQueue)
    {
        if (queue != null)
        {
            queue.SelectionChanged -= OnSelectionChanged;
            queue.AmmoChanged -= OnAmmoChanged;
            queue.UpgradesChanged -= OnUpgradesChanged;
            queue.AllVolleyModeChanged -= OnAllVolleyModeChanged;
        }

        queue = projectileQueue;
        previewAllAnimals = false;
        Canvas hud = GetComponentInParent<Canvas>(true);
        if (hud != null) hud.transform.localScale = Vector3.one;
        radius = queue.WheelRadius;
        angleStep = Mathf.Max(minimumAngleStep, queue.WheelAngleStep);
        centerY = queue.WheelVerticalOffset;
        queue.SelectionChanged += OnSelectionChanged;
        queue.AmmoChanged += OnAmmoChanged;
        queue.UpgradesChanged += OnUpgradesChanged;
        queue.AllVolleyModeChanged += OnAllVolleyModeChanged;

        root = GetComponent<RectTransform>();
        if (layoutVersion < 2)
        {
            selectedItemColor = new Color(0.68f, 0.43f, 0.1f, 1f);
            normalItemColor = new Color(0.12f, 0.2f, 0.15f, 0.98f);
            itemSize = 96f;
            minimumAngleStep = 54f;
            layoutVersion = 2;
        }
        if (layoutVersion < 3)
        {
            // Version 3 replaces the oversized golden arc with a compact marker
            // behind the selected animal button.
            layoutVersion = 3;
        }
        if (layoutVersion < 4)
        {
            selectedItemColor = new Color(0.37f, 0.25f, 0.13f, 1f);
            normalItemColor = new Color(0.13f, 0.21f, 0.16f, 0.98f);
            ringColor = new Color(0.16f, 0.13f, 0.09f, 0.96f);
            selectionGlowColor = new Color(0.72f, 0.45f, 0.20f, 0.95f);
            layoutVersion = 4;
        }
        if (root.childCount == 0)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
        }
        root.sizeDelta = new Vector2(radius + itemSize + 18f,
            centerY + radius + itemSize * 0.58f);

        BuildVisuals();
        RefreshPositions();
    }

    private void OnDestroy()
    {
        if (queue != null)
        {
            queue.SelectionChanged -= OnSelectionChanged;
            queue.AmmoChanged -= OnAmmoChanged;
            queue.UpgradesChanged -= OnUpgradesChanged;
            queue.AllVolleyModeChanged -= OnAllVolleyModeChanged;
        }
    }

    private void Update()
    {
        RefreshAllVolleyToggle();
        if (!dragging && !Mathf.Approximately(visualAngle, 0f))
        {
            visualAngle = Mathf.SmoothDampAngle(
                visualAngle,
                0f,
                ref angleVelocity,
                0.12f,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (Mathf.Abs(visualAngle) < 0.05f)
            {
                visualAngle = 0f;
            }

            RefreshPositions();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
        dragDistance = 0f;
        angleVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (queue == null)
        {
            return;
        }

        dragDistance += eventData.delta.magnitude;
        visualAngle += eventData.delta.y * DragDegreesPerPixel;
        RefreshPositions();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        if (dragDistance >= ClickDragThreshold) SnapToNearestAnimal();
        else
        {
            visualAngle = 0f;
            RefreshPositions();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        dragging = false;
        if (queue == null || dragDistance >= ClickDragThreshold) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;

        Vector2 fromCenter = point - new Vector2(0f, centerY);
        float pointerAngle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
        if (Mathf.Abs(pointerAngle) > VisibleAngle + 8f) return;

        RebuildSelectableIndices();
        int bestIndex = -1;
        float bestDifference = float.PositiveInfinity;
        for (int i = 0; i < selectableIndices.Count; i++)
        {
            float difference = Mathf.Abs(Mathf.DeltaAngle(pointerAngle,
                GetDisplayAngle(selectableIndices[i])));
            if (difference < bestDifference)
            {
                bestDifference = difference;
                bestIndex = selectableIndices[i];
            }
        }
        if (bestIndex >= 0 && bestDifference <= angleStep * 0.62f)
            SelectWithAnimation(bestIndex);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (queue == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }

        queue.MoveSelection(eventData.scrollDelta.y > 0f ? -1 : 1);
    }

    private void BuildVisuals()
    {
        items.RemoveAll(item => item.rectTransform == null);
        if (root.Find("Wheel") == null) CreateWheelRing();
        Transform obsoleteRim = root.Find("Wheel Rim");
        if (obsoleteRim != null) obsoleteRim.gameObject.SetActive(false);
        if (root.Find("Selected") == null) CreateSelectionMarker();
        if (root.Find("Forest Wheel Artwork") == null) EnsureForestRims();

        for (int i = 0; i < queue.Animals.Count; i++)
        {
            if (!items.Exists(item => item.index == i)) CreateItem(i, queue.Animals[i]);
        }
        foreach (WheelItem item in items)
        {
            Button button = item.rectTransform.GetComponent<Button>();
            int index = item.index;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectFromButton(index));
            ConfigureItemVisual(item);
        }
        EnsureAllVolleyToggle();
        ConfigureWheelVisuals();
    }

    private void EnsureAllVolleyToggle()
    {
        Transform existing = allVolleyToggle != null ? allVolleyToggle.transform : root.Find("All Volley Toggle");
        if (existing == null)
        {
            GameObject toggleObject = new GameObject("All Volley Toggle",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            toggleObject.transform.SetParent(root, false);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = Vector2.zero;
            toggleRect.anchoredPosition = new Vector2(root.sizeDelta.x + 18f, 18f);
            toggleRect.sizeDelta = new Vector2(236f, 58f);

            GameObject labelObject = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(toggleRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 4f);
            labelRect.offsetMax = new Vector2(-10f, -4f);
            Text label = labelObject.GetComponent<Text>();
            label.font = GameTypography.Font;
            label.fontSize = 17;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            GameTypography.Apply(label);
            existing = toggleObject.transform;
        }

        allVolleyToggle = existing.GetComponent<Button>();
        // Preserve the authored position when the button lives outside the wheel.
        if (existing.parent == root)
            allVolleyToggle.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(root.sizeDelta.x + 18f, 18f);
        allVolleyToggleBackground = allVolleyToggle.GetComponent<Image>();
        allVolleyToggleLabel = allVolleyToggle.transform.Find("Label").GetComponent<Text>();
        allVolleyToggle.targetGraphic = allVolleyToggleBackground;
        allVolleyToggle.navigation = new Navigation { mode = Navigation.Mode.None };
        allVolleyToggle.onClick.RemoveAllListeners();
        allVolleyToggle.onClick.AddListener(ToggleAllVolley);
        if (existing.parent == root) existing.SetAsLastSibling();
        RefreshAllVolleyToggle();
    }

    private void ToggleAllVolley()
    {
        if (queue == null || Player.HasActiveProjectiles) return;
        queue.ToggleAllVolleyMode();
    }

    private void RefreshAllVolleyToggle()
    {
        if (allVolleyToggle == null || queue == null) return;
        bool enabled = queue.AllVolleyEnabled;
        bool tutorialLocked = Application.isPlaying && GameManager.Instance != null
            && GameManager.Instance.IsTutorialFlowActive;
        allVolleyToggle.interactable = !tutorialLocked && !Player.HasActiveProjectiles;
        allVolleyToggleBackground.color = enabled ? volleyOnColor : volleyOffColor;
        allVolleyToggleLabel.text = enabled ? "ОБЩИЙ ЗАЛП  ·  ВКЛ" : "ОБЩИЙ ЗАЛП  ·  ВЫКЛ";
    }

    private void CreateWheelRing()
    {
        GameObject ringObject = new GameObject(
            "Wheel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RadialSegmentGraphic));
        ringObject.transform.SetParent(root, false);

        RectTransform ringRect = ringObject.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.zero;
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = Vector2.one * (radius * 2f + 50f);
        ringRect.anchoredPosition = new Vector2(0f, centerY);

        RadialSegmentGraphic ring = ringObject.GetComponent<RadialSegmentGraphic>();
        ring.color = ringColor;
        ring.raycastTarget = true;
        ring.Configure(0.72f, -90f, 90f, 48);
    }

    private void CreateSelectionMarker()
    {
        GameObject markerObject = new GameObject(
            "Selected",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RadialSegmentGraphic));
        markerObject.transform.SetParent(root, false);

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = Vector2.zero;
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = Vector2.one * (itemSize + 22f);
        markerRect.anchoredPosition = new Vector2(radius, centerY);

        RadialSegmentGraphic marker = markerObject.GetComponent<RadialSegmentGraphic>();
        marker.color = selectionGlowColor;
        marker.raycastTarget = false;
        marker.Configure(0.79f, 0f, 360f, 48);
    }

    private void CreateItem(int index, AnimalProjectileQueue.AnimalProjectileEntry entry)
    {
        GameObject itemObject = new GameObject(
            entry.DisplayName,
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(CanvasRenderer),
            typeof(RadialSegmentGraphic),
            typeof(Button));

        itemObject.transform.SetParent(root, false);

        RectTransform itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.anchorMin = Vector2.zero;
        itemRect.anchorMax = Vector2.zero;
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = Vector2.one * itemSize;

        RadialSegmentGraphic background = itemObject.GetComponent<RadialSegmentGraphic>();
        background.color = normalItemColor;
        background.Configure(0f, 0f, 360f, 28);

        Button button = itemObject.GetComponent<Button>();
        button.targetGraphic = background;
        int capturedIndex = index;
        button.onClick.AddListener(() => SelectFromButton(capturedIndex));

        Outline outline = itemObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.015f, 0.025f, 0.018f, 0.95f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        EnsureItemRim(itemRect);
        CreateIcon(itemRect, entry);
        Text label = CreateLabel(itemRect);

        WheelItem item = new WheelItem
        {
            index = index,
            rectTransform = itemRect,
            canvasGroup = itemObject.GetComponent<CanvasGroup>(),
            background = background,
            label = label
        };

        items.Add(item);
    }

    private void CreateIcon(RectTransform parent, AnimalProjectileQueue.AnimalProjectileEntry entry)
    {
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.12f, 0.29f);
        iconRect.anchorMax = new Vector2(0.88f, 0.96f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = entry.GetIcon();
        image.color = entry.GetIconColor();
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private Text CreateLabel(RectTransform parent)
    {
        Font font = GameTypography.Font;
        if (font == null)
        {
            return null;
        }

        GameObject labelObject = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0.38f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text text = labelObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 9;
        text.resizeTextMaxSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        GameTypography.Apply(text);
        return text;
    }

    private void SelectFromButton(int index)
    {
        SelectWithAnimation(index);
    }

    private void SnapToNearestAnimal()
    {
        if (queue == null || angleStep <= 0f)
        {
            visualAngle = 0f;
            return;
        }

        int steps = Mathf.RoundToInt(visualAngle / angleStep);
        int direction = steps > 0 ? -1 : 1;

        for (int i = 0; i < Mathf.Abs(steps); i++)
        {
            queue.MoveSelection(direction);
        }

        visualAngle -= steps * angleStep;
        RefreshPositions();
    }

    private void SelectWithAnimation(int index)
    {
        if (queue == null || index == queue.SelectedIndex || !queue.HasAmmo(index)) return;
        float previousAngle = GetDisplayAngle(index);
        if (!queue.Select(index)) return;
        visualAngle = previousAngle;
        angleVelocity = 0f;
        RefreshPositions();
    }

    private void RebuildSelectableIndices()
    {
        selectableIndices.Clear();
        for (int i = 0; i < queue.Animals.Count; i++)
        {
            bool selectable = !Application.isPlaying
                ? queue.IsAvailable(i) || previewAllAnimals
                : queue.HasAmmo(i);
            if (selectable) selectableIndices.Add(i);
        }
    }

    private int DisplaySelection()
    {
        if (!Application.isPlaying && previewAllAnimals && selectableIndices.Count > 0)
            return selectableIndices[selectableIndices.Count / 2];
        return queue.SelectedIndex;
    }

    private float GetDisplayAngle(int animalIndex)
    {
        RebuildSelectableIndices();
        int selectedRank = selectableIndices.IndexOf(DisplaySelection());
        int itemRank = selectableIndices.IndexOf(animalIndex);
        if (selectedRank < 0 || itemRank < 0) return 180f;
        int offset = itemRank - selectedRank;
        int count = selectableIndices.Count;
        if (offset > count / 2) offset -= count;
        if (offset < -count / 2) offset += count;
        return offset * angleStep + visualAngle;
    }

    private void ConfigureWheelVisuals()
    {
        RectTransform ring = root.Find("Wheel") as RectTransform;
        if (ring != null)
        {
            ring.sizeDelta = Vector2.one * (radius * 2f + 50f);
            ring.anchoredPosition = new Vector2(0f, centerY);
            RadialSegmentGraphic graphic = ring.GetComponent<RadialSegmentGraphic>();
            graphic.color = ringColor;
            graphic.Configure(0.72f, -90f, 90f, 48);
            ring.SetAsFirstSibling();
        }
        RectTransform artwork = root.Find("Forest Wheel Artwork") as RectTransform;
        if (artwork != null)
        {
            artwork.anchorMin = artwork.anchorMax = Vector2.zero;
            artwork.pivot = new Vector2(0.5f, 0.5f);
            // The outer carving stays visible beyond the animal medallions.
            artwork.sizeDelta = Vector2.one * (radius * 2f + 160f);
            artwork.anchoredPosition = new Vector2(0f, centerY);
            artwork.SetSiblingIndex(1);
            Image image = artwork.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
        }
        else
        {
            ConfigureForestRim("Bark Crown", radius * 2f + 66f, 0.95f,
                barkRimColor, 1);
            ConfigureForestRim("Moss Inlay", radius * 2f - 32f, 0.94f,
                mossInlayColor, 2);
        }
        RectTransform marker = root.Find("Selected") as RectTransform;
        if (marker != null)
        {
            marker.sizeDelta = Vector2.one * (itemSize + 22f);
            marker.anchoredPosition = new Vector2(radius, centerY);
            RadialSegmentGraphic graphic = marker.GetComponent<RadialSegmentGraphic>();
            graphic.color = selectionGlowColor;
            graphic.Configure(0.79f, 0f, 360f, 48);
        }
    }

    private void EnsureForestRims()
    {
        EnsureArc("Bark Crown");
        EnsureArc("Moss Inlay");
    }

    private void EnsureArc(string name)
    {
        if (root.Find(name) != null) return;
        GameObject arc = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RadialSegmentGraphic));
        arc.transform.SetParent(root, false);
        RadialSegmentGraphic graphic = arc.GetComponent<RadialSegmentGraphic>();
        graphic.raycastTarget = false;
    }

    private void ConfigureForestRim(string name, float diameter, float innerRadius,
        Color tint, int siblingIndex)
    {
        RectTransform rim = root.Find(name) as RectTransform;
        if (rim == null) return;
        rim.anchorMin = rim.anchorMax = Vector2.zero;
        rim.pivot = new Vector2(0.5f, 0.5f);
        rim.sizeDelta = Vector2.one * diameter;
        rim.anchoredPosition = new Vector2(0f, centerY);
        rim.SetSiblingIndex(siblingIndex);
        RadialSegmentGraphic graphic = rim.GetComponent<RadialSegmentGraphic>();
        graphic.color = tint;
        graphic.raycastTarget = false;
        graphic.Configure(innerRadius, -90f, 90f, 64);
    }

    private RadialSegmentGraphic EnsureItemRim(RectTransform item)
    {
        Transform existing = item.Find("Wood Rim");
        if (existing == null)
        {
            GameObject rimObject = new GameObject("Wood Rim", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RadialSegmentGraphic));
            rimObject.transform.SetParent(item, false);
            existing = rimObject.transform;
        }
        RectTransform rect = (RectTransform)existing;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * (itemSize + 3f);
        rect.SetAsFirstSibling();
        RadialSegmentGraphic graphic = existing.GetComponent<RadialSegmentGraphic>();
        graphic.color = normalMedallionRimColor;
        graphic.raycastTarget = false;
        graphic.Configure(0.88f, 0f, 360f, 48);
        return graphic;
    }

    private void ConfigureItemVisual(WheelItem item)
    {
        item.rectTransform.sizeDelta = Vector2.one * itemSize;
        Outline outline = item.rectTransform.GetComponent<Outline>();
        if (outline == null) outline = item.rectTransform.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.015f, 0.025f, 0.018f, 0.95f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);
        EnsureItemRim(item.rectTransform);
        RectTransform carvedRim = item.rectTransform.Find("Carved Rim") as RectTransform;
        if (carvedRim != null)
        {
            carvedRim.anchorMin = carvedRim.anchorMax = carvedRim.pivot = new Vector2(0.5f, 0.5f);
            carvedRim.anchoredPosition = Vector2.zero;
            carvedRim.sizeDelta = Vector2.one * (itemSize + 20f);
            carvedRim.SetSiblingIndex(1);
            Image carvedImage = carvedRim.GetComponent<Image>();
            if (carvedImage != null) carvedImage.raycastTarget = false;
        }
        Transform icon = item.rectTransform.Find("Icon");
        if (icon is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0.08f, 0.27f);
            iconRect.anchorMax = new Vector2(0.92f, 0.97f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image iconImage = iconRect.GetComponent<Image>();
            if (iconImage != null && item.index >= 0 && item.index < queue.Animals.Count)
            {
                AnimalProjectileQueue.AnimalProjectileEntry entry = queue.Animals[item.index];
                iconImage.sprite = entry.GetIcon();
                iconImage.color = entry.GetIconColor();
                iconImage.preserveAspect = true;
            }
        }
        if (item.label != null)
        {
            item.label.fontSize = 13;
            item.label.fontStyle = FontStyle.Bold;
            item.label.resizeTextForBestFit = true;
            item.label.resizeTextMinSize = 9;
            item.label.resizeTextMaxSize = 14;
            GameTypography.Apply(item.label);
        }
    }

    private void OnSelectionChanged(int index)
    {
        RefreshPositions();
    }

    private void OnAmmoChanged()
    {
        RefreshPositions();
        RefreshAllVolleyToggle();
    }

    private void OnUpgradesChanged()
    {
        RefreshPositions();
    }

    private void OnAllVolleyModeChanged(bool enabled)
    {
        RefreshAllVolleyToggle();
    }

    private void RefreshPositions()
    {
        if (queue == null)
        {
            return;
        }

        RebuildSelectableIndices();
        RefreshAllVolleyToggle();
        int displaySelection = DisplaySelection();
        for (int i = 0; i < items.Count; i++)
        {
            WheelItem item = items[i];
            if (item.index >= queue.Animals.Count) { item.rectTransform.gameObject.SetActive(false); continue; }
            bool available = selectableIndices.Contains(item.index);
            bool hasAmmo = !Application.isPlaying || queue.HasAmmo(item.index);
            item.rectTransform.gameObject.SetActive(available);

            if (!available)
            {
                continue;
            }

            float angle = GetDisplayAngle(item.index);
            float radians = angle * Mathf.Deg2Rad;
            item.rectTransform.anchoredPosition = new Vector2(
                Mathf.Cos(radians) * radius,
                centerY + Mathf.Sin(radians) * radius);

            float distanceFromSelection = Mathf.Abs(Mathf.DeltaAngle(0f, angle));
            bool visible = distanceFromSelection <= VisibleAngle;
            float visibilityAlpha = visible ? Mathf.Lerp(1f, 0.72f, distanceFromSelection / VisibleAngle) : 0f;
            item.canvasGroup.alpha = hasAmmo ? visibilityAlpha : visibilityAlpha * 0.3f;
            item.canvasGroup.blocksRaycasts = visible && hasAmmo;
            item.canvasGroup.interactable = visible && hasAmmo;

            if (item.label != null)
            {
                AnimalProjectileQueue.AnimalProjectileEntry entry = queue.Animals[item.index];
                int ammo = Application.isPlaying ? entry.RemainingAmmo : entry.AmmoPerTurn;
                int totalVolleys = queue.GetManualVolleyCount(item.index);
                int remainingVolleys = Application.isPlaying
                    ? queue.GetRemainingVolleys(item.index)
                    : totalVolleys;
                string volleyText = totalVolleys > 1
                    ? " · залп " + Mathf.Clamp(totalVolleys - remainingVolleys + 1, 1, totalVolleys)
                        + "/" + totalVolleys
                    : string.Empty;
                item.label.text = LocalizedName(item.index) + "\n"
                    + ammo + "/" + entry.AmmoPerTurn + volleyText + " · ур." + entry.UpgradeLevel;
            }

            bool selected = item.index == displaySelection && distanceFromSelection < angleStep * 0.5f;
            item.rectTransform.localScale = Vector3.one * (selected ? 1.18f : 0.94f);
            item.background.color = selected
                ? selectedItemColor
                : normalItemColor;
            RadialSegmentGraphic rim = item.rectTransform.Find("Wood Rim")?
                .GetComponent<RadialSegmentGraphic>();
            if (rim != null) rim.color = selected
                ? selectedMedallionRimColor : normalMedallionRimColor;
            Image carved = item.rectTransform.Find("Carved Rim")?.GetComponent<Image>();
            if (carved != null) carved.color = selected
                ? new Color(0.82f, 0.83f, 0.76f, 1f)
                : new Color(0.61f, 0.67f, 0.61f, 0.94f);
        }
    }

    private static string LocalizedName(int index)
    {
        if (index == 0) return "СВЕТЛЯЧОК";
        if (index == 1) return "ЁЖ";
        if (index == 2) return "КРОЛИК";
        if (index == 3) return "МЕДОЕД";
        if (index == 4) return "КАБАН";
        return "СНАРЯД";
    }
}

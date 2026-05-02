using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UIButton = UnityEngine.UI.Button;
using UIText = UnityEngine.UI.Text;
using UITKButton = UnityEngine.UIElements.Button;
using UITKLabel = UnityEngine.UIElements.Label;
using UITKVisual = UnityEngine.UIElements.VisualElement;

public class WeaponInventory : MonoBehaviour
{
    const string PanelSettingsResourcePath = "UI/WeaponInventoryPanelSettings";
    const string LayoutResourcePath = "UI/WeaponInventoryRuntime";
    const string ThemeResourcePath = "UI/UnityDefaultRuntimeTheme";

    [Serializable]
    public class WeaponEntry
    {
        public string displayName = "Weapon";
        [TextArea]
        public string description = string.Empty;
        public GameObject projectilePrefab;
        public int ammo = -1;
        public Color accentColor = new(0.95f, 0.8f, 0.25f, 1f);
    }

    sealed class SlotView
    {
        public UITKButton button;
        public UITKVisual accent;
        public UITKLabel hotkey;
        public UITKLabel title;
        public UITKLabel detail;
    }

    sealed class UnitWeaponState
    {
        public readonly List<WeaponEntry> weapons = new();
        public int selectedIndex;
    }

    public static WeaponInventory Instance { get; private set; }

    [Header("Weapons")]
    public List<WeaponEntry> weapons = new();

    [Header("Legacy UI")]
    public GameObject panel;
    public UIText weaponNameText;
    public UIText weaponDescriptionText;
    public UIText ammoText;
    public UIButton previousButton;
    public UIButton nextButton;
    public Transform slotsRoot;
    public UIButton slotButtonPrefab;

    readonly List<SlotView> _slotViews = new();
    readonly List<UIButton> _legacySlotButtons = new();
    readonly List<UIText> _legacySlotLabels = new();

    UIDocument _uiDocument;
    PanelSettings _panelSettings;
    VisualTreeAsset _layoutAsset;
    StyleSheet _styleSheet;
    ThemeStyleSheet _runtimeTheme;

    UITKVisual _inventoryRoot;
    UITKVisual _inventoryShell;
    UITKVisual _ammoChip;
    UITKVisual _slotsContainer;
    UITKLabel _weaponNameLabel;
    UITKLabel _weaponDescriptionLabel;
    UITKLabel _ammoValueLabel;

    readonly Dictionary<int, UnitWeaponState> _teamWeaponStates = new();
    bool _uiInitialized;
    bool _lastShouldShowUi = true;
    Unit _lastContextUnit;
    const int VisibleSortingOrder = 200;
    const int HiddenSortingOrder = -1000;

    public WeaponEntry SelectedWeapon => GetSelectedWeaponForUnit(GetContextUnit());
    public bool HasWeapons => GetCurrentWeapons().Count > 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NormalizeWeaponEntries();
    }

    void Start()
    {
        InitializeRuntimeUi();
        _lastContextUnit = GetContextUnit();
        EnsureValidSelection(_lastContextUnit);
        _lastShouldShowUi = ShouldShowInventoryUi();
        RefreshUi();
    }

    void Update()
    {
        CleanupUnitStates();
        Unit contextUnit = GetContextUnit();
        bool shouldShowUi = ShouldShowInventoryUi();
        if (contextUnit != _lastContextUnit)
        {
            _lastContextUnit = contextUnit;
            if (shouldShowUi)
            {
                EnsureValidSelection(contextUnit);
                RefreshUi();
            }
        }

        if (shouldShowUi != _lastShouldShowUi)
        {
            _lastShouldShowUi = shouldShowUi;
            RefreshUi();
        }

        if (!shouldShowUi)
        {
            return;
        }

        if (!_uiInitialized)
        {
            InitializeRuntimeUi();
            if (_uiInitialized)
            {
                RefreshUi();
            }
        }

        if (!CanHandleSelectionInput())
        {
            return;
        }

        float scroll = ReadScrollDelta();
        if (scroll > 0.05f)
        {
            SelectPreviousWeapon();
        }
        else if (scroll < -0.05f)
        {
            SelectNextWeapon();
        }

        if (ReadPreviousPressed())
        {
            SelectPreviousWeapon();
        }

        if (ReadNextPressed())
        {
            SelectNextWeapon();
        }

        int hotkeyIndex = ReadWeaponHotkeyIndex();
        if (hotkeyIndex >= 0)
        {
            SelectWeapon(hotkeyIndex);
        }
    }

    public GameObject GetSelectedProjectilePrefab(GameObject fallback = null)
    {
        return GetSelectedProjectilePrefab(GetContextUnit(), fallback);
    }

    public GameObject GetSelectedProjectilePrefab(Unit unit, GameObject fallback = null)
    {
        WeaponEntry weapon = GetSelectedWeaponForUnit(unit);
        if (weapon == null || weapon.projectilePrefab == null || !CanUseWeapon(weapon))
        {
            return fallback;
        }

        return weapon.projectilePrefab;
    }

    public bool TryBeginShot(out WeaponEntry weapon, out GameObject projectilePrefab)
    {
        return TryBeginShot(GetContextUnit(), out weapon, out projectilePrefab);
    }

    public bool TryBeginShot(Unit unit, out WeaponEntry weapon, out GameObject projectilePrefab)
    {
        EnsureValidSelection(unit);

        weapon = GetSelectedWeaponForUnit(unit);
        projectilePrefab = null;
        if (weapon == null || weapon.projectilePrefab == null || !CanUseWeapon(weapon))
        {
            return false;
        }

        projectilePrefab = weapon.projectilePrefab;
        return true;
    }

    public void CommitShot(WeaponEntry weapon)
    {
        CommitShot(GetContextUnit(), weapon);
    }

    public void CommitShot(Unit unit, WeaponEntry weapon)
    {
        if (weapon == null || weapon.ammo < 0)
        {
            return;
        }

        weapon.ammo = Mathf.Max(0, weapon.ammo - 1);
        EnsureValidSelection(unit);
        RefreshUi();
    }

    public void SelectPreviousWeapon() => CycleWeapon(-1);
    public void SelectNextWeapon() => CycleWeapon(1);

    public void SelectWeapon(int index)
    {
        Unit unit = GetContextUnit();
        UnitWeaponState state = GetOrCreateState(unit);
        if (state == null || state.weapons.Count == 0)
        {
            return;
        }

        state.selectedIndex = Mathf.Clamp(index, 0, state.weapons.Count - 1);
        EnsureValidSelection(unit);
        RefreshUi();
    }

    public IReadOnlyList<WeaponEntry> GetWeaponsForUnit(Unit unit)
    {
        UnitWeaponState state = GetOrCreateState(unit);
        return state != null ? state.weapons : Array.Empty<WeaponEntry>();
    }

    public WeaponEntry GetSelectedWeaponForUnit(Unit unit)
    {
        UnitWeaponState state = GetOrCreateState(unit);
        if (state == null || state.weapons.Count == 0)
        {
            return null;
        }

        EnsureValidSelection(unit);
        return state.weapons[Mathf.Clamp(state.selectedIndex, 0, state.weapons.Count - 1)];
    }

    public void RefreshUi()
    {
        bool shouldShowUi = ShouldShowInventoryUi();
        if (!shouldShowUi)
        {
            HideRuntimeUiDocument();
            HideLegacyUi();
            return;
        }

        if (HasLegacyUiReferences())
        {
            HideRuntimeUiDocument();
            RefreshLegacyUi();
            return;
        }

        ShowRuntimeUiDocument();

        if (!_uiInitialized)
        {
            InitializeRuntimeUi();
        }

        if (!HasRuntimeUiReferences())
        {
            ClearRuntimeUiCache();
            InitializeRuntimeUi();
        }

        if (!HasRuntimeUiReferences())
        {
            RefreshLegacyUi();
            return;
        }

        HideLegacyUi();

        IReadOnlyList<WeaponEntry> currentWeapons = GetCurrentWeapons();
        if (_slotViews.Count != currentWeapons.Count)
        {
            BuildSlots();
        }
        _inventoryRoot.style.display = DisplayStyle.Flex;

        WeaponEntry selected = GetSelectedWeaponForUnit(GetContextUnit());
        Color selectedAccent = GetWeaponAccent(selected);

        _weaponNameLabel.text = selected != null ? selected.displayName : "No Weapon";
        _weaponDescriptionLabel.text = FormatDescription(selected != null ? selected.description : "Add a projectile prefab to the inventory.");
        _ammoValueLabel.text = selected == null ? "--" : (selected.ammo < 0 ? "INF" : selected.ammo.ToString());
        _ammoValueLabel.style.color = new StyleColor(selectedAccent);
        if (_ammoChip != null)
        {
            _ammoChip.style.borderTopColor = new StyleColor(selectedAccent);
        }

        for (int i = 0; i < _slotViews.Count; i++)
        {
            SlotView slot = _slotViews[i];
            WeaponEntry weapon = currentWeapons[i];
            bool selectedSlot = selected != null && weapon == selected;
            bool usable = CanUseWeapon(weapon);
            Color accent = GetWeaponAccent(weapon);

            slot.button.SetEnabled(usable || selectedSlot);
            slot.button.style.marginRight = 0f;
            slot.button.EnableInClassList("weapon-slot--selected", selectedSlot);
            slot.button.EnableInClassList("weapon-slot--disabled", !usable);
            slot.button.style.backgroundColor = new StyleColor(selectedSlot
                ? Color.Lerp(new Color(0.24f, 0.27f, 0.35f, 0.6f), accent, 0.18f)
                : (usable ? new Color(0.2f, 0.23f, 0.3f, 0.42f) : new Color(0.14f, 0.16f, 0.22f, 0.22f)));
            slot.button.style.borderBottomColor = new StyleColor(selectedSlot ? WithAlpha(accent, 0.85f) : new Color(1f, 1f, 1f, usable ? 0.18f : 0.08f));
            slot.button.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, usable ? 0.14f : 0.08f));
            slot.button.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, usable ? 0.14f : 0.08f));
            slot.button.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, usable ? 0.18f : 0.08f));

            slot.accent.style.backgroundColor = new StyleColor(selectedSlot ? accent : WithAlpha(accent, usable ? 0.6f : 0.28f));
            slot.hotkey.text = (i + 1).ToString();
            slot.title.text = weapon != null ? weapon.displayName : "Empty";
            slot.detail.text = string.Empty;

            slot.hotkey.style.color = new StyleColor(usable ? Color.white : new Color(0.62f, 0.66f, 0.72f, 0.9f));
            slot.title.style.color = new StyleColor(usable ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.9f));
            slot.detail.style.display = DisplayStyle.None;
        }
    }

    bool HasRuntimeUiReferences()
    {
        return _uiInitialized &&
               _inventoryRoot != null &&
               _inventoryShell != null &&
               _slotsContainer != null &&
               _weaponNameLabel != null &&
               _weaponDescriptionLabel != null &&
               _ammoChip != null &&
               _ammoValueLabel != null;
    }

    void ClearRuntimeUiCache()
    {
        _uiInitialized = false;
        _slotViews.Clear();
        _inventoryRoot = null;
        _inventoryShell = null;
        _ammoChip = null;
        _slotsContainer = null;
        _weaponNameLabel = null;
        _weaponDescriptionLabel = null;
        _ammoValueLabel = null;
    }

    void InitializeRuntimeUi()
    {
        ClearRuntimeUiCache();

        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            _uiDocument = gameObject.AddComponent<UIDocument>();
        }

        _panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
        if (_panelSettings == null)
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        }

        _runtimeTheme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
        if (_runtimeTheme != null && _panelSettings.themeStyleSheet != _runtimeTheme)
        {
            _panelSettings.themeStyleSheet = _runtimeTheme;
        }

        _layoutAsset = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
        _styleSheet = Resources.Load<StyleSheet>(LayoutResourcePath);

        _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        _panelSettings.scale = 1f;
        _panelSettings.sortingOrder = VisibleSortingOrder;
        _uiDocument.panelSettings = _panelSettings;
        _uiDocument.visualTreeAsset = _layoutAsset;
        _uiDocument.sortingOrder = VisibleSortingOrder;

        UITKVisual root = _uiDocument.rootVisualElement;
        if (root == null)
        {
            _uiInitialized = false;
            return;
        }

        root.pickingMode = PickingMode.Ignore;

        if (_styleSheet != null)
        {
            root.styleSheets.Remove(_styleSheet);
            root.styleSheets.Add(_styleSheet);
        }

        if (root.childCount == 0)
        {
            root.Clear();
            if (_layoutAsset != null)
            {
                _layoutAsset.CloneTree(root);
            }

            if (root.childCount == 0)
            {
                BuildFallbackTree(root);
            }
        }

        CacheUiReferences(root);
        ApplyDocumentStyles();
        _uiInitialized = _inventoryRoot != null && _inventoryShell != null && _slotsContainer != null && _weaponNameLabel != null && _weaponDescriptionLabel != null && _ammoChip != null && _ammoValueLabel != null;

        if (_uiInitialized)
        {
            BuildSlots();
            HideLegacyUi();
        }
    }

    void CacheUiReferences(UITKVisual root)
    {
        _inventoryRoot = root.Q<UITKVisual>("inventory-root");
        _inventoryShell = root.Q<UITKVisual>("inventory-shell");
        _ammoChip = root.Q<UITKVisual>("ammo-chip");
        _slotsContainer = root.Q<UITKVisual>("inventory-slots");
        _weaponNameLabel = root.Q<UITKLabel>("weapon-name");
        _weaponDescriptionLabel = root.Q<UITKLabel>("weapon-description");
        _ammoValueLabel = root.Q<UITKLabel>("ammo-value");

        if (_inventoryShell != null)
        {
            _inventoryShell.pickingMode = PickingMode.Position;
        }

        if (_slotsContainer != null)
        {
            _slotsContainer.pickingMode = PickingMode.Position;
        }

        if (_ammoChip != null)
        {
            _ammoChip.pickingMode = PickingMode.Ignore;
        }

        if (_weaponNameLabel != null)
        {
            _weaponNameLabel.pickingMode = PickingMode.Ignore;
        }

        if (_weaponDescriptionLabel != null)
        {
            _weaponDescriptionLabel.pickingMode = PickingMode.Ignore;
        }

        if (_ammoValueLabel != null)
        {
            _ammoValueLabel.pickingMode = PickingMode.Ignore;
        }
    }

    void ApplyDocumentStyles()
    {
        if (_inventoryRoot == null || _inventoryShell == null || _slotsContainer == null || _weaponNameLabel == null || _weaponDescriptionLabel == null || _ammoChip == null || _ammoValueLabel == null)
        {
            return;
        }

        _inventoryRoot.style.position = Position.Absolute;
        _inventoryRoot.style.left = 0f;
        _inventoryRoot.style.right = 0f;
        _inventoryRoot.style.top = 0f;
        _inventoryRoot.style.bottom = 0f;
        _inventoryRoot.style.justifyContent = Justify.FlexEnd;
        _inventoryRoot.style.alignItems = Align.FlexEnd;
        _inventoryRoot.style.paddingRight = 14f;
        _inventoryRoot.style.paddingBottom = 12f;

        _inventoryShell.style.width = 530f;
        _inventoryShell.style.minHeight = 92f;
        _inventoryShell.style.paddingTop = 8f;
        _inventoryShell.style.paddingRight = 10f;
        _inventoryShell.style.paddingBottom = 8f;
        _inventoryShell.style.paddingLeft = 10f;
        _inventoryShell.style.overflow = Overflow.Hidden;
        _inventoryShell.style.backgroundColor = new Color(0.18f, 0.22f, 0.29f, 0.48f);
        _inventoryShell.style.borderTopWidth = 2f;
        _inventoryShell.style.borderRightWidth = 1f;
        _inventoryShell.style.borderBottomWidth = 1f;
        _inventoryShell.style.borderLeftWidth = 1f;
        _inventoryShell.style.borderTopColor = new Color(1f, 1f, 1f, 0.22f);
        _inventoryShell.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
        _inventoryShell.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        _inventoryShell.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);

        UITKVisual header = _inventoryShell.Q<UITKVisual>("inventory-header");
        UITKVisual copy = _inventoryShell.Q<UITKVisual>("inventory-copy");
        if (header != null)
        {
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 28f;
            header.style.marginBottom = 8f;
            header.style.paddingBottom = 6f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
        }

        if (copy != null)
        {
            copy.style.flexGrow = 1f;
            copy.style.marginRight = 10f;
            copy.style.alignItems = Align.FlexStart;
            copy.style.maxWidth = 280f;
            copy.style.overflow = Overflow.Hidden;
        }

        RuntimeUiFont.Apply(_inventoryShell);
        RuntimeUiFont.Apply(_weaponNameLabel, true);
        RuntimeUiFont.Apply(_weaponDescriptionLabel);
        RuntimeUiFont.Apply(_ammoValueLabel, true);

        _weaponNameLabel.style.color = Color.white;
        _weaponNameLabel.style.fontSize = 18f;
        _weaponNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _weaponNameLabel.style.marginBottom = 0f;
        _weaponNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        _weaponNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        _weaponNameLabel.style.overflow = Overflow.Hidden;
        _weaponNameLabel.style.textOverflow = TextOverflow.Ellipsis;
        _weaponNameLabel.style.maxWidth = 280f;

        _weaponDescriptionLabel.style.color = new Color(0.84f, 0.86f, 0.9f, 0.82f);
        _weaponDescriptionLabel.style.display = DisplayStyle.None;

        _ammoChip.style.width = 62f;
        _ammoChip.style.minHeight = 30f;
        _ammoChip.style.justifyContent = Justify.Center;
        _ammoChip.style.alignItems = Align.Center;
        _ammoChip.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
        _ammoChip.style.borderTopWidth = 2f;
        _ammoChip.style.borderRightWidth = 1f;
        _ammoChip.style.borderBottomWidth = 1f;
        _ammoChip.style.borderLeftWidth = 1f;
        _ammoChip.style.borderTopColor = new Color(1f, 1f, 1f, 0.34f);
        _ammoChip.style.borderRightColor = new Color(1f, 1f, 1f, 0.16f);
        _ammoChip.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
        _ammoChip.style.borderLeftColor = new Color(1f, 1f, 1f, 0.16f);

        _ammoValueLabel.style.fontSize = 20f;
        _ammoValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _ammoValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        _slotsContainer.style.flexDirection = FlexDirection.Row;
        _slotsContainer.style.alignItems = Align.Stretch;
        _slotsContainer.style.justifyContent = Justify.SpaceBetween;
        _slotsContainer.style.width = Length.Percent(100);
        _slotsContainer.style.overflow = Overflow.Hidden;
    }

    void BuildFallbackTree(UITKVisual root)
    {
        UITKVisual inventoryRoot = new() { name = "inventory-root" };
        UITKVisual inventoryShell = new() { name = "inventory-shell" };
        UITKVisual header = new() { name = "inventory-header" };
        UITKVisual copy = new() { name = "inventory-copy" };
        UITKLabel nameLabel = new("Weapon") { name = "weapon-name" };
        UITKLabel descriptionLabel = new("Description") { name = "weapon-description" };
        UITKVisual ammoChip = new() { name = "ammo-chip" };
        UITKLabel ammoValue = new("INF") { name = "ammo-value" };
        UITKVisual slots = new() { name = "inventory-slots" };

        root.Add(inventoryRoot);
        inventoryRoot.Add(inventoryShell);
        inventoryShell.Add(header);
        header.Add(copy);
        header.Add(ammoChip);
        inventoryShell.Add(slots);

        copy.Add(nameLabel);
        copy.Add(descriptionLabel);
        ammoChip.Add(ammoValue);
    }

    void BuildSlots()
    {
        _slotViews.Clear();

        if (_slotsContainer == null)
        {
            return;
        }

        _slotsContainer.Clear();

        IReadOnlyList<WeaponEntry> currentWeapons = GetCurrentWeapons();
        for (int i = 0; i < currentWeapons.Count; i++)
        {
            int index = i;

            UITKButton slotButton = new();
            slotButton.text = string.Empty;
            slotButton.focusable = false;
            slotButton.pickingMode = PickingMode.Position;
            slotButton.AddToClassList("weapon-slot");
            slotButton.clicked += () =>
            {
                if (!CanHandleSelectionInput())
                {
                    return;
                }

                SelectWeapon(index);
            };

            UITKVisual accent = new();
            accent.pickingMode = PickingMode.Ignore;
            accent.AddToClassList("weapon-slot__accent");

            UITKVisual content = new();
            content.pickingMode = PickingMode.Ignore;
            content.AddToClassList("weapon-slot__content");

            UITKLabel hotkey = new((i + 1).ToString());
            hotkey.pickingMode = PickingMode.Ignore;
            hotkey.AddToClassList("weapon-slot__hotkey");

            UITKLabel title = new();
            title.pickingMode = PickingMode.Ignore;
            title.AddToClassList("weapon-slot__title");

            UITKLabel detail = new();
            detail.pickingMode = PickingMode.Ignore;
            detail.AddToClassList("weapon-slot__detail");

            content.Add(hotkey);
            content.Add(title);
            content.Add(detail);

            slotButton.Add(accent);
            slotButton.Add(content);
            _slotsContainer.Add(slotButton);

            _slotViews.Add(new SlotView
            {
                button = slotButton,
                accent = accent,
                hotkey = hotkey,
                title = title,
                detail = detail
            });

            ApplySlotStyles(slotButton, accent, content, hotkey, title, detail);
        }
    }

    static void ApplySlotStyles(UITKButton slotButton, UITKVisual accent, UITKVisual content, UITKLabel hotkey, UITKLabel title, UITKLabel detail)
    {
        RuntimeUiFont.Apply(slotButton);
        RuntimeUiFont.Apply(hotkey, true);
        RuntimeUiFont.Apply(title, true);
        RuntimeUiFont.Apply(detail);

        slotButton.style.width = 116f;
        slotButton.style.height = 40f;
        slotButton.style.marginRight = 0f;
        slotButton.style.paddingLeft = 0f;
        slotButton.style.paddingRight = 0f;
        slotButton.style.paddingTop = 0f;
        slotButton.style.paddingBottom = 0f;
        slotButton.style.flexDirection = FlexDirection.Row;
        slotButton.style.alignItems = Align.Stretch;
        slotButton.style.justifyContent = Justify.FlexStart;
        slotButton.style.overflow = Overflow.Hidden;
        slotButton.style.backgroundColor = new Color(0.2f, 0.23f, 0.3f, 0.42f);
        slotButton.style.borderTopWidth = 1f;
        slotButton.style.borderRightWidth = 1f;
        slotButton.style.borderBottomWidth = 2f;
        slotButton.style.borderLeftWidth = 1f;
        slotButton.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
        slotButton.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
        slotButton.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
        slotButton.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);

        accent.style.width = 2f;
        accent.style.height = Length.Percent(100);
        accent.style.backgroundColor = new Color(0.47f, 0.71f, 0.98f, 0.6f);

        content.style.flexGrow = 1f;
        content.style.flexDirection = FlexDirection.Column;
        content.style.alignItems = Align.Stretch;
        content.style.justifyContent = Justify.Center;
        content.style.paddingLeft = 5f;
        content.style.paddingRight = 5f;
        content.style.paddingTop = 1f;
        content.style.paddingBottom = 1f;

        hotkey.style.fontSize = 9f;
        hotkey.style.marginBottom = 1f;
        hotkey.style.width = Length.Percent(100);
        hotkey.style.unityTextAlign = TextAnchor.MiddleCenter;

        title.style.fontSize = 11f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.width = Length.Percent(100);
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.overflow = Overflow.Hidden;
        title.style.textOverflow = TextOverflow.Clip;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;

        detail.style.display = DisplayStyle.None;
    }

    public bool IsPointerOverInteractiveUi(Vector2 screenPosition)
    {
        if (!_uiInitialized || _uiDocument == null || !_uiDocument.enabled || _inventoryShell == null)
        {
            return false;
        }

        IPanel panel = _inventoryShell.panel;
        if (panel == null)
        {
            return false;
        }

        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
        VisualElement picked = panel.Pick(panelPosition);
        for (VisualElement current = picked; current != null; current = current.parent)
        {
            if (current == _inventoryShell)
            {
                return true;
            }
        }

        return false;
    }

    void HideRuntimeUiDocument()
    {
        if (_inventoryRoot != null)
        {
            _inventoryRoot.style.display = DisplayStyle.None;
            _inventoryRoot.pickingMode = PickingMode.Ignore;
        }

        if (_uiDocument != null)
        {
            _uiDocument.sortingOrder = HiddenSortingOrder;
            _uiDocument.enabled = false;
            _uiDocument.panelSettings = null;
        }

        if (_panelSettings != null)
        {
            _panelSettings.sortingOrder = HiddenSortingOrder;
        }

        ClearRuntimeUiCache();
    }

    void ShowRuntimeUiDocument()
    {
        if (_uiDocument == null)
        {
            return;
        }

        if (_panelSettings != null)
        {
            _panelSettings.sortingOrder = VisibleSortingOrder;
        }

        if (_uiDocument.panelSettings != _panelSettings)
        {
            _uiDocument.panelSettings = _panelSettings;
            _uiInitialized = false;
        }

        _uiDocument.sortingOrder = VisibleSortingOrder;
        if (!_uiDocument.enabled)
        {
            _uiDocument.enabled = true;
            _uiInitialized = false;
        }

        if (_inventoryRoot != null)
        {
            _inventoryRoot.style.display = DisplayStyle.Flex;
            _inventoryRoot.pickingMode = PickingMode.Ignore;
        }
    }

    bool HasLegacyUiReferences()
    {
        return panel != null &&
               weaponNameText != null &&
               weaponDescriptionText != null &&
               ammoText != null &&
               slotsRoot != null;
    }

    void HideLegacyUi()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(false);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }

        if (slotButtonPrefab != null)
        {
            slotButtonPrefab.gameObject.SetActive(false);
        }
    }

    void RefreshLegacyUi()
    {
        if (!HasLegacyUiReferences())
        {
            HideLegacyUi();
            return;
        }

        panel.SetActive(true);

        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(false);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }

        if (slotButtonPrefab != null)
        {
            slotButtonPrefab.gameObject.SetActive(false);
        }

        WeaponEntry selected = GetSelectedWeaponForUnit(GetContextUnit());
        weaponNameText.text = selected != null ? selected.displayName : "No Weapon";
        weaponDescriptionText.text = string.Empty;
        ammoText.text = selected == null ? "Ammo: --" : $"Ammo: {(selected.ammo < 0 ? "INF" : selected.ammo.ToString())}";
        weaponNameText.fontStyle = FontStyle.Bold;
        ammoText.fontStyle = FontStyle.Bold;
        RuntimeUiFont.Apply(weaponNameText, true);
        RuntimeUiFont.Apply(weaponDescriptionText);
        RuntimeUiFont.Apply(ammoText, true);

        RectTransform panelRect = panel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-24f, 20f);
            panelRect.sizeDelta = new Vector2(620f, 184f);
        }

        RectTransform nameRect = weaponNameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(16f, -14f);
        nameRect.sizeDelta = new Vector2(344f, 34f);
        weaponNameText.fontSize = 22;
        weaponNameText.alignment = TextAnchor.MiddleLeft;
        weaponNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        weaponNameText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform ammoRect = ammoText.rectTransform;
        ammoRect.anchorMin = new Vector2(1f, 1f);
        ammoRect.anchorMax = new Vector2(1f, 1f);
        ammoRect.pivot = new Vector2(1f, 1f);
        ammoRect.anchoredPosition = new Vector2(-16f, -14f);
        ammoRect.sizeDelta = new Vector2(160f, 34f);
        ammoText.fontSize = 19;
        ammoText.alignment = TextAnchor.MiddleRight;
        ammoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        ammoText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform descriptionRect = weaponDescriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 1f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.pivot = new Vector2(0.5f, 1f);
        descriptionRect.anchoredPosition = new Vector2(0f, -52f);
        descriptionRect.sizeDelta = new Vector2(-32f, 44f);
        weaponDescriptionText.fontSize = 14;
        weaponDescriptionText.alignment = TextAnchor.UpperLeft;
        weaponDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        weaponDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;

        EnsureLegacySlots();

        IReadOnlyList<WeaponEntry> currentWeapons = GetCurrentWeapons();
        for (int i = 0; i < _legacySlotButtons.Count; i++)
        {
            UIButton button = _legacySlotButtons[i];
            UIText label = _legacySlotLabels[i];
            bool active = i < currentWeapons.Count;
            button.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            WeaponEntry weapon = currentWeapons[i];
            bool selectedSlot = selected != null && weapon == selected;
            bool usable = CanUseWeapon(weapon);
            string ammoValue = weapon.ammo < 0 ? "INF" : weapon.ammo.ToString();

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0f, 0f);
                buttonRect.anchorMax = new Vector2(0f, 0f);
                buttonRect.pivot = new Vector2(0f, 0f);
                buttonRect.anchoredPosition = new Vector2(16f + (i * 140f), 0f);
                buttonRect.sizeDelta = new Vector2(136f, 64f);
            }
            label.text = $"{i + 1}. {weapon.displayName}\n{ammoValue}";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 13;
            RuntimeUiFont.Apply(label, true);
            label.color = usable ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.9f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            UnityEngine.UI.Image image = button.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color accent = GetWeaponAccent(weapon);
                image.color = selectedSlot
                    ? Color.Lerp(new Color(0.24f, 0.27f, 0.35f, 0.68f), accent, 0.18f)
                    : (usable ? new Color(0.2f, 0.23f, 0.3f, 0.42f) : new Color(0.14f, 0.16f, 0.22f, 0.22f));
            }

            button.interactable = usable || selectedSlot;
            int index = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (CanHandleSelectionInput())
                {
                    SelectWeapon(index);
                }
            });
        }
    }

    void EnsureLegacySlots()
    {
        if (slotsRoot == null)
        {
            return;
        }

        _legacySlotButtons.Clear();
        _legacySlotLabels.Clear();

        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            Transform child = slotsRoot.GetChild(i);
            UIButton button = child.GetComponent<UIButton>();
            if (button == null)
            {
                continue;
            }

            if (slotButtonPrefab != null && child.gameObject == slotButtonPrefab.gameObject)
            {
                continue;
            }

            UIText label = child.GetComponentInChildren<UIText>(true);
            if (label == null)
            {
                continue;
            }

            _legacySlotButtons.Add(button);
            _legacySlotLabels.Add(label);
        }

        int currentWeaponsCount = GetCurrentWeapons().Count;
        while (_legacySlotButtons.Count < currentWeaponsCount && slotButtonPrefab != null)
        {
            UIButton button = Instantiate(slotButtonPrefab, slotsRoot);
            button.gameObject.name = $"WeaponSlot{_legacySlotButtons.Count + 1}";
            button.gameObject.SetActive(true);
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(16f + (_legacySlotButtons.Count * 140f), 0f);
                rect.sizeDelta = new Vector2(136f, 64f);
            }

            UIText label = button.GetComponentInChildren<UIText>(true);
            if (label != null)
            {
                _legacySlotButtons.Add(button);
                _legacySlotLabels.Add(label);
            }
            else
            {
                Destroy(button.gameObject);
                break;
            }
        }
    }

    void CycleWeapon(int direction)
    {
        Unit unit = GetContextUnit();
        UnitWeaponState state = GetOrCreateState(unit);
        if (state == null || state.weapons.Count == 0)
        {
            return;
        }

        int startIndex = state.selectedIndex;
        for (int i = 0; i < state.weapons.Count; i++)
        {
            state.selectedIndex = (state.selectedIndex + direction + state.weapons.Count) % state.weapons.Count;
            if (CanUseWeapon(state.weapons[state.selectedIndex]))
            {
                RefreshUi();
                return;
            }
        }

        state.selectedIndex = startIndex;
        RefreshUi();
    }

    void EnsureValidSelection(Unit unit)
    {
        UnitWeaponState state = GetOrCreateState(unit);
        if (state == null || state.weapons.Count == 0)
        {
            return;
        }

        state.selectedIndex = Mathf.Clamp(state.selectedIndex, 0, state.weapons.Count - 1);
        if (CanUseWeapon(state.weapons[state.selectedIndex]))
        {
            return;
        }

        SelectFirstUsableWeapon(unit);
    }

    void SelectFirstUsableWeapon(Unit unit)
    {
        UnitWeaponState state = GetOrCreateState(unit);
        if (state == null || state.weapons.Count == 0)
        {
            return;
        }

        for (int i = 0; i < state.weapons.Count; i++)
        {
            if (CanUseWeapon(state.weapons[i]))
            {
                state.selectedIndex = i;
                return;
            }
        }

        state.selectedIndex = 0;
    }

        IReadOnlyList<WeaponEntry> GetCurrentWeapons()
    {
        return GetWeaponsForUnit(GetContextUnit());
    }

    UnitWeaponState GetOrCreateState(Unit unit)
    {
        if (unit == null || unit.IsDead)
        {
            return null;
        }

        int teamKey = GetTeamKey(unit);
        if (_teamWeaponStates.TryGetValue(teamKey, out UnitWeaponState existingState))
        {
            return existingState;
        }

        UnitWeaponState state = new();
        foreach (WeaponEntry template in weapons)
        {
            state.weapons.Add(CloneWeapon(template));
        }

        if (state.weapons.Count > 0)
        {
            state.selectedIndex = Mathf.Clamp(FindFirstUsableWeaponIndex(state.weapons), 0, state.weapons.Count - 1);
        }

        _teamWeaponStates[teamKey] = state;
        return state;
    }

    void CleanupUnitStates()
    {
    }

    int GetTeamKey(Unit unit)
    {
        return unit != null && !unit.isPlayerTeam ? 1 : 0;
    }

    Unit GetContextUnit()
    {
        if (TurnManager.Instance != null)
        {
            Unit activeUnit = TurnManager.Instance.GetActiveUnitForCurrentTurn();
            if (activeUnit != null && !activeUnit.IsDead)
            {
                return activeUnit;
            }
        }

        if (GameManager.Instance == null)
        {
            return null;
        }

        if (GameManager.Instance.IsEnemyTurn)
        {
            return GetFirstAliveUnit(GameManager.Instance.enemyUnits) ?? GetFirstAliveUnit(GameManager.Instance.playerUnits);
        }

        return GetFirstAliveUnit(GameManager.Instance.playerUnits) ?? GetFirstAliveUnit(GameManager.Instance.enemyUnits);
    }

    static Unit GetFirstAliveUnit(List<Unit> units)
    {
        if (units == null)
        {
            return null;
        }

        foreach (Unit unit in units)
        {
            if (unit != null && !unit.IsDead)
            {
                return unit;
            }
        }

        return null;
    }

    int FindFirstUsableWeaponIndex(IReadOnlyList<WeaponEntry> runtimeWeapons)
    {
        if (runtimeWeapons == null || runtimeWeapons.Count == 0)
        {
            return 0;
        }

        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            if (CanUseWeapon(runtimeWeapons[i]))
            {
                return i;
            }
        }

        return 0;
    }

    static WeaponEntry CloneWeapon(WeaponEntry template)
    {
        if (template == null)
        {
            return null;
        }

        return new WeaponEntry
        {
            displayName = template.displayName,
            description = template.description,
            projectilePrefab = template.projectilePrefab,
            ammo = template.ammo,
            accentColor = template.accentColor
        };
    }

    bool CanUseWeapon(WeaponEntry weapon)
    {
        return weapon != null && weapon.projectilePrefab != null && weapon.ammo != 0;
    }

    void NormalizeWeaponEntries()
    {
        foreach (WeaponEntry weapon in weapons)
        {
            if (weapon == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(weapon.displayName) &&
                weapon.displayName.Trim().Equals("Bouncer", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Slider";
                weapon.description = "Skips and rolls off surfaces before detonating. Best for low tunnels and ground pressure.";
            }

            if (!string.IsNullOrWhiteSpace(weapon.displayName) &&
                weapon.displayName.Trim().Equals("Heavy Bomb", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Nuke";
            }

            string prefabName = weapon.projectilePrefab != null ? weapon.projectilePrefab.name : string.Empty;
            if (weapon.displayName.Equals("Cannonball", StringComparison.OrdinalIgnoreCase) || prefabName.Equals("Cannonball", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Cannonball";
                weapon.description = "Reliable standard shell. Direct hits deal 20 damage. Infinite ammo.";
                weapon.ammo = -1;
            }
            else if (weapon.displayName.Equals("Nuke", StringComparison.OrdinalIgnoreCase) || prefabName.Equals("Nuke", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Nuke";
                weapon.description = "Heavy shell. Direct hits deal 30 damage and the shrapnel bursts for 5 each.";
                weapon.ammo = 1;
            }
            else if (weapon.displayName.Equals("Cluster Bomb", StringComparison.OrdinalIgnoreCase) || prefabName.Equals("ClusterBomb", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Cluster Bomb";
                weapon.description = "Splits in mid air into bomblets that each deal 10 damage on direct hit.";
                weapon.ammo = 2;
            }
            else if (weapon.displayName.Equals("Roller", StringComparison.OrdinalIgnoreCase) ||
                     weapon.displayName.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                     prefabName.Equals("Roller", StringComparison.OrdinalIgnoreCase))
            {
                weapon.displayName = "Slider";
                weapon.description = "The only shell that skips and rolls. Great for low tunnels and floor pressure.";
                weapon.ammo = 2;
            }
        }
    }

    bool CanHandleSelectionInput()
    {
        if (GameManager.Instance == null)
        {
            return true;
        }

        if (MainMenuController.IsMenuOpen)
        {
            return false;
        }

        if (GameManager.Instance.IsGameOver)
        {
            return false;
        }

        return GameManager.Instance.IsCurrentTurnHumanControlled && !GameManager.Instance.IsTurnTransitioning && !GameManager.Instance.HasActiveProjectiles;
    }

    float ReadScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.scroll.ReadValue().y;
        }
        return 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    bool ReadPreviousPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.qKey.wasPressedThisFrame || keyboard.leftBracketKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftBracket);
#endif
    }

    bool ReadNextPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.eKey.wasPressedThisFrame || keyboard.rightBracketKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightBracket);
#endif
    }

    int ReadWeaponHotkeyIndex()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        }
        return -1;
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
        return -1;
#endif
    }

    Color GetWeaponAccent(WeaponEntry weapon)
    {
        if (weapon == null)
        {
            return new Color(0.45f, 0.78f, 0.97f, 1f);
        }

        Color accent = weapon.accentColor;
        if (accent.a <= 0f)
        {
            accent = new Color(0.45f, 0.78f, 0.97f, 1f);
        }

        accent.a = 1f;
        return accent;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    static string FormatDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "No details available.";
        }

        string formatted = description.Trim();
        int endIndex = formatted.IndexOf('.');
        if (endIndex < 0)
        {
            endIndex = formatted.IndexOf('!');
        }
        if (endIndex < 0)
        {
            endIndex = formatted.IndexOf('?');
        }

        return endIndex >= 0 ? formatted[..(endIndex + 1)] : formatted;
    }

    static bool ShouldShowInventoryUi()
    {
        if (MainMenuController.IsMenuOpen)
        {
            return false;
        }

        return GameManager.Instance == null || !GameManager.Instance.IsGameOver;
    }
}













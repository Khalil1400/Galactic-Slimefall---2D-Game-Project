#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameplayHudSceneAuthoring
{
    const string SampleScenePath = "Assets/GalacticSlimefall/Scenes/SampleScene.unity";

    static GameplayHudSceneAuthoring()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall -= EnsureActiveSceneHud;
        EditorApplication.delayCall += EnsureActiveSceneHud;
    }

    [MenuItem("Tools/Galactic Slimefall/Ensure Scene HUD")]
    static void EnsureActiveSceneHud()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != SampleScenePath)
        {
            return;
        }

        EnsureSceneHud(scene);
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != SampleScenePath)
        {
            return;
        }

        EnsureSceneHud(scene);
    }

    static void EnsureSceneHud(Scene scene)
    {
        GameObject canvasObject = FindRoot(scene, "Canvas");
        if (canvasObject == null)
        {
            return;
        }

        UIManager uiManager = canvasObject.GetComponent<UIManager>();
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        WeaponInventory inventory = Object.FindFirstObjectByType<WeaponInventory>();
        if (uiManager == null || canvas == null || inventory == null)
        {
            return;
        }

        bool changed = false;
        Transform canvasTransform = canvas.transform;
        Transform healthBarsRoot = FindDirectChild(canvasTransform, "HealthBarsRoot");

        GameObject turnPanel = FindOrCreatePanel(canvasTransform, "TurnPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(320f, 110f), ref changed);
        Image turnPanelImage = EnsureImage(turnPanel, ref changed);
        turnPanelImage.color = Color.clear;
        turnPanelImage.enabled = false;
        turnPanelImage.raycastTarget = false;
        Text turnText = FindOrCreateText(turnPanel.transform, "TurnText", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Your Turn", 22, FontStyle.Bold, TextAnchor.MiddleCenter, ref changed);
        turnText.rectTransform.offsetMin = new Vector2(10f, 10f);
        turnText.rectTransform.offsetMax = new Vector2(-10f, -10f);

        GameObject timerPanel = FindOrCreatePanel(canvasTransform, "TurnTimerPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(126f, 62f), ref changed);
        Image timerImage = EnsureImage(timerPanel, ref changed);
        timerImage.color = Color.clear;
        timerImage.enabled = false;
        timerImage.raycastTarget = false;
        Text timerLabel = FindOrCreateText(timerPanel.transform, "TimerLabel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, 14f), "Round Time", 10, FontStyle.Bold, TextAnchor.UpperCenter, ref changed);
        timerLabel.color = new Color(0.86f, 0.9f, 0.98f, 0.92f);
        Text timerText = FindOrCreateText(timerPanel.transform, "TimerText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "20", 30, FontStyle.Bold, TextAnchor.MiddleCenter, ref changed);
        timerText.rectTransform.offsetMin = new Vector2(0f, 0f);
        timerText.rectTransform.offsetMax = new Vector2(0f, -18f);

        GameObject matchModeButton = FindOrCreateButton(turnPanel.transform, "MatchModeButton", "Mode: VS AI", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(170f, 28f), ref changed);
        matchModeButton.SetActive(false);
        Button matchModeButtonComponent = matchModeButton.GetComponent<Button>();
        Text matchModeButtonText = matchModeButton.GetComponentInChildren<Text>(true);

        GameObject gameOverPanel = FindOrCreatePanel(canvasTransform, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 240f), ref changed);
        Image gameOverImage = EnsureImage(gameOverPanel, ref changed);
        gameOverImage.color = Color.clear;
        gameOverImage.enabled = false;
        gameOverImage.raycastTarget = false;
        Text gameOverText = FindOrCreateText(gameOverPanel.transform, "GameOverText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(320f, 80f), "Victory!", 42, FontStyle.Bold, TextAnchor.MiddleCenter, ref changed);
        GameObject restartButton = FindOrCreateButton(gameOverPanel.transform, "RestartButton", "Restart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96f, -70f), new Vector2(170f, 52f), ref changed);
        GameObject mainMenuButton = FindOrCreateButton(gameOverPanel.transform, "MainMenuButton", "Main Menu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, -70f), new Vector2(170f, 52f), ref changed);
        gameOverPanel.SetActive(false);

        GameObject inventoryPanel = FindOrCreatePanel(canvasTransform, "InventoryPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(620f, 184f), ref changed);
        Image inventoryImage = EnsureImage(inventoryPanel, ref changed);
        inventoryImage.color = new Color(0.18f, 0.22f, 0.29f, 0.48f);
        inventoryImage.enabled = true;
        inventoryImage.raycastTarget = false;

        Text weaponName = FindOrCreateText(inventoryPanel.transform, "WeaponName", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(344f, 34f), "Cannonball", 22, FontStyle.Bold, TextAnchor.MiddleLeft, ref changed);
        Text weaponAmmo = FindOrCreateText(inventoryPanel.transform, "WeaponAmmo", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(160f, 34f), "Ammo: INF", 19, FontStyle.Bold, TextAnchor.MiddleRight, ref changed);
        Text weaponDescription = FindOrCreateText(inventoryPanel.transform, "WeaponDescription", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(-32f, 44f), string.Empty, 14, FontStyle.Normal, TextAnchor.UpperLeft, ref changed);
        Transform weaponSlots = FindOrCreateSlotsRoot(inventoryPanel.transform, ref changed);
        Button previousButton = FindOrCreateButton(inventoryPanel.transform, "WeaponPrevButton", "<", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -6f), new Vector2(28f, 28f), ref changed).GetComponent<Button>();
        Button nextButton = FindOrCreateButton(inventoryPanel.transform, "WeaponNextButton", ">", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -6f), new Vector2(28f, 28f), ref changed).GetComponent<Button>();
        previousButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        Button slotTemplate = FindOrCreateSlotTemplate(weaponSlots, ref changed);
        EnsureSceneSlots(weaponSlots, slotTemplate, 4, ref changed);

        uiManager.turnPanel = turnPanel;
        uiManager.turnText = turnText;
        uiManager.timerText = timerText;
        uiManager.matchModeButton = matchModeButtonComponent;
        uiManager.matchModeButtonText = matchModeButtonText;
        uiManager.gameOverPanel = gameOverPanel;
        uiManager.gameOverText = gameOverText;
        uiManager.restartButton = restartButton.GetComponent<Button>();
        uiManager.mainMenuButton = mainMenuButton.GetComponent<Button>();
        if (healthBarsRoot != null)
        {
            uiManager.hpBarsRoot = healthBarsRoot;
        }

        inventory.panel = inventoryPanel;
        inventory.weaponNameText = weaponName;
        inventory.weaponDescriptionText = weaponDescription;
        inventory.ammoText = weaponAmmo;
        inventory.previousButton = previousButton;
        inventory.nextButton = nextButton;
        inventory.slotsRoot = weaponSlots;
        inventory.slotButtonPrefab = slotTemplate;

        EditorUtility.SetDirty(uiManager);
        EditorUtility.SetDirty(inventory);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    static GameObject FindOrCreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, ref bool changed)
    {
        Transform existing = FindDirectChild(parent, name);
        GameObject panel = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null)
        {
            panel.transform.SetParent(parent, false);
            changed = true;
        }

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return panel;
    }

    static Transform FindOrCreateSlotsRoot(Transform panelTransform, ref bool changed)
    {
        GameObject slotsPanel = FindOrCreatePanel(panelTransform, "WeaponSlots", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-20f, 88f), ref changed);
        Image image = EnsureImage(slotsPanel, ref changed);
        image.color = Color.clear;
        image.enabled = false;
        image.raycastTarget = false;
        return slotsPanel.transform;
    }

    static Button FindOrCreateSlotTemplate(Transform slotsRoot, ref bool changed)
    {
        Transform existing = FindDirectChild(slotsRoot, "WeaponSlotTemplate");
        GameObject template = existing != null ? existing.gameObject : CreateButtonObject("WeaponSlotTemplate", slotsRoot, ref changed);
        RectTransform rect = template.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(136f, 64f);
        template.SetActive(false);
        Text label = template.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "1. Weapon\nINF";
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(6f, 4f);
            label.rectTransform.offsetMax = new Vector2(-6f, -4f);
        }
        return template.GetComponent<Button>();
    }

    static void EnsureSceneSlots(Transform slotsRoot, Button slotTemplate, int count, ref bool changed)
    {
        for (int i = 0; i < count; i++)
        {
            string name = $"WeaponSlot{i + 1}";
            Transform existing = FindDirectChild(slotsRoot, name);
            GameObject slot = existing != null ? existing.gameObject : null;
            if (slot == null)
            {
                slot = Object.Instantiate(slotTemplate.gameObject, slotsRoot, false);
                slot.name = name;
                changed = true;
            }

            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f + (i * 140f), 0f);
            rect.sizeDelta = new Vector2(136f, 64f);
            Text slotLabel = slot.GetComponentInChildren<Text>(true);
            if (slotLabel != null)
            {
                slotLabel.fontSize = 13;
                slotLabel.alignment = TextAnchor.MiddleCenter;
                slotLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                slotLabel.verticalOverflow = VerticalWrapMode.Truncate;
                slotLabel.rectTransform.anchorMin = Vector2.zero;
                slotLabel.rectTransform.anchorMax = Vector2.one;
                slotLabel.rectTransform.offsetMin = new Vector2(6f, 4f);
                slotLabel.rectTransform.offsetMax = new Vector2(-6f, -4f);
            }

            slot.SetActive(true);
        }
    }

    static Text FindOrCreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, string value, int fontSize, FontStyle fontStyle, TextAnchor alignment, ref bool changed)
    {
        Transform existing = FindDirectChild(parent, name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        if (existing == null)
        {
            textObject.transform.SetParent(parent, false);
            changed = true;
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.material = null;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static GameObject FindOrCreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, ref bool changed)
    {
        Transform existing = FindDirectChild(parent, name);
        GameObject buttonObject = existing != null ? existing.gameObject : CreateButtonObject(name, parent, ref changed);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = buttonObject.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }

        return buttonObject;
    }

    static GameObject CreateButtonObject(string name, Transform parent, ref bool changed)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        changed = true;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        bool textChanged = false;
        Text label = FindOrCreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleCenter, ref textChanged);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.color = new Color(0.985f, 0.99f, 1f, 1f);
        if (textChanged)
        {
            changed = true;
        }

        return buttonObject;
    }

    static Image EnsureImage(GameObject gameObject, ref bool changed)
    {
        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
            changed = true;
        }

        return image;
    }
}
#endif











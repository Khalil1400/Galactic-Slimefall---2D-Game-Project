using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    const string MainMenuSceneName = "MainMenu";
    const string GameplaySceneName = "SampleScene";
    const string BackgroundResourcePath = "MainMenu/GalacticSlimefall";

    static MainMenuController _instance;
    static bool _hasPendingMatchMode;
    static bool _hasSessionMatchMode;
    static GameManager.MatchMode _pendingMatchMode = GameManager.MatchMode.VsAi;
    static GameManager.MatchMode _sessionMatchMode = GameManager.MatchMode.VsAi;
    static Sprite _backgroundSprite;
    static Sprite _whiteSprite;

    public static bool IsMenuOpen { get; private set; }

    CanvasGroup _overlayGroup;
    GameObject _overlay;
    GameObject _panel;
    Text _titleText;
    Button _botsButton;
    Button _humansButton;
    Button _resumeButton;
    Button _mainMenuButton;
    Button _exitButton;
    bool _playRequested;
    bool _isMainMenuScene;
    bool _pendingModeApplied;
    bool _sceneEventsSubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureMenu()
    {
        if (!Application.isPlaying || _instance != null)
        {
            return;
        }

        GameObject root = new GameObject("MainMenuController");
        _instance = root.AddComponent<MainMenuController>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SubscribeSceneEvents();
    }

    void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        UnsubscribeSceneEvents();

        if (_instance == this)
        {
            Time.timeScale = 1f;
            IsMenuOpen = false;
            _instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneLoaded(scene, mode);
    }

    void Update()
    {
        if (_overlay != null && IsMenuOpen)
        {
            _overlay.transform.SetAsLastSibling();
        }

        if (_isMainMenuScene)
        {
            HandleMainMenuShortcuts();
            return;
        }

        TryApplyPendingMatchMode();

        if (!IsMenuOpen || _playRequested)
        {
            if (ShouldTogglePauseMenu())
            {
                ShowPauseMenu();
            }
            return;
        }

        if (ShouldTogglePauseMenu())
        {
            OnResumeClicked();
        }
    }

    void BuildOverlay()
    {
        TearDownOverlay();
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        _overlay = new GameObject("MainMenuOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _overlay.transform.SetParent(canvasObject.transform, false);
        RectTransform overlayRect = _overlay.GetComponent<RectTransform>();
        Stretch(overlayRect, Vector2.zero, Vector2.zero);

        Image overlayImage = _overlay.GetComponent<Image>();
        overlayImage.color = _isMainMenuScene
            ? new Color(0.01f, 0.02f, 0.05f, 1f)
            : new Color(0.01f, 0.02f, 0.05f, 0.82f);

        _overlayGroup = _overlay.GetComponent<CanvasGroup>();
        _overlayGroup.alpha = 1f;
        _overlayGroup.interactable = true;
        _overlayGroup.blocksRaycasts = true;

        if (_isMainMenuScene)
        {
            BuildMainMenuLayout();
            return;
        }

        BuildPauseLayout();
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _playRequested = false;
        _pendingModeApplied = false;

        string activeSceneName = scene.name;
        _isMainMenuScene = IsScene(activeSceneName, MainMenuSceneName);
        if (ShouldRedirectFreshGameplaySession(activeSceneName))
        {
            SceneManager.LoadScene(MainMenuSceneName);
            return;
        }

        if (_isMainMenuScene)
        {
            EnsureMenuCamera();
        }

        BuildOverlay();

        if (_isMainMenuScene)
        {
            ShowMainMenu();
        }
        else
        {
            HideOverlayInstant();
            TryApplyPendingMatchMode();
        }
    }

    void BuildMainMenuLayout()
    {
        CreateBackgroundImage(_overlay.transform);

        _panel = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(_overlay.transform, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(560f, 308f);

        Image panelImage = _panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0f);


        _titleText = null;

        _botsButton = CreateButton(_panel.transform, "Play VS Bots", new Vector2(0f, 58f), new Vector2(444f, 66f));
        _botsButton.onClick.AddListener(() => OnPlayClicked(GameManager.MatchMode.VsAi));

        _humansButton = CreateButton(_panel.transform, "Play VS Human", new Vector2(0f, -26f), new Vector2(444f, 66f));
        _humansButton.onClick.AddListener(() => OnPlayClicked(GameManager.MatchMode.Hotseat));

        _exitButton = CreateButton(_panel.transform, "Exit", new Vector2(0f, -110f), new Vector2(444f, 62f));
        _exitButton.onClick.AddListener(OnExitClicked);
        SetButtonLabelSize(_exitButton, 38);
    }

    void BuildPauseLayout()
    {
        _panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(_overlay.transform, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(560f, 340f);

        Image panelImage = _panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0f);
        panelImage.enabled = false;


        _titleText = CreateText("Title", _panel.transform, "Paused", 52, FontStyle.Bold, new Vector2(0.5f, 0.82f), new Vector2(460f, 68f));

        _resumeButton = CreateButton(_panel.transform, "Resume", new Vector2(0f, 16f), new Vector2(390f, 64f));
        _resumeButton.onClick.AddListener(OnResumeClicked);
        SetButtonLabelSize(_resumeButton, 38);

        _mainMenuButton = CreateButton(_panel.transform, "Main Menu", new Vector2(0f, -62f), new Vector2(390f, 64f));
        _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        SetButtonLabelSize(_mainMenuButton, 38);

        _exitButton = CreateButton(_panel.transform, "Exit Game", new Vector2(0f, -140f), new Vector2(390f, 64f));
        _exitButton.onClick.AddListener(OnExitClicked);
        SetButtonLabelSize(_exitButton, 38);
    }

    void CreateBackgroundImage(Transform parent)
    {
        Sprite backgroundSprite = LoadBackgroundSprite();
        if (backgroundSprite == null)
        {
            return;
        }

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        background.transform.SetParent(parent, false);
        RectTransform rect = background.GetComponent<RectTransform>();
        Stretch(rect, Vector2.zero, Vector2.zero);

        Image image = background.GetComponent<Image>();
        image.sprite = backgroundSprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
    }

    void ShowMainMenu()
    {
        if (!_isMainMenuScene)
        {
            return;
        }

        ShowOverlay(false);
    }

    void ShowPauseMenu()
    {
        if (_isMainMenuScene)
        {
            return;
        }

        ShowOverlay(true);
    }

    void ShowOverlay(bool pauseTime)
    {
        if (_overlay == null)
        {
            return;
        }

        _overlay.transform.SetAsLastSibling();
        _overlay.SetActive(true);

        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 1f;
            _overlayGroup.interactable = true;
            _overlayGroup.blocksRaycasts = true;
        }

        Time.timeScale = pauseTime ? 0f : 1f;
        IsMenuOpen = true;
        AimingSystem.Instance?.DisableAiming();
        WeaponInventory.Instance?.RefreshUi();
    }

    void HideOverlayInstant()
    {
        HideOverlay(false);
    }

    void HideOverlay(bool refreshGameplay = true)
    {
        Time.timeScale = 1f;
        IsMenuOpen = false;

        if (_overlayGroup != null)
        {
            _overlayGroup.alpha = 0f;
            _overlayGroup.interactable = false;
            _overlayGroup.blocksRaycasts = false;
        }

        if (_overlay != null)
        {
            _overlay.SetActive(false);
        }

        if (refreshGameplay)
        {
            AimingSystem.Instance?.EnableAiming();
            WeaponInventory.Instance?.RefreshUi();
        }
    }

    void OnPlayClicked(GameManager.MatchMode mode)
    {
        if (_playRequested)
        {
            return;
        }

        GalacticSlimefallAudio.PlayUiClick();
        _playRequested = true;
        _hasPendingMatchMode = true;
        _pendingMatchMode = mode;
        _hasSessionMatchMode = true;
        _sessionMatchMode = mode;
        HideOverlay(false);
        SceneManager.LoadScene(GameplaySceneName);
    }

    void OnResumeClicked()
    {
        if (_playRequested)
        {
            return;
        }

        GalacticSlimefallAudio.PlayUiClick();
        HideOverlay();
    }

    void OnMainMenuClicked()
    {
        if (_playRequested)
        {
            return;
        }

        GalacticSlimefallAudio.PlayUiClick();
        _playRequested = true;
        HideOverlay(false);
        SceneManager.LoadScene(MainMenuSceneName);
    }

    void OnExitClicked()
    {
        GalacticSlimefallAudio.PlayUiClick();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void TryApplyPendingMatchMode()
    {
        if (_isMainMenuScene || _pendingModeApplied || GameManager.Instance == null)
        {
            return;
        }

        if (_hasPendingMatchMode)
        {
            _sessionMatchMode = _pendingMatchMode;
            _hasSessionMatchMode = true;
            GameManager.Instance.SetMatchMode(_pendingMatchMode);
            _hasPendingMatchMode = false;
            _pendingModeApplied = true;
            return;
        }

        if (_hasSessionMatchMode)
        {
            GameManager.Instance.SetMatchMode(_sessionMatchMode);
        }

        _pendingModeApplied = true;
    }

    void HandleMainMenuShortcuts()
    {
        if (_playRequested)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            OnPlayClicked(GameManager.MatchMode.VsAi);
        }
#endif
    }

    static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        Text labelText = CreateText("Label", buttonObject.transform, label, 34, FontStyle.Bold, new Vector2(0.5f, 0.5f), size - new Vector2(12f, 8f));
        labelText.rectTransform.anchoredPosition = Vector2.zero;
        labelText.color = new Color(0.985f, 0.99f, 1f, 1f);
        AddHoverGlow(buttonObject, labelText);
        return button;
    }

    static void SetButtonLabelSize(Button button, int fontSize)
    {
        if (button == null)
        {
            return;
        }

        Text label = button.GetComponentInChildren<Text>();
        if (label == null)
        {
            return;
        }

        label.fontSize = fontSize;
    }

    static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, Vector2 anchor, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.material = null;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.alignByGeometry = true;
        text.resizeTextForBestFit = false;
        text.raycastTarget = false;
        return text;
    }

    static void AddHoverGlow(GameObject targetObject, Text labelText)
    {
        if (targetObject == null || labelText == null)
        {
            return;
        }

        Outline outline = labelText.GetComponent<Outline>();
        if (outline == null)
        {
            outline = labelText.gameObject.AddComponent<Outline>();
        }

        Color offColor = new Color(0.67f, 0.46f, 0.95f, 0f);
        Color onColor = new Color(0.67f, 0.46f, 0.95f, 0.9f);
        outline.effectColor = offColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = true;

        EventTrigger trigger = targetObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = targetObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
        }

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener(_ => { outline.effectColor = onColor; GalacticSlimefallAudio.PlayUiHover(); });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener(_ => outline.effectColor = offColor);
        trigger.triggers.Add(exitEntry);

        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener(_ => { outline.effectColor = onColor; GalacticSlimefallAudio.PlayUiHover(); });
        trigger.triggers.Add(selectEntry);

        EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
        deselectEntry.eventID = EventTriggerType.Deselect;
        deselectEntry.callback.AddListener(_ => outline.effectColor = offColor);
        trigger.triggers.Add(deselectEntry);
    }
    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    void SubscribeSceneEvents()
    {
        if (_sceneEventsSubscribed)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        _sceneEventsSubscribed = true;
    }

    void UnsubscribeSceneEvents()
    {
        if (!_sceneEventsSubscribed)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        _sceneEventsSubscribed = false;
    }

    static void EnsureMenuCamera()
    {
        if (Camera.main != null)
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 1f);
        camera.depth = -1f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }
    void TearDownOverlay()
    {
        if (_overlay != null)
        {
            Transform parent = _overlay.transform.parent;
            if (parent != null)
            {
                Destroy(parent.gameObject);
            }
            else
            {
                Destroy(_overlay);
            }
        }

        _overlay = null;
        _overlayGroup = null;
        _panel = null;
        _titleText = null;
        _botsButton = null;
        _humansButton = null;
        _resumeButton = null;
        _mainMenuButton = null;
        _exitButton = null;
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    static Sprite LoadBackgroundSprite()
    {
        if (_backgroundSprite != null)
        {
            return _backgroundSprite;
        }

        _backgroundSprite = Resources.Load<Sprite>(BackgroundResourcePath);
        if (_backgroundSprite != null)
        {
            return _backgroundSprite;
        }

        Sprite[] backgroundSprites = Resources.LoadAll<Sprite>(BackgroundResourcePath);
        if (backgroundSprites != null && backgroundSprites.Length > 0)
        {
            _backgroundSprite = backgroundSprites[0];
            return _backgroundSprite;
        }

        Texture2D backgroundTexture = Resources.Load<Texture2D>(BackgroundResourcePath);
        if (backgroundTexture == null)
        {
            return null;
        }

        backgroundTexture.wrapMode = TextureWrapMode.Clamp;
        backgroundTexture.filterMode = FilterMode.Bilinear;

        _backgroundSprite = Sprite.Create(
            backgroundTexture,
            new Rect(0f, 0f, backgroundTexture.width, backgroundTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        _backgroundSprite.name = "MainMenuBackgroundRuntime";
        return _backgroundSprite;
    }

    static bool IsScene(string sceneName, string targetName)
    {
        return string.Equals(sceneName, targetName, System.StringComparison.OrdinalIgnoreCase);
    }

    static bool ShouldRedirectFreshGameplaySession(string sceneName)
    {
        return IsScene(sceneName, GameplaySceneName) &&
               !_hasPendingMatchMode &&
               !_hasSessionMatchMode;
    }

    static bool ShouldTogglePauseMenu()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return keyboard.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}



























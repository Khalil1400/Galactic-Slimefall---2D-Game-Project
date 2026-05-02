using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Turn Panel")]
    public GameObject turnPanel;
    public Text turnText;
    public Text timerText;
    public Button matchModeButton;
    public Text matchModeButtonText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text gameOverText;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Health Bars")]
    public GameObject hpBarPrefab;
    public Transform hpBarsRoot;

    readonly Dictionary<Unit, Slider> _hpBars = new();
    readonly Dictionary<Unit, Text> _hpValueTexts = new();
    readonly Dictionary<Unit, GameObject> _hpBarRoots = new();
    readonly Dictionary<Unit, List<Image>> _hpBarDividers = new();
    readonly Dictionary<Unit, List<float>> _hpBarDividerThresholds = new();
    readonly Dictionary<Unit, float> _hpValueVisibleUntil = new();
    readonly Collider2D[] _hoverProbeBuffer = new Collider2D[12];
    bool _subscribed;
    bool _matchModeButtonBound;
    bool _restartButtonBound;
    bool _mainMenuButtonBound;
    float _nextHealthBarSyncAt;
    Unit _hoveredHpUnit;
    const float HpValueVisibleDuration = 5f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureHudReferences();

        HideGameOverPanel();

        BindRestartButton();
        BindMainMenuButton();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        EnsureHudReferences();
        HideGameOverPanel();
        EnsureUnitHealthBars();
        BindRestartButton();
        BindMainMenuButton();

        TrySubscribe();
        RefreshHud();
    }

    void Update()
    {
        UpdateHoveredHpValue();
        UpdateHpValueVisibility();

        if (Time.unscaledTime < _nextHealthBarSyncAt)
        {
            return;
        }

        _nextHealthBarSyncAt = Time.unscaledTime + 0.35f;
        EnsureUnitHealthBars();
    }

    void OnDisable()
    {
        if (_subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnTurnTimerChanged -= HandleTurnTimerChanged;
            GameManager.Instance.OnMatchModeChanged -= HandleMatchModeChanged;
            GameManager.Instance.OnGameOver -= HandleGameOver;
            _subscribed = false;
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null || hpBarPrefab == null || hpBarsRoot == null || _hpBars.ContainsKey(unit))
        {
            return;
        }

        GameObject barRoot = Instantiate(hpBarPrefab, hpBarsRoot);
        barRoot.SetActive(true);
        barRoot.transform.SetAsLastSibling();

        WorldSpaceFollow follow = barRoot.GetComponent<WorldSpaceFollow>();
        if (follow != null)
        {
            follow.target = unit.transform;
        }

        Slider slider = barRoot.GetComponentInChildren<Slider>();
        if (slider != null)
        {
            _hpBarRoots[unit] = barRoot;
            slider.minValue = 0f;
            slider.maxValue = unit.maxHP;
            slider.value = unit.CurrentHP;
            ConfigureHealthBarSegments(unit, barRoot);

            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = unit.isPlayerTeam ? new Color(0.29f, 0.78f, 0.4f) : new Color(0.9f, 0.36f, 0.29f);
            }

            Text valueText = EnsureHpValueText(unit);
            if (valueText != null)
            {
                valueText.text = unit.CurrentHP.ToString();
                valueText.enabled = false;
                _hpValueTexts[unit] = valueText;
            }

            _hpBars[unit] = slider;
        }

        unit.OnDeath += () => OnUnitDied(unit);
        UpdateUnitHealth(unit);
    }

    public void UpdateUnitHealth(Unit unit)
    {
        if (unit != null && _hpBars.TryGetValue(unit, out Slider slider))
        {
            slider.value = unit.CurrentHP;
            UpdateHealthBarDividerVisibility(unit, slider);
        }

        if (unit != null && _hpValueTexts.TryGetValue(unit, out Text valueText) && valueText != null)
        {
            valueText.text = unit.CurrentHP.ToString();
        }
    }

    public void ShowHpValue(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        Text valueText = EnsureHpValueText(unit);
        if (valueText == null)
        {
            return;
        }

        valueText.text = unit.CurrentHP.ToString();
        valueText.enabled = true;
        _hpValueVisibleUntil[unit] = Time.time + HpValueVisibleDuration;
    }

    public void ShowDamagePopup(Unit unit, int amount)
    {
        if (unit == null || amount <= 0 || hpBarsRoot == null)
        {
            return;
        }

        GameObject popup = new GameObject("DamagePopup", typeof(RectTransform), typeof(CanvasGroup));
        popup.transform.SetParent(hpBarsRoot, false);
        popup.transform.SetAsLastSibling();

        WorldSpaceFollow follow = popup.AddComponent<WorldSpaceFollow>();
        follow.target = unit.transform;
        follow.worldOffset = new Vector3(0f, 1.35f, 0f);

        CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Text text = CreateText("DamageText", popup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 26f), 22, TextAnchor.MiddleCenter);
        text.text = $"-{amount}";
        text.color = new Color(1f, 0.58f, 0.16f, 1f);
        text.fontStyle = FontStyle.Bold;
        ApplyTextFont(text, true);
        text.raycastTarget = false;

        StartCoroutine(AnimateDamagePopup(popup.transform as RectTransform, follow, canvasGroup));
    }

    public void ShowGameOver(bool playerWon)
    {
        EnsureHudReferences();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }

        if (gameOverText != null)
        {
            bool hotseat = GameManager.Instance != null && GameManager.Instance.CurrentMatchMode == GameManager.MatchMode.Hotseat;
            if (hotseat)
            {
                gameOverText.text = playerWon ? "Player One Wins" : "Player Two Wins";
            }
            else
            {
                gameOverText.text = playerWon ? "Victory!" : "You Lost";
            }
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
        }

        RefreshHud();
    }

    void HandleStateChanged(GameManager.GameState _)
    {
        RefreshHud();
    }

    void HandleTurnTimerChanged(float timeRemaining)
    {
        if (timerText == null)
        {
            return;
        }

        int displaySeconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
        timerText.text = $"{displaySeconds:00}";
        timerText.color = timeRemaining <= 5f ? new Color(1f, 0.45f, 0.33f) : new Color(0.94f, 0.95f, 0.98f);
    }

    void HandleMatchModeChanged(GameManager.MatchMode _)
    {
        RefreshHud();
    }

    void HandleGameOver(bool playerWon)
    {
        ShowGameOver(playerWon);
        RefreshHud();
    }

    void RefreshHud()
    {
        EnsureHudReferences();

        GameManager manager = GameManager.Instance;
        if (turnPanel != null)
        {
            bool showTurnPanel = manager == null || !manager.IsGameOver;
            turnPanel.SetActive(showTurnPanel);
        }

        if (manager == null || !manager.IsGameOver)
        {
            HideGameOverPanel();
        }

        if (manager == null)
        {
            if (turnText != null)
            {
                turnText.text = "Your Turn";
            }

            if (timerText != null)
            {
                timerText.text = "--";
            }

            if (matchModeButtonText != null)
            {
                matchModeButtonText.text = "Mode: VS AI";
            }

            return;
        }

        if (manager.IsGameOver)
        {
            return;
        }

        if (turnText != null)
        {
            turnText.text = GetTurnLabel(manager);
            turnText.color = manager.IsPlayerTurn
                ? new Color(0.27f, 0.82f, 0.38f)
                : (manager.CurrentMatchMode == GameManager.MatchMode.Hotseat
                    ? new Color(0.39f, 0.76f, 1f)
                    : new Color(0.93f, 0.39f, 0.3f));
        }

        if (matchModeButtonText != null)
        {
            matchModeButtonText.text = manager.CurrentMatchMode == GameManager.MatchMode.Hotseat
                ? "Mode: HOTSEAT"
                : "Mode: VS AI";
        }

        if (matchModeButton != null)
        {
            matchModeButton.interactable = !manager.IsTurnTransitioning;
        }

        HandleTurnTimerChanged(manager.TurnTimeRemaining);
    }

    string GetTurnLabel(GameManager manager)
    {
        if (manager.CurrentMatchMode == GameManager.MatchMode.Hotseat)
        {
            return manager.IsPlayerTurn ? "Player One Turn" : "Player Two Turn";
        }

        if (manager.IsPlayerTurn)
        {
            return "Your Turn";
        }

        return "Enemy Turn";
    }

    void OnUnitDied(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        if (_hpBarRoots.TryGetValue(unit, out GameObject root))
        {
            Destroy(root);
        }

        _hpBars.Remove(unit);
        _hpValueTexts.Remove(unit);
        _hpBarRoots.Remove(unit);
        _hpBarDividers.Remove(unit);
        _hpBarDividerThresholds.Remove(unit);
        _hpValueVisibleUntil.Remove(unit);
        if (_hoveredHpUnit == unit)
        {
            _hoveredHpUnit = null;
        }
    }

    void UpdateHpValueVisibility()
    {
        if (_hoveredHpUnit != null && (_hoveredHpUnit.IsDead || !_hpValueTexts.ContainsKey(_hoveredHpUnit)))
        {
            _hoveredHpUnit = null;
        }

        List<Unit> expiredUnits = null;
        float now = Time.time;
        foreach (KeyValuePair<Unit, Text> pair in _hpValueTexts)
        {
            Unit unit = pair.Key;
            Text valueText = pair.Value;
            if (unit == null || valueText == null)
            {
                continue;
            }

            bool timerVisible = _hpValueVisibleUntil.TryGetValue(unit, out float hideAt) && now < hideAt;
            bool hoverVisible = unit == _hoveredHpUnit;
            valueText.enabled = timerVisible || hoverVisible;
            if (valueText.enabled)
            {
                valueText.text = unit.CurrentHP.ToString();
            }

            if (!timerVisible && _hpValueVisibleUntil.ContainsKey(unit))
            {
                expiredUnits ??= new List<Unit>();
                expiredUnits.Add(unit);
            }
        }

        if (expiredUnits == null)
        {
            return;
        }

        foreach (Unit unit in expiredUnits)
        {
            _hpValueVisibleUntil.Remove(unit);
        }
    }

    void UpdateHoveredHpValue()
    {
        _hoveredHpUnit = GetHoveredUnitUnderPointer();
    }

    Unit GetHoveredUnitUnderPointer()
    {
        if (MainMenuController.IsMenuOpen)
        {
            return null;
        }

        Vector2 screenPosition = ReadPointerScreenPosition();
        if (WeaponInventory.Instance != null && WeaponInventory.Instance.IsPointerOverInteractiveUi(screenPosition))
        {
            return null;
        }

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return null;
        }

        Vector3 worldPoint3 = activeCamera.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint = new Vector2(worldPoint3.x, worldPoint3.y);
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;
        int hitCount = Physics2D.OverlapPoint(worldPoint, filter, _hoverProbeBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Unit unit = GetUnitFromCollider(_hoverProbeBuffer[i]);
            if (unit != null && !unit.IsDead)
            {
                return unit;
            }
        }

        return null;
    }

    IEnumerator AnimateDamagePopup(RectTransform popupRect, WorldSpaceFollow follow, CanvasGroup canvasGroup)
    {
        float duration = 0.7f;
        float elapsed = 0f;
        Vector3 startOffset = follow.worldOffset;
        Vector3 endOffset = startOffset + new Vector3(0f, 0.42f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            follow.worldOffset = Vector3.Lerp(startOffset, endOffset, t);
            canvasGroup.alpha = 1f - t;

            if (popupRect != null)
            {
                popupRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, t * 0.5f);
            }

            yield return null;
        }

        if (popupRect != null)
        {
            Destroy(popupRect.gameObject);
        }
    }

    void OnRestartClicked()
    {
        GalacticSlimefallAudio.PlayUiClick();
        SceneLoader.Instance?.RestartLevel();
    }

    void OnMainMenuButtonClicked()
    {
        GalacticSlimefallAudio.PlayUiClick();
        SceneLoader.Instance?.LoadMainMenu();
    }

    void OnMatchModeClicked()
    {
        GalacticSlimefallAudio.PlayUiClick();
        GameManager.Instance?.CycleMatchMode();
    }

    void TrySubscribe()
    {
        if (_subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnStateChanged += HandleStateChanged;
        GameManager.Instance.OnTurnTimerChanged += HandleTurnTimerChanged;
        GameManager.Instance.OnMatchModeChanged += HandleMatchModeChanged;
        GameManager.Instance.OnGameOver += HandleGameOver;
        _subscribed = true;
        RefreshHud();
    }

    void EnsureHudReferences()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null && hpBarsRoot == null)
        {
            Transform existing = canvas.transform.Find("HealthBarsRoot");
            if (existing != null)
            {
                hpBarsRoot = existing;
            }
            else
            {
                GameObject rootObject = new GameObject("HealthBarsRoot", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                RectTransform rootRect = rootObject.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                hpBarsRoot = rootRect;
            }
        }

        if (turnPanel == null)
        {
            if (canvas != null)
            {
                GameObject panelObject = new GameObject("TurnPanel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(canvas.transform, false);
                RectTransform rect = panelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -20f);
                rect.sizeDelta = new Vector2(320f, 110f);

                Image image = panelObject.GetComponent<Image>();
                image.color = Color.clear;
                image.enabled = false;
                image.raycastTarget = false;
                turnPanel = panelObject;
            }
        }

        if (turnPanel == null)
        {
            return;
        }

        EnsureGameOverReferences(canvas);

        if (turnText == null)
        {
            turnText = CreateText("TurnText", turnPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(240f, 28f), 22, TextAnchor.MiddleCenter);
        }
        turnText.fontSize = 22;
        turnText.fontStyle = FontStyle.Bold;
        turnText.alignment = TextAnchor.MiddleCenter;
        ApplyTextFont(turnText, true);

        if (turnText != null)
        {
            RectTransform turnTextRect = turnText.rectTransform;
            turnTextRect.anchorMin = Vector2.zero;
            turnTextRect.anchorMax = Vector2.one;
            turnTextRect.pivot = new Vector2(0.5f, 0.5f);
            turnTextRect.anchoredPosition = Vector2.zero;
            turnTextRect.offsetMin = new Vector2(10f, 10f);
            turnTextRect.offsetMax = new Vector2(-10f, -10f);
        }

        if (timerText == null)
        {
            Transform timerParent = turnPanel.transform.parent != null ? turnPanel.transform.parent : turnPanel.transform;
            GameObject badge = new GameObject("TurnTimerPanel", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(timerParent, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-24f, -22f);
            badgeRect.sizeDelta = new Vector2(126f, 62f);

            Image badgeImage = badge.GetComponent<Image>();
            badgeImage.color = Color.clear;
            badgeImage.enabled = false;
            badgeImage.raycastTarget = false;

            timerText = CreateText("TimerText", badge.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, TextAnchor.MiddleCenter);
            timerText.rectTransform.offsetMin = Vector2.zero;
            timerText.rectTransform.offsetMax = new Vector2(0f, -18f);
        }
        timerText.fontSize = 30;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleCenter;
        ApplyTextFont(timerText, true);

        if (timerText != null && timerText.transform.parent != null)
        {
            GameObject ensuredTimerPanel = timerText.transform.parent.gameObject;
            ensuredTimerPanel.SetActive(true);
            ensuredTimerPanel.transform.SetAsLastSibling();

            RectTransform ensuredTimerRect = ensuredTimerPanel.transform as RectTransform;
            if (ensuredTimerRect != null)
            {
                ensuredTimerRect.anchorMin = new Vector2(1f, 1f);
                ensuredTimerRect.anchorMax = new Vector2(1f, 1f);
                ensuredTimerRect.pivot = new Vector2(1f, 1f);
                ensuredTimerRect.anchoredPosition = new Vector2(-24f, -22f);
                ensuredTimerRect.sizeDelta = new Vector2(126f, 62f);
            }

            timerText.rectTransform.anchorMin = Vector2.zero;
            timerText.rectTransform.anchorMax = Vector2.one;
            timerText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            timerText.rectTransform.anchoredPosition = Vector2.zero;
            timerText.rectTransform.offsetMin = new Vector2(0f, 0f);
            timerText.rectTransform.offsetMax = new Vector2(0f, -18f);

            Transform timerLabelTransform = ensuredTimerPanel.transform.Find("TimerLabel");
            Text timerLabel = timerLabelTransform != null
                ? timerLabelTransform.GetComponent<Text>()
                : CreateText("TimerLabel", ensuredTimerPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 12f), 10, TextAnchor.UpperCenter);
            if (timerLabel != null)
            {
                timerLabel.text = "Round Time";
                timerLabel.fontSize = 10;
                timerLabel.fontStyle = FontStyle.Bold;
                timerLabel.alignment = TextAnchor.UpperCenter;
                ApplyTextFont(timerLabel, true);
                timerLabel.color = new Color(0.86f, 0.9f, 0.98f, 0.92f);
                timerLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
                timerLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
                timerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
                timerLabel.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                timerLabel.rectTransform.sizeDelta = new Vector2(0f, 14f);
            }

            Image ensuredTimerImage = ensuredTimerPanel.GetComponent<Image>();
            if (ensuredTimerImage != null)
            {
                ensuredTimerImage.enabled = false;
                ensuredTimerImage.color = Color.clear;
                ensuredTimerImage.raycastTarget = false;
            }

            Outline ensuredTimerOutline = ensuredTimerPanel.GetComponent<Outline>();
            if (ensuredTimerOutline != null)
            {
                ensuredTimerOutline.enabled = false;
            }
        }

        if (matchModeButton == null)
        {
            matchModeButton = CreateButton("MatchModeButton", turnPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(170f, 28f));
            matchModeButtonText = matchModeButton.GetComponentInChildren<Text>();
        }
        else if (matchModeButtonText == null)
        {
            matchModeButtonText = matchModeButton.GetComponentInChildren<Text>();
        }

        if (matchModeButtonText != null)
        {
            matchModeButtonText.fontStyle = FontStyle.Bold;
            ApplyTextFont(matchModeButtonText, true);
        }

        RectTransform turnRect = turnPanel.GetComponent<RectTransform>();
        if (turnRect != null)
        {
            turnRect.anchorMin = new Vector2(0.5f, 1f);
            turnRect.anchorMax = new Vector2(0.5f, 1f);
            turnRect.pivot = new Vector2(0.5f, 1f);
            turnRect.anchoredPosition = new Vector2(0f, -18f);
            turnRect.sizeDelta = new Vector2(260f, 66f);
        }

        Image turnImage = turnPanel.GetComponent<Image>();
        if (turnImage != null)
        {
            turnImage.color = Color.clear;
            turnImage.enabled = false;
            turnImage.raycastTarget = false;
        }

        if (matchModeButton != null && !_matchModeButtonBound)
        {
            matchModeButton.onClick.AddListener(OnMatchModeClicked);
            _matchModeButtonBound = true;
        }

        BindRestartButton();
        BindMainMenuButton();
    }

    void EnsureGameOverReferences(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        if (gameOverPanel == null)
        {
            Transform existing = canvas.transform.Find("GameOverPanel");
            if (existing != null)
            {
                gameOverPanel = existing.gameObject;
            }
            else
            {
                gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
                gameOverPanel.transform.SetParent(canvas.transform, false);
                RectTransform rect = gameOverPanel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(420f, 240f);
                rect.anchoredPosition = Vector2.zero;

                Image image = gameOverPanel.GetComponent<Image>();
                image.color = new Color(0.03f, 0.08f, 0.12f, 0.92f);
                image.raycastTarget = false;
            }
        }

        if (gameOverText == null && gameOverPanel != null)
        {
            Transform existingText = gameOverPanel.transform.Find("GameOverText");
            gameOverText = existingText != null
                ? existingText.GetComponent<Text>()
                : CreateText("GameOverText", gameOverPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(320f, 80f), 42, TextAnchor.MiddleCenter);
        }

        if (gameOverText != null)
        {
            gameOverText.fontSize = 42;
            gameOverText.fontStyle = FontStyle.Bold;
            gameOverText.alignment = TextAnchor.MiddleCenter;
            ApplyTextFont(gameOverText, true);
        }

        if (gameOverPanel != null)
        {
            Image panelImage = gameOverPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.enabled = false;
                panelImage.raycastTarget = false;
            }
        }

        if (restartButton == null && gameOverPanel != null)
        {
            Transform existingButton = gameOverPanel.transform.Find("RestartButton");
            restartButton = existingButton != null
                ? existingButton.GetComponent<Button>()
                : CreateButton("RestartButton", gameOverPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96f, -70f), new Vector2(170f, 52f));

            Text label = restartButton != null ? restartButton.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.text = "Restart";
                label.fontStyle = FontStyle.Bold;
                ApplyTextFont(label, true);
            }
        }

        if (mainMenuButton == null && gameOverPanel != null)
        {
            Transform existingMainMenuButton = gameOverPanel.transform.Find("MainMenuButton");
            mainMenuButton = existingMainMenuButton != null
                ? existingMainMenuButton.GetComponent<Button>()
                : CreateButton("MainMenuButton", gameOverPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, -70f), new Vector2(170f, 52f));

            Text mainMenuLabel = mainMenuButton != null ? mainMenuButton.GetComponentInChildren<Text>() : null;
            if (mainMenuLabel != null)
            {
                mainMenuLabel.text = "Main Menu";
                mainMenuLabel.fontStyle = FontStyle.Bold;
                ApplyTextFont(mainMenuLabel, true);
            }
        }
    }

    void BindRestartButton()
    {
        if (restartButton == null || _restartButtonBound)
        {
            return;
        }

        restartButton.onClick.AddListener(OnRestartClicked);
        _restartButtonBound = true;
    }

    void BindMainMenuButton()
    {
        if (mainMenuButton == null || _mainMenuButtonBound)
        {
            return;
        }

        mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        _mainMenuButtonBound = true;
    }

    void EnsureUnitHealthBars()
    {
        EnsureHudReferences();
        if (hpBarsRoot == null || hpBarPrefab == null)
        {
            return;
        }

        List<Unit> staleUnits = null;
        foreach (KeyValuePair<Unit, GameObject> pair in _hpBarRoots)
        {
            Unit unit = pair.Key;
            if (unit != null && !unit.IsDead)
            {
                continue;
            }

            staleUnits ??= new List<Unit>();
            staleUnits.Add(unit);
        }

        if (staleUnits != null)
        {
            foreach (Unit staleUnit in staleUnits)
            {
                if (staleUnit != null)
                {
                    OnUnitDied(staleUnit);
                    continue;
                }

                if (_hpBarRoots.TryGetValue(staleUnit, out GameObject staleRoot) && staleRoot != null)
                {
                    Destroy(staleRoot);
                }

                _hpBarRoots.Remove(staleUnit);
                _hpBars.Remove(staleUnit);
                _hpValueTexts.Remove(staleUnit);
                _hpBarDividers.Remove(staleUnit);
                _hpBarDividerThresholds.Remove(staleUnit);
            }
        }

        foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if (unit == null)
            {
                continue;
            }

            if (_hpBarRoots.TryGetValue(unit, out GameObject root) && root == null)
            {
                _hpBarRoots.Remove(unit);
                _hpBars.Remove(unit);
                _hpValueTexts.Remove(unit);
                _hpBarDividers.Remove(unit);
                _hpBarDividerThresholds.Remove(unit);
            }

            if (!_hpBars.ContainsKey(unit))
            {
                RegisterUnit(unit);
                continue;
            }

            UpdateUnitHealth(unit);
        }
    }

    Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        RuntimeUiFont.Apply(text);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        return text;
    }

    Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        Text label = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, TextAnchor.MiddleCenter);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.text = "Mode: VS AI";
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.985f, 0.99f, 1f, 1f);
        ApplyTextFont(label, true);
        AddHoverGlow(buttonObject, label);
        return button;
    }

    void AddHoverGlow(GameObject targetObject, Text labelText)
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
        else
        {
            trigger.triggers.Clear();
        }

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener(_ => outline.effectColor = onColor);
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener(_ => outline.effectColor = offColor);
        trigger.triggers.Add(exitEntry);

        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener(_ => outline.effectColor = onColor);
        trigger.triggers.Add(selectEntry);

        EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
        deselectEntry.eventID = EventTriggerType.Deselect;
        deselectEntry.callback.AddListener(_ => outline.effectColor = offColor);
        trigger.triggers.Add(deselectEntry);
    }

    void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    Text EnsureHpValueText(Unit unit)
    {
        if (unit == null)
        {
            return null;
        }

        if (!_hpBarRoots.TryGetValue(unit, out GameObject barRoot) || barRoot == null)
        {
            return null;
        }

        Transform existing = barRoot.transform.Find("HPValueText");
        Text text = existing != null
            ? existing.GetComponent<Text>()
            : CreateText("HPValueText", barRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76f, 0f), new Vector2(64f, 24f), 18, TextAnchor.MiddleLeft);

        ApplyTextFont(text, true);
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enabled = unit == _hoveredHpUnit || (_hpValueVisibleUntil.TryGetValue(unit, out float hideAt) && Time.time < hideAt);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(66f, 0f);
        rect.sizeDelta = new Vector2(64f, 24f);

        return text;
    }

    void ConfigureHealthBarSegments(Unit unit, GameObject barRoot)
    {
        if (unit == null || barRoot == null)
        {
            return;
        }

        Transform overlay = EnsureDividerOverlay(barRoot.transform);
        if (overlay == null)
        {
            return;
        }

        for (int i = overlay.childCount - 1; i >= 0; i--)
        {
            Transform child = overlay.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        int segmentCount = Mathf.Clamp(Mathf.CeilToInt(unit.maxHP / 10f), 4, 12);
        if (segmentCount <= 1)
        {
            _hpBarDividers.Remove(unit);
            _hpBarDividerThresholds.Remove(unit);
            return;
        }

        List<Image> dividers = new();
        List<float> thresholds = new();
        for (int i = 1; i < segmentCount; i++)
        {
            float anchor = i / (float)segmentCount;
            GameObject divider = new GameObject($"Divider{i}", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(overlay, false);

            RectTransform rect = divider.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchor, 0f);
            rect.anchorMax = new Vector2(anchor, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2f, 0f);

            Image image = divider.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.58f);
            image.raycastTarget = false;

            Outline outline = divider.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.05f);
            outline.effectDistance = new Vector2(1f, 0f);
            outline.useGraphicAlpha = true;

            dividers.Add(image);
            thresholds.Add(anchor);
        }

        _hpBarDividers[unit] = dividers;
        _hpBarDividerThresholds[unit] = thresholds;
    }

    void UpdateHealthBarDividerVisibility(Unit unit, Slider slider)
    {
        if (unit == null || slider == null)
        {
            return;
        }

        if (!_hpBarDividers.TryGetValue(unit, out List<Image> dividers) ||
            !_hpBarDividerThresholds.TryGetValue(unit, out List<float> thresholds))
        {
            return;
        }

        float fillAmount = slider.maxValue <= slider.minValue
            ? 0f
            : Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value);

        int count = Mathf.Min(dividers.Count, thresholds.Count);
        for (int i = 0; i < count; i++)
        {
            Image divider = dividers[i];
            if (divider == null)
            {
                continue;
            }

            bool visible = fillAmount > 0.001f && thresholds[i] < fillAmount;
            divider.enabled = visible;

            Outline outline = divider.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = visible;
            }
        }
    }

    static Transform EnsureDividerOverlay(Transform barRoot)
    {
        if (barRoot == null)
        {
            return null;
        }

        Transform existing = barRoot.Find("DividerOverlay");
        if (existing != null)
        {
            return existing;
        }

        GameObject overlay = new GameObject("DividerOverlay", typeof(RectTransform));
        overlay.transform.SetParent(barRoot, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);
        return overlay.transform;
    }

    static Unit GetUnitFromCollider(Collider2D collider)
    {
        return collider == null
            ? null
            : collider.GetComponent<Unit>() ?? collider.GetComponentInParent<Unit>() ?? collider.attachedRigidbody?.GetComponent<Unit>();
    }

    static Vector2 ReadPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        if (Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        return Vector2.zero;
#else
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
#endif
    }

    static void ApplyTextFont(Text text, bool bold = false)
    {
        RuntimeUiFont.Apply(text, bold);
    }
}


















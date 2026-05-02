using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SlimefallRetroHudStyler : MonoBehaviour
{
    readonly Color yourPanel = new(0.04f, 0.12f, 0.06f, 0.96f);
    readonly Color yourBorder = new(0.18f, 0.8f, 0.44f, 1f);
    readonly Color yourGlow = new(0.24f, 1f, 0.48f, 0.34f);
    readonly Color enemyPanel = new(0.11f, 0.03f, 0.03f, 0.96f);
    readonly Color enemyBorder = new(0.9f, 0.3f, 0.24f, 1f);
    readonly Color enemyGlow = new(1f, 0.35f, 0.3f, 0.34f);
    readonly Color boxPanel = new(0.055f, 0.04f, 0.12f, 0.96f);
    readonly Color boxBorder = new(0.78f, 0.57f, 0.05f, 1f);
    readonly Color boxShadow = new(0f, 0f, 0f, 0.5f);
    readonly Color glassPanel = new(0.3f, 0.34f, 0.41f, 0.26f);
    readonly Color glassBorder = new(1f, 1f, 1f, 0.22f);
    readonly Color glassShadow = new(0f, 0f, 0f, 0.08f);
    readonly Color glassTopLine = new(1f, 0.92f, 0.62f, 0.18f);
    readonly Color timerSegmentOff = new(0.48f, 0.52f, 0.6f, 0.18f);
    readonly Color timerSegmentBorder = new(1f, 1f, 1f, 0.06f);
    readonly Color timerGreen = new(0.24f, 1f, 0.48f, 1f);
    readonly Color timerYellow = new(1f, 0.85f, 0.29f, 1f);
    readonly Color timerRed = new(1f, 0.35f, 0.29f, 1f);
    readonly Color softLabel = new(0.52f, 0.33f, 0.08f, 1f);
    readonly Color textWhite = new(0.95f, 0.97f, 1f, 1f);

    GameObject hudRoot;
    GameObject bannerRoot;
    Image bannerGlow;
    Image bannerFrame;
    Image bannerInnerPanel;
    Image bannerTopRail;
    Image bannerBottomRail;
    Image bannerLeftCap;
    Image bannerRightCap;
    Text bannerLeftChevrons;
    Text bannerRightChevrons;
    Text bannerText;
    GameObject timerRoot;
    Text timerValueText;
    RectTransform timerBarRoot;
    readonly List<Image> timerSegments = new();
    GameObject victoryRoot;
    Text victoryTitleText;
    Text victorySubText;
    Button victoryRestartButton;
    Button victoryMainMenuButton;

    bool subscribed;
    GameManager.GameState lastState;
    IEnumerator Start()
    {
        yield return null;
        yield return null;
        EnsureHud();
        RefreshAll();
        TrySubscribe();
        StartCoroutine(PollHud());
    }

    void OnDestroy()
    {
        if (subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnTurnTimerChanged -= HandleTimerChanged;
            GameManager.Instance.OnMatchModeChanged -= HandleModeChanged;
            GameManager.Instance.OnGameOver -= HandleGameOver;
            subscribed = false;
        }
    }

    void TrySubscribe()
    {
        if (subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnStateChanged += HandleStateChanged;
        GameManager.Instance.OnTurnTimerChanged += HandleTimerChanged;
        GameManager.Instance.OnMatchModeChanged += HandleModeChanged;
        GameManager.Instance.OnGameOver += HandleGameOver;
        subscribed = true;
        lastState = GameManager.Instance.State;
    }

    IEnumerator PollHud()
    {
        while (true)
        {
            HideLegacyHud();
            if (bannerRoot != null)
            {
                bannerRoot.SetActive(false);
            }
            RefreshTimer();
            yield return new WaitForSeconds(0.2f);
        }
    }

    void HandleStateChanged(GameManager.GameState state)
    {
        EnsureHud();
        HideLegacyHud();
        RefreshAll();

        if (MainMenuController.IsMenuOpen)
        {
            lastState = state;
            return;
        }

        if (state == lastState || state == GameManager.GameState.GameOver)
        {
            lastState = state;
            return;
        }

        lastState = state;
    }

    void HandleTimerChanged(float _)
    {
        RefreshTimer();
    }

    void HandleModeChanged(GameManager.MatchMode _)
    {
        RefreshAll();
    }

    void HandleGameOver(bool playerWon)
    {
        HideVictory();
    }

    void RefreshAll()
    {
        EnsureHud();
        TrySubscribe();
        HideLegacyHud();
        RefreshTimer();
        StyleInventory();

        if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
        {
            HideVictory();
        }
    }

    void EnsureHud()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        if (hudRoot == null)
        {
            hudRoot = new GameObject("GB_RetroHudRoot", typeof(RectTransform));
            hudRoot.transform.SetParent(canvas.transform, false);
            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;
        }

        EnsureTimerPanel();
        EnsureVictoryPanel();
    }

    void EnsureBanner()
    {
        if (bannerRoot != null)
        {
            return;
        }

        bannerRoot = new GameObject("GB_TurnBanner", typeof(RectTransform), typeof(CanvasGroup));
        bannerRoot.transform.SetParent(hudRoot.transform, false);

        RectTransform rect = bannerRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(690f, 164f);

        RectTransform glowRect = EnsureRect(bannerRoot.transform, "Glow");
        Stretch(glowRect, Vector2.zero, Vector2.zero);
        bannerGlow = EnsureImage(glowRect.gameObject, yourGlow);
        bannerGlow.color = new Color(yourGlow.r, yourGlow.g, yourGlow.b, 0.18f);

        RectTransform frameRect = EnsureRect(bannerRoot.transform, "Frame");
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(520f, 92f);
        frameRect.anchoredPosition = Vector2.zero;
        bannerFrame = EnsureImage(frameRect.gameObject, yourPanel);
        AddOutline(frameRect.gameObject, yourBorder, 3f);
        AddShadow(frameRect.gameObject, yourGlow, Vector2.zero);

        RectTransform innerRect = EnsureRect(frameRect, "InnerPanel");
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.sizeDelta = new Vector2(468f, 62f);
        innerRect.anchoredPosition = Vector2.zero;
        bannerInnerPanel = EnsureImage(innerRect.gameObject, new Color(0.02f, 0.09f, 0.12f, 0.88f));
        AddOutline(innerRect.gameObject, new Color(0.02f, 0.24f, 0.26f, 0.95f), 1.5f);

        RectTransform topRailRect = EnsureRect(frameRect, "TopRail");
        topRailRect.anchorMin = new Vector2(0f, 1f);
        topRailRect.anchorMax = new Vector2(1f, 1f);
        topRailRect.pivot = new Vector2(0.5f, 1f);
        topRailRect.offsetMin = new Vector2(22f, -12f);
        topRailRect.offsetMax = new Vector2(-22f, -6f);
        bannerTopRail = EnsureImage(topRailRect.gameObject, yourBorder);

        RectTransform bottomRailRect = EnsureRect(frameRect, "BottomRail");
        bottomRailRect.anchorMin = new Vector2(0f, 0f);
        bottomRailRect.anchorMax = new Vector2(1f, 0f);
        bottomRailRect.pivot = new Vector2(0.5f, 0f);
        bottomRailRect.offsetMin = new Vector2(22f, 6f);
        bottomRailRect.offsetMax = new Vector2(-22f, 12f);
        bannerBottomRail = EnsureImage(bottomRailRect.gameObject, yourBorder);

        RectTransform leftCapRect = EnsureRect(frameRect, "LeftCap");
        leftCapRect.anchorMin = new Vector2(0f, 0.5f);
        leftCapRect.anchorMax = new Vector2(0f, 0.5f);
        leftCapRect.pivot = new Vector2(0.5f, 0.5f);
        leftCapRect.sizeDelta = new Vector2(26f, 74f);
        leftCapRect.anchoredPosition = new Vector2(-8f, 0f);
        leftCapRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        bannerLeftCap = EnsureImage(leftCapRect.gameObject, yourBorder);

        RectTransform rightCapRect = EnsureRect(frameRect, "RightCap");
        rightCapRect.anchorMin = new Vector2(1f, 0.5f);
        rightCapRect.anchorMax = new Vector2(1f, 0.5f);
        rightCapRect.pivot = new Vector2(0.5f, 0.5f);
        rightCapRect.sizeDelta = new Vector2(26f, 74f);
        rightCapRect.anchoredPosition = new Vector2(8f, 0f);
        rightCapRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        bannerRightCap = EnsureImage(rightCapRect.gameObject, yourBorder);

        bannerLeftChevrons = CreateText("LeftChevrons", bannerRoot.transform, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        bannerLeftChevrons.text = "<<";
        bannerLeftChevrons.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        bannerLeftChevrons.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bannerLeftChevrons.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bannerLeftChevrons.rectTransform.sizeDelta = new Vector2(56f, 34f);
        bannerLeftChevrons.rectTransform.anchoredPosition = new Vector2(-298f, 0f);

        bannerRightChevrons = CreateText("RightChevrons", bannerRoot.transform, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        bannerRightChevrons.text = ">>";
        bannerRightChevrons.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRightChevrons.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRightChevrons.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bannerRightChevrons.rectTransform.sizeDelta = new Vector2(56f, 34f);
        bannerRightChevrons.rectTransform.anchoredPosition = new Vector2(298f, 0f);

        bannerText = CreateText("BannerText", bannerRoot.transform, 52, FontStyle.Bold, TextAnchor.MiddleCenter);
        bannerText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        bannerText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        bannerText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bannerText.rectTransform.sizeDelta = new Vector2(420f, 72f);
        bannerText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        bannerText.supportRichText = false;
        bannerText.text = "YOUR TURN";
        AddOutline(bannerText.gameObject, new Color(0f, 0.16f, 0.12f, 1f), 2f);
        AddShadow(bannerText.gameObject, new Color(1f, 1f, 1f, 0.08f), new Vector2(0f, -1f));

        CanvasGroup group = bannerRoot.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        bannerRoot.SetActive(false);
    }

    void EnsureTimerPanel()
    {
        if (timerRoot != null)
        {
            ApplyTimerGlassStyle();
            return;
        }

        timerRoot = CreateBox("GB_TimerPanel", hudRoot.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(186f, 100f));
        ApplyTimerGlassStyle();

        Text label = CreateText("TimerLabel", timerRoot.transform, 10, FontStyle.Bold, TextAnchor.UpperCenter);
        label.text = "Round Time";
        label.color = softLabel;
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -5f);
        label.rectTransform.sizeDelta = new Vector2(0f, 14f);

        timerValueText = CreateText("TimerValue", timerRoot.transform, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
        timerValueText.rectTransform.anchorMin = new Vector2(0f, 0f);
        timerValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        timerValueText.rectTransform.offsetMin = new Vector2(10f, 8f);
        timerValueText.rectTransform.offsetMax = new Vector2(-10f, -18f);
        timerValueText.text = "20";

        timerBarRoot = EnsureRect(timerRoot.transform, "TimerBar");
        timerBarRoot.anchorMin = new Vector2(0f, 0f);
        timerBarRoot.anchorMax = new Vector2(1f, 0f);
        timerBarRoot.pivot = new Vector2(0.5f, 0f);
        timerBarRoot.offsetMin = new Vector2(14f, 10f);
        timerBarRoot.offsetMax = new Vector2(-14f, 24f);
    }

    void EnsureVictoryPanel()
    {
        if (victoryRoot != null)
        {
            return;
        }

        victoryRoot = new GameObject("GB_VictoryOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        victoryRoot.transform.SetParent(hudRoot.transform, false);

        RectTransform overlayRect = victoryRoot.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = victoryRoot.GetComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.02f, 0.08f, 0.84f);

        GameObject box = CreateBox("VictoryBox", victoryRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 232f));
        CreateScrew(box.transform, "ScrewTL", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f));
        CreateScrew(box.transform, "ScrewTR", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f));
        CreateScrew(box.transform, "ScrewBL", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 10f));
        CreateScrew(box.transform, "ScrewBR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 10f));

        RectTransform stars = EnsureRect(box.transform, "Stars");
        stars.anchorMin = new Vector2(0.5f, 1f);
        stars.anchorMax = new Vector2(0.5f, 1f);
        stars.pivot = new Vector2(0.5f, 1f);
        stars.anchoredPosition = new Vector2(0f, -24f);
        stars.sizeDelta = new Vector2(120f, 20f);
        CreateStar(stars, "StarLeft", new Vector2(-26f, 0f), 12f, new Color(0.78f, 0.57f, 0.05f, 1f));
        CreateStar(stars, "StarCenter", new Vector2(0f, 0f), 18f, timerYellow);
        CreateStar(stars, "StarRight", new Vector2(26f, 0f), 12f, new Color(0.78f, 0.57f, 0.05f, 1f));

        victoryTitleText = CreateText("VictoryTitle", box.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        victoryTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        victoryTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        victoryTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        victoryTitleText.rectTransform.anchoredPosition = new Vector2(0f, -66f);
        victoryTitleText.rectTransform.sizeDelta = new Vector2(0f, 34f);
        victoryTitleText.color = timerYellow;

        RectTransform divider = EnsureRect(box.transform, "Divider");
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(0.5f, 1f);
        divider.offsetMin = new Vector2(28f, -112f);
        divider.offsetMax = new Vector2(-28f, -108f);
        EnsureImage(divider.gameObject, boxBorder);

        victorySubText = CreateText("VictorySub", box.transform, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
        victorySubText.rectTransform.anchorMin = new Vector2(0f, 1f);
        victorySubText.rectTransform.anchorMax = new Vector2(1f, 1f);
        victorySubText.rectTransform.pivot = new Vector2(0.5f, 1f);
        victorySubText.rectTransform.anchoredPosition = new Vector2(0f, -138f);
        victorySubText.rectTransform.sizeDelta = new Vector2(0f, 16f);
        victorySubText.color = softLabel;

        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(box.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(-96f, 24f);
        buttonRect.sizeDelta = new Vector2(176f, 44f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.08f, 0.19f, 1f);
        AddOutline(buttonObject, boxBorder, 2f);

        victoryRestartButton = buttonObject.GetComponent<Button>();
        victoryRestartButton.onClick.AddListener(() => { GalacticSlimefallAudio.PlayUiClick(); SceneLoader.Instance?.RestartLevel(); });

        Text buttonText = CreateText("Label", buttonObject.transform, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        buttonText.text = "RESTART";
        buttonText.color = timerYellow;
        buttonText.rectTransform.anchorMin = Vector2.zero;
        buttonText.rectTransform.anchorMax = Vector2.one;
        buttonText.rectTransform.offsetMin = Vector2.zero;
        buttonText.rectTransform.offsetMax = Vector2.zero;

        GameObject menuButtonObject = new GameObject("MainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        menuButtonObject.transform.SetParent(box.transform, false);
        RectTransform menuButtonRect = menuButtonObject.GetComponent<RectTransform>();
        menuButtonRect.anchorMin = new Vector2(0.5f, 0f);
        menuButtonRect.anchorMax = new Vector2(0.5f, 0f);
        menuButtonRect.pivot = new Vector2(0.5f, 0f);
        menuButtonRect.anchoredPosition = new Vector2(96f, 24f);
        menuButtonRect.sizeDelta = new Vector2(176f, 44f);

        Image menuButtonImage = menuButtonObject.GetComponent<Image>();
        menuButtonImage.color = new Color(0.12f, 0.08f, 0.19f, 1f);
        AddOutline(menuButtonObject, boxBorder, 2f);

        victoryMainMenuButton = menuButtonObject.GetComponent<Button>();
        victoryMainMenuButton.onClick.AddListener(() => { GalacticSlimefallAudio.PlayUiClick(); SceneLoader.Instance?.LoadMainMenu(); });

        Text menuButtonText = CreateText("Label", menuButtonObject.transform, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
        menuButtonText.text = "MAIN MENU";
        menuButtonText.color = timerYellow;
        menuButtonText.rectTransform.anchorMin = Vector2.zero;
        menuButtonText.rectTransform.anchorMax = Vector2.one;
        menuButtonText.rectTransform.offsetMin = Vector2.zero;
        menuButtonText.rectTransform.offsetMax = Vector2.zero;

        CanvasGroup group = victoryRoot.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        victoryRoot.SetActive(false);
    }

    IEnumerator ShowBanner(string label, bool playerTurn)
    {
        if (bannerRoot == null || bannerText == null)
        {
            yield break;
        }

        bannerRoot.SetActive(true);
        CanvasGroup group = bannerRoot.GetComponent<CanvasGroup>();
        RectTransform rect = bannerRoot.GetComponent<RectTransform>();
        Color panelColor = playerTurn ? yourPanel : enemyPanel;
        Color borderColor = playerTurn ? yourBorder : enemyBorder;
        Color glowColor = playerTurn ? yourGlow : enemyGlow;
        Color textColor = playerTurn ? timerGreen : timerRed;

        bannerGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.24f);
        bannerFrame.color = panelColor;
        bannerInnerPanel.color = playerTurn
            ? new Color(0.02f, 0.1f, 0.12f, 0.9f)
            : new Color(0.13f, 0.03f, 0.03f, 0.9f);
        bannerTopRail.color = borderColor;
        bannerBottomRail.color = borderColor;
        bannerLeftCap.color = borderColor;
        bannerRightCap.color = borderColor;
        bannerLeftChevrons.color = textColor;
        bannerRightChevrons.color = textColor;
        bannerText.text = label;
        bannerText.color = textColor;

        Outline frameOutline = bannerFrame.GetComponent<Outline>();
        if (frameOutline != null)
        {
            frameOutline.effectColor = borderColor;
        }

        Shadow frameShadow = bannerFrame.GetComponent<Shadow>();
        if (frameShadow != null)
        {
            frameShadow.effectColor = glowColor;
            frameShadow.effectDistance = Vector2.zero;
        }

        Outline innerOutline = bannerInnerPanel.GetComponent<Outline>();
        if (innerOutline != null)
        {
            innerOutline.effectColor = playerTurn
                ? new Color(0.07f, 0.54f, 0.42f, 0.82f)
                : new Color(0.62f, 0.12f, 0.1f, 0.82f);
        }

        float t = 0f;
        Vector3 startScale = new Vector3(0.9f, 0.9f, 1f);
        Vector3 endScale = Vector3.one;
        rect.localScale = startScale;
        group.alpha = 0f;
        rect.anchoredPosition = Vector2.zero;

        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / 0.22f);
            float eased = 1f - Mathf.Pow(1f - lerp, 3f);
            group.alpha = eased;
            rect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        group.alpha = 1f;
        rect.localScale = endScale;
        yield return new WaitForSeconds(0.92f);

        t = 0f;
        while (t < 0.24f)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / 0.24f);
            group.alpha = 1f - lerp;
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 16f, lerp));
            yield return null;
        }

        bannerRoot.SetActive(false);
        group.alpha = 0f;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void RefreshTimer()
    {
        if (timerValueText == null || timerBarRoot == null)
        {
            return;
        }

        int total = 20;
        int current = 0;
        if (GameManager.Instance != null)
        {
            total = Mathf.Max(1, Mathf.CeilToInt(GameManager.Instance.turnDuration));
            current = Mathf.Clamp(Mathf.CeilToInt(GameManager.Instance.TurnTimeRemaining), 0, total);
        }

        EnsureTimerSegments(total);
        timerValueText.text = current.ToString();

        Color activeColor = current > total * 0.5f ? timerGreen : (current > total * 0.25f ? timerYellow : timerRed);
        timerValueText.color = activeColor;

        for (int i = 0; i < timerSegments.Count; i++)
        {
            Image segment = timerSegments[i];
            bool active = i < current;
            if (!active)
            {
                segment.color = timerSegmentOff;
                continue;
            }

            segment.color = activeColor;
        }
    }

    void EnsureTimerSegments(int count)
    {
        while (timerSegments.Count > count)
        {
            Image image = timerSegments[timerSegments.Count - 1];
            timerSegments.RemoveAt(timerSegments.Count - 1);
            if (image != null)
            {
                Destroy(image.gameObject);
            }
        }

        for (int i = timerSegments.Count; i < count; i++)
        {
            GameObject segmentObject = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(Image));
            segmentObject.transform.SetParent(timerBarRoot, false);
            Image image = segmentObject.GetComponent<Image>();
            image.color = timerSegmentOff;
            image.raycastTarget = false;
            AddOutline(segmentObject, timerSegmentBorder, 1f);
            timerSegments.Add(image);
        }

        float availableWidth = Mathf.Max(0f, Mathf.Round(timerBarRoot.rect.width));
        float spacing = availableWidth >= 180f ? 3f : 2f;
        float width = count > 0 ? Mathf.Floor((availableWidth - (count - 1) * spacing) / count) : 6f;
        if (count > 0 && width < 4f)
        {
            spacing = 1f;
            width = Mathf.Max(3f, Mathf.Floor((availableWidth - (count - 1) * spacing) / count));
        }

        float height = Mathf.Clamp(Mathf.Floor(timerBarRoot.rect.height) - 2f, 8f, 12f);
        float usedWidth = count > 0 ? count * width + (count - 1) * spacing : 0f;
        float startX = Mathf.Round((availableWidth - usedWidth) * 0.5f);
        for (int i = 0; i < timerSegments.Count; i++)
        {
            RectTransform rect = timerSegments[i].rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + Mathf.Round(i * (width + spacing)), 0f);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    void ShowVictory(bool playerWon)
    {
        if (victoryRoot == null)
        {
            return;
        }

        CanvasGroup group = victoryRoot.GetComponent<CanvasGroup>();
        victoryRoot.transform.SetAsLastSibling();
        victoryRoot.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        bool hotseat = GameManager.Instance != null && GameManager.Instance.CurrentMatchMode == GameManager.MatchMode.Hotseat;
        if (hotseat)
        {
            victoryTitleText.text = playerWon ? "PLAYER ONE WINS" : "PLAYER TWO WINS";
            victoryTitleText.color = playerWon ? timerGreen : timerRed;
            victorySubText.text = string.Empty;
            victorySubText.color = new Color(0f, 0f, 0f, 0f);
            if (victoryRestartButton != null)
            {
                victoryRestartButton.gameObject.SetActive(true);
            }
            if (victoryMainMenuButton != null)
            {
                victoryMainMenuButton.gameObject.SetActive(true);
            }
            return;
        }

        victoryTitleText.text = playerWon ? "VICTORY!" : "YOU LOST";
        victoryTitleText.color = playerWon ? timerYellow : timerRed;
        victorySubText.text = playerWon ? "ROUND COMPLETE" : "TRY AGAIN";
        victorySubText.color = playerWon ? softLabel : new Color(0.58f, 0.22f, 0.12f, 1f);
        if (victoryRestartButton != null)
        {
            victoryRestartButton.gameObject.SetActive(true);
        }
        if (victoryMainMenuButton != null)
        {
            victoryMainMenuButton.gameObject.SetActive(true);
        }
    }

    void HideVictory()
    {
        if (victoryRoot == null)
        {
            return;
        }

        CanvasGroup group = victoryRoot.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        victoryRoot.SetActive(false);
    }

    void HideLegacyHud()
    {
        UIManager ui = UIManager.Instance;
        if (ui == null)
        {
            return;
        }

        if (ui.turnPanel != null)
        {
            ui.turnPanel.SetActive(true);
        }

        if (ui.matchModeButton != null)
        {
            ui.matchModeButton.gameObject.SetActive(false);
        }

        if (ui.timerText != null)
        {
            ui.timerText.gameObject.SetActive(true);

            Transform timerParent = ui.timerText.transform.parent;
            if (timerParent != null)
            {
                timerParent.gameObject.SetActive(true);
            }
        }

        if (ui.gameOverPanel != null && (GameManager.Instance == null || !GameManager.Instance.IsGameOver))
        {
            ui.gameOverPanel.SetActive(false);
        }
    }

    void StyleInventory()
    {
    }

    GameObject CreateBox(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);

        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMax.x >= 1f ? 1f : 0.5f, anchorMin.y <= 0f ? 0f : 1f);
        if (anchorMin == anchorMax && anchorMin == new Vector2(0.5f, 0.5f))
        {
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image bg = box.GetComponent<Image>();
        bg.color = boxPanel;

        AddOutline(box, boxBorder, 2f);
        AddShadow(box, boxShadow, new Vector2(2f, -2f));

        RectTransform topLine = EnsureRect(box.transform, "TopLine");
        topLine.anchorMin = new Vector2(0f, 1f);
        topLine.anchorMax = new Vector2(1f, 1f);
        topLine.pivot = new Vector2(0.5f, 1f);
        topLine.offsetMin = new Vector2(8f, -4f);
        topLine.offsetMax = new Vector2(-8f, -3f);
        EnsureImage(topLine.gameObject, new Color(1f, 0.82f, 0.39f, 0.22f));
        return box;
    }

    void ApplyTimerGlassStyle()
    {
        if (timerRoot == null)
        {
            return;
        }

        Image bg = timerRoot.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = Color.clear;
            bg.enabled = false;
        }

        Outline outline = timerRoot.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        Shadow shadow = timerRoot.GetComponent<Shadow>();
        if (shadow != null)
        {
            shadow.enabled = false;
        }

        Transform topLine = timerRoot.transform.Find("TopLine");
        if (topLine != null && topLine.TryGetComponent(out Image topLineImage))
        {
            topLineImage.enabled = false;
            topLineImage.color = Color.clear;
        }
    }

    void CreateScrew(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        GameObject screw = new GameObject(name, typeof(RectTransform), typeof(Image));
        screw.transform.SetParent(parent, false);
        RectTransform rect = screw.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(10f, 10f);

        Image image = screw.GetComponent<Image>();
        image.color = new Color(0.12f, 0.08f, 0.19f, 1f);
        AddOutline(screw, boxBorder, 1.5f);

        GameObject core = new GameObject("Core", typeof(RectTransform), typeof(Image));
        core.transform.SetParent(screw.transform, false);
        RectTransform coreRect = core.GetComponent<RectTransform>();
        coreRect.anchorMin = new Vector2(0.5f, 0.5f);
        coreRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreRect.pivot = new Vector2(0.5f, 0.5f);
        coreRect.anchoredPosition = Vector2.zero;
        coreRect.sizeDelta = new Vector2(3f, 3f);
        core.GetComponent<Image>().color = boxBorder;
    }

    void CreateStar(Transform parent, string name, Vector2 anchoredPosition, float size, Color color)
    {
        GameObject star = new GameObject(name, typeof(RectTransform), typeof(Image));
        star.transform.SetParent(parent, false);
        RectTransform rect = star.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image image = star.GetComponent<Image>();
        image.color = color;
        AddShadow(star, new Color(0.35f, 0.18f, 0f, 0.7f), new Vector2(0f, -2f));

        GameObject starInner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        starInner.transform.SetParent(star.transform, false);
        RectTransform innerRect = starInner.GetComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.5f, 0.5f);
        innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.anchoredPosition = Vector2.zero;
        innerRect.sizeDelta = new Vector2(size * 0.45f, size * 0.45f);
        innerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        starInner.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
    }

    Text CreateText(string name, Transform parent, int size, FontStyle style, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = textWhite;
        t.resizeTextForBestFit = false;
        t.supportRichText = false;
        return t;
    }

    RectTransform EnsureRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }


    Image EnsureImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.AddComponent<Image>();
        }

        image.color = color;
        return image;
    }

    void AddOutline(GameObject target, Color color, float width)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.AddComponent<Outline>();
        }

        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }

    void AddShadow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = target.AddComponent<Shadow>();
        }

        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

}



















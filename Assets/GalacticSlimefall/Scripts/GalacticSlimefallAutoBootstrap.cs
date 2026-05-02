using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
public static class GalacticSlimefallAutoBootstrap
{
    const string SupportedScenePath = "Assets/GalacticSlimefall/Scenes/SampleScene.unity";
    static Sprite _whiteSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BuildIfNeeded()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.path, SupportedScenePath, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Object.FindAnyObjectByType<GameManager>() != null || Object.FindAnyObjectByType<Unit>() != null)
        {
            return;
        }

        EnsureEventSystem();

        Camera mainCamera = EnsureCamera();
        var cameraController = mainCamera.GetComponent<CameraController>();
        if (cameraController == null)
        {
            cameraController = mainCamera.gameObject.AddComponent<CameraController>();
        }

        cameraController.followSmoothTime = 0.24f;
        cameraController.offset = new Vector3(0f, 1f, -10f);

        GameObject managerRoot = new("GameManager");
        var audioSource = managerRoot.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        var gameManager = managerRoot.AddComponent<GameManager>();
        gameManager.delayBetweenTurns = 1f;
        managerRoot.AddComponent<TurnManager>();
        var sceneLoader = managerRoot.AddComponent<SceneLoader>();

        CanvasGroup fadeOverlay;
        BuildHud(sceneLoader, out fadeOverlay);
        sceneLoader.fadeOverlay = fadeOverlay;

        GameObject explosionTemplate = BuildExplosionTemplate(managerRoot.transform);
        GameObject projectileTemplate = BuildProjectileTemplate(managerRoot.transform, explosionTemplate);

        BuildEnvironment();
        BuildUnits(projectileTemplate);

        cameraController.ReturnToDefault();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new("EventSystem");
        eventSystem.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    static Camera EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraGo = new("Main Camera");
            camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
        }

        camera.orthographic = true;
        camera.orthographicSize = 5.6f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.backgroundColor = new Color(0.56f, 0.82f, 0.95f);

        return camera;
    }

    static void BuildHud(SceneLoader sceneLoader, out CanvasGroup fadeOverlay)
    {
        Font boldFont = RuntimeUiFont.Get(true);

        GameObject canvasGo = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Mathf.Max(1f, Mathf.Round(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f)));
        scaler.referencePixelsPerUnit = 100f;

        UIManager uiManager = canvasGo.GetComponent<UIManager>();

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

        GameObject turnPanel = CreateUiBlock("TurnPanel", canvasRect, new Color(0.07f, 0.2f, 0.3f, 0.82f));
        RectTransform turnRect = turnPanel.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.5f, 1f);
        turnRect.anchorMax = new Vector2(0.5f, 1f);
        turnRect.pivot = new Vector2(0.5f, 1f);
        turnRect.sizeDelta = new Vector2(280f, 72f);
        turnRect.anchoredPosition = new Vector2(0f, -34f);

        Image turnPanelImage = turnPanel.GetComponent<Image>();
        if (turnPanelImage != null)
        {
            turnPanelImage.color = Color.clear;
            turnPanelImage.enabled = false;
        }

        Text turnText = CreateText("TurnText", turnRect, boldFont, 32, FontStyle.Bold, Color.white, "Your Turn");
        Stretch(turnText.rectTransform, Vector2.zero, Vector2.zero);

        GameObject gameOverPanel = CreateUiBlock("GameOverPanel", canvasRect, new Color(0.03f, 0.08f, 0.12f, 0.92f));
        RectTransform gameOverRect = gameOverPanel.GetComponent<RectTransform>();
        gameOverRect.anchorMin = new Vector2(0.5f, 0.5f);
        gameOverRect.anchorMax = new Vector2(0.5f, 0.5f);
        gameOverRect.pivot = new Vector2(0.5f, 0.5f);
        gameOverRect.sizeDelta = new Vector2(420f, 240f);
        gameOverRect.anchoredPosition = Vector2.zero;

        Text gameOverText = CreateText("GameOverText", gameOverRect, boldFont, 42, FontStyle.Bold, Color.white, "You Lost");
        Stretch(gameOverText.rectTransform, new Vector2(24f, 120f), new Vector2(24f, 24f));

        Button restartButton = CreateButton(gameOverRect, boldFont, "Restart", new Vector2(180f, 52f), new Vector2(0f, -64f));

        GameObject hpBarsRoot = new("HealthBarsRoot", typeof(RectTransform));
        hpBarsRoot.transform.SetParent(canvasRect, false);
        Stretch(hpBarsRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject hpBarTemplate = BuildHealthBarTemplate(hpBarsRoot.transform);
        hpBarTemplate.SetActive(false);

        GameObject fade = CreateUiBlock("FadeOverlay", canvasRect, new Color(0f, 0f, 0f, 1f));
        fadeOverlay = fade.AddComponent<CanvasGroup>();
        fadeOverlay.alpha = 0f;
        Stretch(fade.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        fade.SetActive(false);

        uiManager.turnPanel = turnPanel;
        uiManager.turnText = turnText;
        uiManager.gameOverPanel = gameOverPanel;
        uiManager.gameOverText = gameOverText;
        uiManager.restartButton = restartButton;
        uiManager.hpBarPrefab = hpBarTemplate;
        uiManager.hpBarsRoot = hpBarsRoot.transform;

        sceneLoader.fadeOverlay = fadeOverlay;
    }

    static GameObject BuildHealthBarTemplate(Transform parent)
    {
        GameObject root = new("HPBarTemplate", typeof(RectTransform), typeof(CanvasGroup), typeof(WorldSpaceFollow), typeof(Image), typeof(Slider));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(112f, 16f);

        Image background = root.GetComponent<Image>();
        background.sprite = GetWhiteSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color(0.05f, 0.06f, 0.08f, 0.94f);
        background.raycastTarget = false;

        Outline border = root.GetComponent<Outline>();
        if (border == null)
        {
            border = root.AddComponent<Outline>();
        }

        border.effectColor = new Color(1f, 1f, 1f, 0.14f);
        border.effectDistance = new Vector2(1f, -1f);
        border.useGraphicAlpha = true;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Slider slider = root.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;

        GameObject fillArea = new("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(2f, 2f), new Vector2(2f, 2f));

        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect, Vector2.zero, Vector2.zero);

        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.3f, 0.8f, 0.4f, 1f);
        fillImage.raycastTarget = false;

        GameObject topGloss = new("TopGloss", typeof(RectTransform), typeof(Image));
        topGloss.transform.SetParent(root.transform, false);
        RectTransform glossRect = topGloss.GetComponent<RectTransform>();
        glossRect.anchorMin = new Vector2(0f, 0.5f);
        glossRect.anchorMax = new Vector2(1f, 1f);
        glossRect.offsetMin = new Vector2(2f, -1f);
        glossRect.offsetMax = new Vector2(-2f, -2f);

        Image glossImage = topGloss.GetComponent<Image>();
        glossImage.sprite = GetWhiteSprite();
        glossImage.type = Image.Type.Sliced;
        glossImage.color = new Color(1f, 1f, 1f, 0.08f);
        glossImage.raycastTarget = false;

        GameObject dividerOverlay = new("DividerOverlay", typeof(RectTransform));
        dividerOverlay.transform.SetParent(root.transform, false);
        Stretch(dividerOverlay.GetComponent<RectTransform>(), new Vector2(2f, 2f), new Vector2(2f, 2f));

        slider.fillRect = fillRect;
        slider.targetGraphic = background;
        slider.handleRect = null;

        return root;
    }

    static GameObject BuildProjectileTemplate(Transform parent, GameObject explosionTemplate)
    {
        GameObject projectile = new("ProjectileTemplate", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(ProjectileController));
        projectile.transform.SetParent(parent, false);
        projectile.SetActive(false);

        SpriteRenderer spriteRenderer = projectile.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = new Color(0.13f, 0.14f, 0.18f);
        spriteRenderer.sortingOrder = 15;
        projectile.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        body.gravityScale = 1f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D circle = projectile.GetComponent<CircleCollider2D>();
        circle.radius = 0.5f;

        ProjectileController controller = projectile.GetComponent<ProjectileController>();
        controller.directDamage = 1;
        controller.splashDamage = 1;
        controller.splashRadius = 1.35f;
        controller.lifetime = 8f;
        controller.hitLayers = ~0;
        controller.explosionPrefab = explosionTemplate;

        return projectile;
    }

    static GameObject BuildExplosionTemplate(Transform parent)
    {
        GameObject explosion = new("ExplosionTemplate", typeof(SpriteRenderer), typeof(ExplosionEffect));
        explosion.transform.SetParent(parent, false);
        explosion.SetActive(false);

        SpriteRenderer spriteRenderer = explosion.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = new Color(1f, 0.72f, 0.18f, 0.85f);
        spriteRenderer.sortingOrder = 20;
        explosion.transform.localScale = new Vector3(0.15f, 0.15f, 1f);

        ExplosionEffect effect = explosion.GetComponent<ExplosionEffect>();
        effect.duration = 0.35f;
        effect.maxScale = 2.6f;
        effect.ring = spriteRenderer;

        return explosion;
    }

    static void BuildEnvironment()
    {
        GameObject world = new("World");

        CreateSpriteBlock("SkyTint", world.transform, new Vector3(0f, 2.5f, 0f), new Vector3(30f, 14f, 1f), new Color(0.86f, 0.95f, 1f), -10);
        CreateSpriteBlock("Water", world.transform, new Vector3(0f, -4.15f, 0f), new Vector3(30f, 3.8f, 1f), new Color(0.11f, 0.47f, 0.76f), -5, true);
        CreateSpriteBlock("Shore", world.transform, new Vector3(0f, -2.55f, 0f), new Vector3(30f, 0.7f, 1f), new Color(0.92f, 0.82f, 0.54f), -4, true);

        CreatePlatform(world.transform, new Vector2(-7.3f, -1.55f), 2.6f);
        CreatePlatform(world.transform, new Vector2(-4f, -1.2f), 2.2f);
        CreatePlatform(world.transform, new Vector2(4f, -1.15f), 2.2f);
        CreatePlatform(world.transform, new Vector2(7.2f, -1.5f), 2.6f);
    }

    static void BuildUnits(GameObject projectileTemplate)
    {
        CreateUnit("Player 1", new Vector2(-7.3f, -0.82f), true, new Color(0.22f, 0.73f, 0.38f), projectileTemplate, false);
        CreateUnit("Player 2", new Vector2(-4f, -0.48f), true, new Color(0.3f, 0.86f, 0.48f), projectileTemplate, false);
        CreateUnit("Enemy 1", new Vector2(4f, -0.43f), false, new Color(0.87f, 0.3f, 0.23f), projectileTemplate, true);
        CreateUnit("Enemy 2", new Vector2(7.2f, -0.78f), false, new Color(0.78f, 0.2f, 0.2f), projectileTemplate, true);
    }

    static void CreatePlatform(Transform parent, Vector2 position, float width)
    {
        GameObject platform = CreateSpriteBlock("Platform", parent, position, new Vector3(width, 0.34f, 1f), new Color(0.54f, 0.34f, 0.16f), 0, true);
        if (platform.TryGetComponent<BoxCollider2D>(out var collider))
        {
            collider.size = new Vector2(1f, 1f);
        }
    }

    static void CreateUnit(string name, Vector2 position, bool isPlayer, Color color, GameObject projectileTemplate, bool addAi)
    {
        GameObject unit = new(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D), typeof(AudioSource), typeof(Unit), typeof(ProjectileLauncher));
        unit.transform.position = position;
        unit.transform.localScale = new Vector3(0.7f, 1f, 1f);

        SpriteRenderer spriteRenderer = unit.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 10;

        Rigidbody2D body = unit.GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = unit.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 1f);

        GameObject aimAnchor = new("AimAnchor");
        aimAnchor.transform.SetParent(unit.transform, false);
        aimAnchor.transform.localPosition = new Vector3(0f, 0.58f, 0f);

        Unit unitComponent = unit.GetComponent<Unit>();
        unitComponent.maxHP = 100;
        unitComponent.isPlayerTeam = isPlayer;
        unitComponent.bodyRenderer = spriteRenderer;
        unitComponent.aimAnchor = aimAnchor.transform;
        unitComponent.knockbackForce = 2.6f;

        ProjectileLauncher launcher = unit.GetComponent<ProjectileLauncher>();
        launcher.projectilePrefab = projectileTemplate;
        launcher.isPlayerProjectile = isPlayer;

        if (addAi)
        {
            EnemyAI ai = unit.AddComponent<EnemyAI>();
            ai.accuracy = 0.7f;
            ai.launchSpeed = 14f;
        }

        if (isPlayer)
        {
            EnsureAimingSystem();
        }
    }

    static void EnsureAimingSystem()
    {
        if (Object.FindAnyObjectByType<AimingSystem>() != null)
        {
            return;
        }

        GameObject aiming = new("AimingSystem", typeof(AimingSystem), typeof(LineRenderer));

        LineRenderer line = aiming.GetComponent<LineRenderer>();
        line.enabled = false;
        line.widthMultiplier = 0.06f;
        line.positionCount = 0;
        line.numCapVertices = 8;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1f, 1f, 1f, 0.95f);
        line.endColor = new Color(1f, 0.82f, 0.38f, 0.95f);
        line.sortingOrder = 25;

        AimingSystem aimingSystem = aiming.GetComponent<AimingSystem>();
        aimingSystem.trajectoryLine = line;
        aimingSystem.maxPower = 18f;
        aimingSystem.minPower = 4f;
        aimingSystem.powerPerPixel = 0.04f;
    }

    static GameObject CreateSpriteBlock(string name, Transform parent, Vector3 position, Vector3 scale, Color color, int sortingOrder, bool addCollider = false)
    {
        GameObject go = new(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        SpriteRenderer spriteRenderer = go.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetWhiteSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        if (addCollider)
        {
            go.AddComponent<BoxCollider2D>();
        }

        return go;
    }

    static GameObject CreateUiBlock(string name, Transform parent, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        return go;
    }

    static Text CreateText(string name, Transform parent, Font font, int fontSize, FontStyle fontStyle, Color color, string value)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        RuntimeUiFont.Apply(text, fontStyle == FontStyle.Bold || fontStyle == FontStyle.BoldAndItalic);
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    static Button CreateButton(Transform parent, Font font, string label, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject buttonGo = CreateUiBlock("RestartButton", parent, new Color(0.2f, 0.54f, 0.87f, 1f));
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonGo.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        Button button = buttonGo.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.54f, 0.87f, 1f);
        colors.highlightedColor = new Color(0.27f, 0.62f, 0.95f, 1f);
        colors.pressedColor = new Color(0.14f, 0.43f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Label", rect, font, 26, FontStyle.Bold, Color.white, label);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);

        return button;
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = -offsetMax;
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _whiteSprite.name = "RuntimeWhiteSprite";
        return _whiteSprite;
    }

}


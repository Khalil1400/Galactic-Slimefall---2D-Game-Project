using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
public class AimingSystem : MonoBehaviour
{
    public static AimingSystem Instance { get; private set; }

    [Header("Aiming")]
    public float maxPower = 18f;
    public float minPower = 3f;
    public float powerPerPixel = 0.025f;

    [Header("Trajectory")]
    public int previewSteps = 12;
    public float previewTimeStep = 0.03f;
    public float previewMaxDistance = 4.8f;
    public LayerMask previewCollisionLayers = ~0;
    public LineRenderer trajectoryLine;
    public SpriteRenderer impactMarker;

    [Header("Fallback Launcher")]
    public ProjectileLauncher launcher;

    bool _canAim;
    bool _isAiming;
    bool _subscribed;
    Vector2 _dragStart;
    Vector2 _aimDirection;
    float _power;
    static readonly List<RaycastResult> UiRaycastResults = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        previewSteps = Mathf.Clamp(previewSteps, 8, 12);
        previewTimeStep = Mathf.Clamp(previewTimeStep, 0.024f, 0.03f);
        previewMaxDistance = Mathf.Clamp(previewMaxDistance, 3.2f, 4.8f);
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        if (!_subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnPlayerTurnStart -= EnableAiming;
        GameManager.Instance.OnEnemyTurnStart -= EnableAiming;
        GameManager.Instance.OnGameOver -= HandleGameOver;
        GameManager.Instance.OnMatchModeChanged -= HandleMatchModeChanged;
        _subscribed = false;
    }

    void Update()
    {
        if (!_canAim || MainMenuController.IsMenuOpen)
        {
            return;
        }

        Vector2 pointer = GetPointerScreenPosition();

        if (PointerDown())
        {
            if (IsPointerOverBlockingUi(pointer))
            {
                return;
            }

            _isAiming = true;
            _dragStart = pointer;
            _aimDirection = Vector2.zero;
            _power = 0f;
        }

        if (_isAiming && PointerHeld())
        {
            Vector2 drag = pointer - _dragStart;
            if (drag.sqrMagnitude > 1f)
            {
                _aimDirection = drag.normalized;
                _power = Mathf.Clamp(drag.magnitude * powerPerPixel, minPower, maxPower);
                DrawTrajectory(GetFireOrigin(), _aimDirection * _power);
            }
        }

        if (_isAiming && PointerUp())
        {
            _isAiming = false;
            HideTrajectory();

            if (_aimDirection != Vector2.zero)
            {
                Fire();
            }
        }
    }

    public void EnableAiming()
    {
        _canAim = GameManager.Instance != null &&
                  !MainMenuController.IsMenuOpen &&
                  GameManager.Instance.IsCurrentTurnHumanControlled &&
                  !GameManager.Instance.IsTurnTransitioning;
    }

    public void DisableAiming()
    {
        _canAim = false;
        _isAiming = false;
        _aimDirection = Vector2.zero;
        HideTrajectory();
    }

    void HandleGameOver(bool _)
    {
        DisableAiming();
    }

    void HandleMatchModeChanged(GameManager.MatchMode _)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
        {
            DisableAiming();
            return;
        }

        EnableAiming();
    }

    void Fire()
    {
        ProjectileLauncher activeLauncher = GetLauncher();
        Unit activeUnit = TurnManager.Instance?.GetActiveUnitForCurrentTurn();
        if (activeLauncher == null)
        {
            Debug.LogWarning("AimingSystem: no ProjectileLauncher found on the active player unit.");
            return;
        }

        GameObject projectilePrefab = activeLauncher.projectilePrefab;
        WeaponInventory.WeaponEntry selectedWeapon = null;

        if (WeaponInventory.Instance != null && WeaponInventory.Instance.HasWeapons)
        {
            if (!WeaponInventory.Instance.TryBeginShot(activeUnit, out selectedWeapon, out projectilePrefab))
            {
                return;
            }
        }

        ProjectileController projectile = activeLauncher.Launch(GetFireOrigin(), _aimDirection * _power, projectilePrefab);
        if (projectile == null)
        {
            return;
        }

        if (selectedWeapon != null)
        {
            WeaponInventory.Instance?.CommitShot(activeUnit, selectedWeapon);
        }

        DisableAiming();
        GameManager.Instance?.EndCurrentTurn();
    }

    void DrawTrajectory(Vector2 origin, Vector2 velocity)
    {
        if (trajectoryLine == null)
        {
            return;
        }

        trajectoryLine.enabled = true;
        HideImpactMarker();

        List<Vector3> points = new(previewSteps);
        points.Add(origin);

        Vector2 position = origin;
        Vector2 currentVelocity = velocity;
        Vector2 gravity = Physics2D.gravity * GetSelectedGravityScale();
        float drag = GetSelectedDrag();
        Unit activeUnit = TurnManager.Instance?.GetActiveUnitForCurrentTurn();
        float traveledDistance = 0f;

        for (int i = 1; i < previewSteps; i++)
        {
            currentVelocity += gravity * previewTimeStep;
            currentVelocity *= Mathf.Max(0f, 1f - drag * previewTimeStep);

            Vector2 nextPosition = position + currentVelocity * previewTimeStep;
            traveledDistance += Vector2.Distance(position, nextPosition);
            RaycastHit2D hit = Physics2D.Linecast(position, nextPosition, previewCollisionLayers);
            if (hit.collider != null && !ShouldIgnorePreviewHit(hit.collider, activeUnit, position, nextPosition, currentVelocity))
            {
                points.Add(hit.point);
                ShowImpactMarker(hit.point);
                break;
            }

            points.Add(nextPosition);
            if (traveledDistance >= previewMaxDistance)
            {
                break;
            }

            position = nextPosition;
        }

        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    void HideTrajectory()
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = false;
        }

        HideImpactMarker();
    }

    Vector2 GetFireOrigin()
    {
        ProjectileLauncher activeLauncher = GetLauncher();
        if (activeLauncher != null)
        {
            Unit launcherUnit = activeLauncher.GetComponent<Unit>() ?? activeLauncher.GetComponentInParent<Unit>();
            if (launcherUnit != null)
            {
                return launcherUnit.AimTarget;
            }
        }

        Unit active = TurnManager.Instance?.GetActiveUnitForCurrentTurn();
        return active != null ? active.AimTarget : (Vector2)transform.position;
    }

    ProjectileLauncher GetLauncher()
    {
        if (launcher != null)
        {
            return launcher;
        }

        Unit active = TurnManager.Instance?.GetActiveUnitForCurrentTurn();
        ProjectileLauncher activeLauncher = active != null
            ? active.GetComponent<ProjectileLauncher>() ?? active.GetComponentInChildren<ProjectileLauncher>()
            : null;
        if (activeLauncher != null)
        {
            return activeLauncher;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            List<Unit> team = manager.IsEnemyTurn ? manager.enemyUnits : manager.playerUnits;
            if (team != null)
            {
                foreach (Unit unit in team)
                {
                    if (unit == null || unit.IsDead)
                    {
                        continue;
                    }

                    ProjectileLauncher fallbackLauncher = unit.GetComponent<ProjectileLauncher>() ?? unit.GetComponentInChildren<ProjectileLauncher>();
                    if (fallbackLauncher != null)
                    {
                        return fallbackLauncher;
                    }
                }
            }
        }

        bool wantPlayerTeam = manager == null || !manager.IsEnemyTurn;
        foreach (Unit unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            if (unit == null || unit.IsDead || unit.isPlayerTeam != wantPlayerTeam)
            {
                continue;
            }

            ProjectileLauncher sceneLauncher = unit.GetComponent<ProjectileLauncher>() ?? unit.GetComponentInChildren<ProjectileLauncher>();
            if (sceneLauncher != null)
            {
                return sceneLauncher;
            }
        }

        foreach (ProjectileLauncher sceneLauncher in FindObjectsByType<ProjectileLauncher>(FindObjectsSortMode.None))
        {
            if (sceneLauncher == null || !sceneLauncher.isActiveAndEnabled)
            {
                continue;
            }

            Unit launcherUnit = sceneLauncher.GetComponent<Unit>() ?? sceneLauncher.GetComponentInParent<Unit>();
            if (launcherUnit != null)
            {
                if (launcherUnit.IsDead || launcherUnit.isPlayerTeam != wantPlayerTeam)
                {
                    continue;
                }

                return sceneLauncher;
            }

            if (sceneLauncher.isPlayerProjectile == wantPlayerTeam)
            {
                return sceneLauncher;
            }
        }

        return null;
    }

    float GetSelectedGravityScale()
    {
        Rigidbody2D projectileBody = GetSelectedProjectileBody();
        return projectileBody != null ? projectileBody.gravityScale : 1f;
    }

    float GetSelectedDrag()
    {
        Rigidbody2D projectileBody = GetSelectedProjectileBody();
        return projectileBody != null ? projectileBody.linearDamping : 0f;
    }

    Rigidbody2D GetSelectedProjectileBody()
    {
        ProjectileLauncher activeLauncher = GetLauncher();
        if (activeLauncher == null)
        {
            return null;
        }

        GameObject projectilePrefab = WeaponInventory.Instance != null
            ? WeaponInventory.Instance.GetSelectedProjectilePrefab(activeLauncher.projectilePrefab)
            : activeLauncher.projectilePrefab;

        return projectilePrefab != null ? projectilePrefab.GetComponent<Rigidbody2D>() : null;
    }

    bool ShouldIgnorePreviewHit(Collider2D collider, Unit activeUnit, Vector2 from, Vector2 to, Vector2 velocity)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (activeUnit == null)
        {
            return false;
        }

        Unit hitUnit = collider.GetComponent<Unit>() ?? collider.GetComponentInParent<Unit>() ?? collider.attachedRigidbody?.GetComponent<Unit>();
        if (hitUnit == activeUnit)
        {
            return true;
        }

        if (collider.GetComponent<MapBoundaryFrame>() != null ||
            collider.GetComponentInParent<MapBoundaryFrame>() != null)
        {
            return true;
        }

        if (ShouldPassThroughOneWayPlatform(collider, from, to, velocity))
        {
            return true;
        }

        if (!IsBypassCandidate(collider))
        {
            return false;
        }

        return !HasUnitOccupyingPlatform(collider) && !CoverPlatformRules.ShouldActAsCover(collider);
    }

    void ShowImpactMarker(Vector2 position)
    {
        if (impactMarker == null)
        {
            return;
        }

        impactMarker.enabled = true;
        impactMarker.transform.position = position;
    }

    void HideImpactMarker()
    {
        if (impactMarker != null)
        {
            impactMarker.enabled = false;
        }
    }

    static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
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

    static bool PointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
               (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
#else
        return Input.GetMouseButtonDown(0) ||
               (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
#endif
    }

    static bool PointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
               (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);
#else
        return Input.GetMouseButton(0) ||
               (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved ||
                                         Input.GetTouch(0).phase == TouchPhase.Stationary));
#endif
    }

    static bool PointerUp()
    {
#if ENABLE_INPUT_SYSTEM
        return (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
               (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame);
#else
        return Input.GetMouseButtonUp(0) ||
               (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Ended ||
                                         Input.GetTouch(0).phase == TouchPhase.Canceled));
#endif
    }

    static bool IsPointerOverBlockingUi(Vector2 screenPosition)
    {
        if (WeaponInventory.Instance != null && WeaponInventory.Instance.IsPointerOverInteractiveUi(screenPosition))
        {
            return true;
        }

        if (EventSystem.current == null)
        {
            return false;
        }

        UiRaycastResults.Clear();
        PointerEventData eventData = new(EventSystem.current)
        {
            position = screenPosition
        };

        EventSystem.current.RaycastAll(eventData, UiRaycastResults);
        foreach (RaycastResult result in UiRaycastResults)
        {
            GameObject hitObject = result.gameObject;
            if (hitObject == null || !hitObject.activeInHierarchy)
            {
                continue;
            }

            CanvasGroup canvasGroup = hitObject.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && !canvasGroup.blocksRaycasts)
            {
                continue;
            }

            Selectable selectable = hitObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }

    void TrySubscribe()
    {
        if (_subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnPlayerTurnStart += EnableAiming;
        GameManager.Instance.OnEnemyTurnStart += EnableAiming;
        GameManager.Instance.OnGameOver += HandleGameOver;
        GameManager.Instance.OnMatchModeChanged += HandleMatchModeChanged;
        _subscribed = true;
    }

    static bool HasUnitOccupyingPlatform(Collider2D collider)
    {
        Bounds bounds = collider.bounds;
        Vector2 probeCenter = new(bounds.center.x, bounds.max.y + 0.325f);
        Vector2 probeSize = new(bounds.size.x * 0.92f, 0.65f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);
        foreach (Collider2D hit in hits)
        {
            Unit unit = hit.GetComponent<Unit>() ?? hit.GetComponentInParent<Unit>() ?? hit.attachedRigidbody?.GetComponent<Unit>();
            if (unit != null && !unit.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    static bool IsBypassCandidate(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return false;
        }

        if (collider.GetComponent<MapBoundaryFrame>() != null ||
            collider.GetComponentInParent<MapBoundaryFrame>() != null)
        {
            return true;
        }

        if (collider.GetComponent<MovementOnlyBlocker>() != null ||
            collider.GetComponentInParent<MovementOnlyBlocker>() != null)
        {
            return true;
        }

        if (collider.GetComponent<HazardDamage>() != null ||
            collider.GetComponentInParent<HazardDamage>() != null ||
            collider.GetComponent<JumpPad>() != null ||
            collider.GetComponentInParent<JumpPad>() != null)
        {
            return false;
        }

        PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>() ?? collider.GetComponentInParent<PlatformEffector2D>();
        if (effector != null || collider.usedByEffector)
        {
            return true;
        }

        Bounds bounds = collider.bounds;
        if (bounds.size.y > 0.8f || bounds.size.x < bounds.size.y * 1.8f)
        {
            return false;
        }

        string objectName = collider.gameObject.name.ToLowerInvariant();
        string parentName = collider.transform.parent != null ? collider.transform.parent.name.ToLowerInvariant() : string.Empty;
        return objectName.Contains("float") || parentName.Contains("float");
    }

    static bool ShouldPassThroughOneWayPlatform(Collider2D collider, Vector2 from, Vector2 to, Vector2 velocity)
    {
        PlatformEffector2D effector = collider != null
            ? collider.GetComponent<PlatformEffector2D>() ?? collider.GetComponentInParent<PlatformEffector2D>()
            : null;
        if (collider == null || (effector == null && !collider.usedByEffector))
        {
            return false;
        }

        if (CoverPlatformRules.ShouldActAsCover(collider))
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        float platformTop = bounds.max.y;
        float tolerance = Mathf.Max(0.04f, bounds.size.y * 0.4f);
        bool descendingOntoTop = velocity.y < -0.35f && from.y >= platformTop - tolerance;
        return !descendingOntoTop;
    }
}


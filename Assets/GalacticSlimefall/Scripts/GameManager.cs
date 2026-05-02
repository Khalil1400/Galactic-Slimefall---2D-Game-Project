using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        PlayerTurn,
        EnemyTurn,
        GameOver
    }

    public enum MatchMode
    {
        VsAi,
        Hotseat
    }

    public static GameManager Instance { get; private set; }

    [Header("Teams")]
    public List<Unit> playerUnits = new();
    public List<Unit> enemyUnits = new();

    [Header("Settings")]
    public float delayBetweenTurns = 1.15f;
    public float minimumProjectilePause = 0.2f;
    public float turnDuration = 20f;
    [Range(0.1f, 1f)]
    public float projectileDamageScale = 1f;

    [Header("Hit Focus")]
    public float hitFocusDuration = 1f;
    public float hitPopupHoldDuration = 0.35f;

    [Header("Projectile Bounds")]
    public Vector2 projectileBoundsPadding = new(2.8f, 2.5f);
    public bool recomputeProjectileBoundsOnStart = true;

    public GameState State { get; private set; }
    public MatchMode CurrentMatchMode { get; private set; } = MatchMode.VsAi;
    public float TurnTimeRemaining { get; private set; }
    public int CurrentRound { get; private set; } = 1;

    public System.Action<GameState> OnStateChanged;
    public System.Action OnPlayerTurnStart;
    public System.Action OnEnemyTurnStart;
    public System.Action<bool> OnGameOver;
    public System.Action<float> OnTurnTimerChanged;
    public System.Action<MatchMode> OnMatchModeChanged;
    public System.Action<int> OnRoundChanged;

    int _activeProjectileCount;
    float _lastProjectileResolvedAt = float.NegativeInfinity;
    Coroutine _turnTransitionRoutine;
    Coroutine _hitPauseRoutine;
    bool _isTurnTransitioning;
    bool _isHitPauseActive;
    bool _hasStartedPlayerTurn;
    bool _hasStartedEnemyTurn;
    Bounds _projectileBounds;
    bool _hasProjectileBounds;
    readonly Dictionary<Unit, int> _pendingHitDamage = new();
    readonly Queue<Unit> _pendingHitUnits = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    IEnumerator Start()
    {
        yield return null;
        CleanupUnits();
        EnsureHotseatControlComponents();

        if (recomputeProjectileBoundsOnStart)
        {
            RecalculateProjectileBounds();
        }

        StartPlayerTurn();
    }

    void Update()
    {
        if (State == GameState.GameOver || _isTurnTransitioning || _isHitPauseActive)
        {
            return;
        }

        if (State != GameState.PlayerTurn && State != GameState.EnemyTurn)
        {
            return;
        }

        float previous = TurnTimeRemaining;
        TurnTimeRemaining = Mathf.Max(0f, TurnTimeRemaining - Time.deltaTime);
        if (!Mathf.Approximately(previous, TurnTimeRemaining))
        {
            OnTurnTimerChanged?.Invoke(TurnTimeRemaining);
        }

        if (TurnTimeRemaining > 0f)
        {
            return;
        }

        EndCurrentTurn();
    }

    public void StartPlayerTurn()
    {
        if (CheckGameOver())
        {
            return;
        }

        StopTurnTransition();
        _isTurnTransitioning = false;

        if (_hasStartedPlayerTurn)
        {
            TurnManager.Instance?.AdvancePlayerUnit();
            CurrentRound++;
        }
        else
        {
            _hasStartedPlayerTurn = true;
            CurrentRound = Mathf.Max(1, CurrentRound);
        }

        BeginTurn(GameState.PlayerTurn);
        OnRoundChanged?.Invoke(CurrentRound);
        OnPlayerTurnStart?.Invoke();
    }

    public void EndPlayerTurn()
    {
        if (State != GameState.PlayerTurn || _isTurnTransitioning)
        {
            return;
        }

        QueueTurnTransition(GameState.EnemyTurn);
    }

    public void EndEnemyTurn()
    {
        if (State != GameState.EnemyTurn || _isTurnTransitioning)
        {
            return;
        }

        QueueTurnTransition(GameState.PlayerTurn);
    }

    public void EndCurrentTurn()
    {
        if (State == GameState.PlayerTurn)
        {
            EndPlayerTurn();
        }
        else if (State == GameState.EnemyTurn)
        {
            EndEnemyTurn();
        }
    }

    public void NotifyProjectileSpawned(ProjectileController projectile)
    {
        if (projectile == null || State == GameState.GameOver)
        {
            return;
        }

        _activeProjectileCount++;
    }

    public void NotifyProjectileResolved(ProjectileController projectile)
    {
        if (projectile == null)
        {
            return;
        }

        _activeProjectileCount = Mathf.Max(0, _activeProjectileCount - 1);
        _lastProjectileResolvedAt = Time.time;
    }

    public void NotifyProjectileHit(Unit unit, int damageAmount)
    {
        if (unit == null || damageAmount <= 0 || State == GameState.GameOver)
        {
            return;
        }

        if (_pendingHitDamage.TryGetValue(unit, out int existingDamage))
        {
            _pendingHitDamage[unit] = existingDamage + damageAmount;
        }
        else
        {
            _pendingHitDamage[unit] = damageAmount;
            _pendingHitUnits.Enqueue(unit);
        }

        if (_hitPauseRoutine == null)
        {
            _hitPauseRoutine = StartCoroutine(HitPauseRoutine());
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        var team = unit.isPlayerTeam ? playerUnits : enemyUnits;
        if (!team.Contains(unit))
        {
            team.Add(unit);
        }
    }

    public void NotifyUnitDied(Unit unit)
    {
        playerUnits.Remove(unit);
        enemyUnits.Remove(unit);
        CheckGameOver();
    }

    public bool IsHumanControlledUnit(Unit unit)
    {
        if (unit == null)
        {
            return false;
        }

        return unit.isPlayerTeam || CurrentMatchMode == MatchMode.Hotseat;
    }

    public bool IsCurrentTurnHumanControlled
    {
        get
        {
            if (State == GameState.PlayerTurn)
            {
                return true;
            }

            return State == GameState.EnemyTurn && CurrentMatchMode == MatchMode.Hotseat;
        }
    }

    public void CycleMatchMode()
    {
        SetMatchMode(CurrentMatchMode == MatchMode.VsAi ? MatchMode.Hotseat : MatchMode.VsAi);
    }

    public void SetMatchMode(MatchMode mode)
    {
        if (CurrentMatchMode == mode)
        {
            return;
        }

        CurrentMatchMode = mode;
        EnsureHotseatControlComponents();
        OnMatchModeChanged?.Invoke(CurrentMatchMode);

        if (State == GameState.EnemyTurn && !_isTurnTransitioning && _activeProjectileCount == 0 && CurrentMatchMode == MatchMode.VsAi)
        {
            TurnManager.Instance?.RunEnemyTurn();
        }
    }

    public void RecalculateProjectileBounds()
    {
        GameObject levelVisual = GameObject.Find("LevelVisual");
        if (levelVisual != null && levelVisual.TryGetComponent(out SpriteRenderer levelRenderer) && levelRenderer.sprite != null)
        {
            Bounds visualBounds = levelRenderer.bounds;
            visualBounds.Expand(new Vector3(Mathf.Max(0.2f, projectileBoundsPadding.x), Mathf.Max(0.2f, projectileBoundsPadding.y), 0f));
            _projectileBounds = visualBounds;
            _hasProjectileBounds = true;
            return;
        }

        bool hasBounds = false;
        Bounds combined = default;

        foreach (Collider2D collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (collider == null || collider.isTrigger || collider.GetComponent<ProjectileController>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.GetComponent<ProjectileController>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            _hasProjectileBounds = false;
            return;
        }

        combined.Expand(new Vector3(projectileBoundsPadding.x * 2f, projectileBoundsPadding.y * 2f, 0f));
        _projectileBounds = combined;
        _hasProjectileBounds = true;
    }

    public bool IsInsideProjectileBounds(Vector3 position)
    {
        return !_hasProjectileBounds || _projectileBounds.Contains(new Vector3(position.x, position.y, _projectileBounds.center.z));
    }

    bool CheckGameOver()
    {
        CleanupUnits();

        if (playerUnits.Count == 0)
        {
            TriggerGameOver(false);
            return true;
        }

        if (enemyUnits.Count == 0)
        {
            TriggerGameOver(true);
            return true;
        }

        return false;
    }

    void TriggerGameOver(bool playerWon)
    {
        if (State == GameState.GameOver)
        {
            return;
        }

        StopTurnTransition();
        _isTurnTransitioning = false;
        SetState(GameState.GameOver);
        OnTurnTimerChanged?.Invoke(0f);
        OnGameOver?.Invoke(playerWon);
        UIManager.Instance?.ShowGameOver(playerWon);
    }

    void SetState(GameState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(newState);
    }

    void BeginTurn(GameState state)
    {
        TurnTimeRemaining = turnDuration;
        SetState(state);
        OnTurnTimerChanged?.Invoke(TurnTimeRemaining);
    }

    void CleanupUnits()
    {
        playerUnits.RemoveAll(unit => unit == null || unit.IsDead);
        enemyUnits.RemoveAll(unit => unit == null || unit.IsDead);
    }

    void EnsureHotseatControlComponents()
    {
        if (CurrentMatchMode != MatchMode.Hotseat)
        {
            return;
        }

        PlayerMovement template = null;
        foreach (Unit playerUnit in playerUnits)
        {
            if (playerUnit != null)
            {
                template = playerUnit.GetComponent<PlayerMovement>();
                if (template != null)
                {
                    break;
                }
            }
        }

        foreach (Unit enemyUnit in enemyUnits)
        {
            if (enemyUnit == null || enemyUnit.GetComponent<PlayerMovement>() != null)
            {
                continue;
            }

            PlayerMovement movement = enemyUnit.gameObject.AddComponent<PlayerMovement>();
            if (template != null)
            {
                movement.moveSpeed = template.moveSpeed;
                movement.jumpForce = template.jumpForce;
                movement.groundedDistance = template.groundedDistance;
                movement.jumpBufferTime = template.jumpBufferTime;
                movement.coyoteTime = template.coyoteTime;
                movement.extraAirJumps = template.extraAirJumps;
                movement.ledgeAssistHeight = template.ledgeAssistHeight;
                movement.ledgeAssistForwardCheck = template.ledgeAssistForwardCheck;
                movement.groundLayers = template.groundLayers;
            }
        }
    }

    void QueueTurnTransition(GameState nextState)
    {
        StopTurnTransition();
        _turnTransitionRoutine = StartCoroutine(TransitionToState(nextState));
    }

    IEnumerator TransitionToState(GameState nextState)
    {
        _isTurnTransitioning = true;
        OnTurnTimerChanged?.Invoke(0f);

        while (_activeProjectileCount > 0 || _isHitPauseActive)
        {
            yield return null;
        }

        if (_lastProjectileResolvedAt > -999f)
        {
            float remainingPause = minimumProjectilePause - (Time.time - _lastProjectileResolvedAt);
            if (remainingPause > 0f)
            {
                yield return new WaitForSeconds(remainingPause);
            }
        }

        if (delayBetweenTurns > 0f)
        {
            yield return new WaitForSeconds(delayBetweenTurns);
        }

        _turnTransitionRoutine = null;
        _isTurnTransitioning = false;

        if (CheckGameOver())
        {
            yield break;
        }

        if (nextState == GameState.PlayerTurn)
        {
            StartPlayerTurn();
            yield break;
        }

        if (_hasStartedEnemyTurn)
        {
            TurnManager.Instance?.AdvanceEnemyUnit();
        }
        else
        {
            _hasStartedEnemyTurn = true;
        }

        BeginTurn(GameState.EnemyTurn);
        OnEnemyTurnStart?.Invoke();
        TurnManager.Instance?.RunEnemyTurn();
    }

    void StopTurnTransition()
    {
        if (_turnTransitionRoutine == null)
        {
            return;
        }

        StopCoroutine(_turnTransitionRoutine);
        _turnTransitionRoutine = null;
    }

    IEnumerator HitPauseRoutine()
    {
        _isHitPauseActive = true;

        while (_pendingHitUnits.Count > 0)
        {
            Unit unit = _pendingHitUnits.Dequeue();
            if (unit == null)
            {
                continue;
            }

            if (!_pendingHitDamage.TryGetValue(unit, out int damageAmount) || damageAmount <= 0)
            {
                continue;
            }

            CameraController camera = CameraController.Instance;
            if (camera != null)
            {
                camera.FollowTarget(unit.transform, false);
            }

            if (hitFocusDuration > 0f)
            {
                yield return new WaitForSeconds(hitFocusDuration);
            }

            if (_pendingHitDamage.TryGetValue(unit, out int refreshedDamage) && refreshedDamage > 0)
            {
                damageAmount = refreshedDamage;
            }

            _pendingHitDamage.Remove(unit);

            if (unit != null)
            {
                UIManager.Instance?.ShowDamagePopup(unit, damageAmount);
            }

            if (hitPopupHoldDuration > 0f)
            {
                yield return new WaitForSeconds(hitPopupHoldDuration);
            }

            if (camera != null)
            {
                camera.ReturnToDefault(true);
            }
        }

        _isHitPauseActive = false;
        _hitPauseRoutine = null;
    }

    void OnDrawGizmosSelected()
    {
        if (!_hasProjectileBounds)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireCube(_projectileBounds.center, _projectileBounds.size);
    }

    public bool IsPlayerTurn => State == GameState.PlayerTurn;
    public bool IsEnemyTurn => State == GameState.EnemyTurn;
    public bool IsGameOver => State == GameState.GameOver;
    public bool IsTurnTransitioning => _isTurnTransitioning;
    public bool IsHitPauseActive => _isHitPauseActive;
    public bool HasActiveProjectiles => _activeProjectileCount > 0;
}



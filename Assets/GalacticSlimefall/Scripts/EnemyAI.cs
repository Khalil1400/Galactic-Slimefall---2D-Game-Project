using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(ProjectileLauncher))]
[RequireComponent(typeof(Collider2D))]
public class EnemyAI : MonoBehaviour
{
    struct ShotSolution
    {
        public bool isValid;
        public bool directHit;
        public bool hitsPlayer;
        public bool friendlyFire;
        public Vector2 origin;
        public Vector2 velocity;
        public Vector2 destination;
        public float score;
    }

    [Header("AI Tuning")]
    [Range(0f, 1f)]
    public float accuracy = 0.95f;
    public float maxSpreadDegrees = 2.2f;
    public float minThinkTime = 0.5f;
    public float maxThinkTime = 1.05f;
    public float directHitRadius = 0.38f;
    public float perfectShotScoreThreshold = 0.45f;
    public Vector4 accuracyBands = new(0.8f, 0.85f, 0.95f, 1f);
    public Vector4 specialWeaponChanceBands = new(0.15f, 0.2f, 0.25f, 0.35f);

    [Header("Shot Search")]
    public float launchSpeed = 14.5f;
    public float minLaunchSpeed = 8.5f;
    public float maxLaunchSpeed = 16.5f;
    public float speedStep = 0.35f;
    public float minLaunchAngle = 4f;
    public float maxLaunchAngle = 72f;
    public float angleStep = 1.2f;
    public int simulationSteps = 110;
    public float simulationTimeStep = 0.045f;
    public LayerMask simulationMask = ~0;
    public float immediateObstacleDistance = 1.15f;
    public float minimumClearFlightDistance = 2.2f;

    [Header("Repositioning")]
    public bool allowShortReposition = true;
    public float repositionRange = 2.1f;
    public float repositionStep = 0.6f;
    public float maxVerticalRepositionDelta = 0.8f;
    public float moveDuration = 0.32f;
    public float edgeRecoveryMargin = 0.18f;

    [Header("Presentation")]
    public float aimSettleTime = 0.32f;
    public float postMoveSettleTime = 0.18f;

    ProjectileLauncher _launcher;
    Collider2D _collider;
    Unit _unit;
    Rigidbody2D _projectileBodyTemplate;
    System.Action _onComplete;
    WeaponInventory.WeaponEntry _selectedWeapon;
    GameObject _selectedProjectilePrefab;
    float _turnAccuracy = 0.95f;
    float _turnPerfectShotChance = 0.45f;
    float _turnDirectPerfectChance = 0.65f;
    float _turnAimOffsetDegrees;
    float _turnMissBiasDegrees;
    float _turnSpecialWeaponChance = 0.2f;

    void Awake()
    {
        _launcher = GetComponent<ProjectileLauncher>();
        _collider = GetComponent<Collider2D>();
        _unit = GetComponent<Unit>();
        _launcher.isPlayerProjectile = false;
        accuracy = Mathf.Clamp(accuracy, 0.9f, 1f);
        maxSpreadDegrees = Mathf.Clamp(maxSpreadDegrees, 0.15f, 3.25f);
        SyncLegacyLaunchSpeed();
        SetTurnAccuracy(accuracy);
    }

    void OnValidate()
    {
        SyncLegacyLaunchSpeed();
    }

    public void TakeTurn(System.Action onComplete)
    {
        _onComplete = onComplete;
        StopAllCoroutines();
        StartCoroutine(TurnRoutine());
    }

    IEnumerator TurnRoutine()
    {
        yield return new WaitForSeconds(Random.Range(minThinkTime, maxThinkTime));
        RollTurnAccuracy();
        yield return RecoverFromMapEdgeIfNeeded();
        yield return FocusCameraOnSelf(aimSettleTime);

        Unit target = PickTarget();
        if (target != null)
        {
            SelectWeaponForTurn(target);
            ShotSolution solution = FindBestShot(target);
            if (solution.isValid)
            {
                if (allowShortReposition && Vector2.Distance((Vector2)transform.position, solution.destination) > 0.05f)
                {
                    yield return MoveTo(solution.destination);
                    yield return FocusCameraOnSelf(postMoveSettleTime);
                }

                FireSolution(solution);
            }
            else
            {
                FireFallback(target);
            }
        }

        yield return new WaitForSeconds(0.12f);
        _onComplete?.Invoke();
    }

    IEnumerator FocusCameraOnSelf(float delay)
    {
        CameraController camera = CameraController.Instance;
        if (camera != null)
        {
            camera.FollowTarget(transform, false);
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
    }

    Unit PickTarget()
    {
        var players = GameManager.Instance?.playerUnits;
        if (players == null || players.Count == 0)
        {
            return null;
        }

        Unit best = null;
        float bestScore = float.NegativeInfinity;

        foreach (Unit unit in players)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            float score = (unit.maxHP - unit.CurrentHP) * 2f;
            score += 1f / Mathf.Max(0.35f, Vector2.Distance(transform.position, unit.transform.position));
            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }

        return best;
    }

    ShotSolution FindBestShot(Unit target)
    {
        ShotSolution best = default;
        best.score = float.MaxValue;

        ShotSolution closeRange = TryFindCloseRangeShot(target);
        if (closeRange.isValid)
        {
            best = closeRange;
        }

        foreach (Vector2 origin in EnumerateCandidateOrigins())
        {
            Vector2 launchOrigin = origin + GetLaunchOffset();
            Vector2 targetPoint = GetTargetPoint(target);
            float directionSign = Mathf.Sign(targetPoint.x - launchOrigin.x);
            if (Mathf.Approximately(directionSign, 0f))
            {
                directionSign = transform.localScale.x >= 0f ? 1f : -1f;
            }

            for (float speed = minLaunchSpeed; speed <= maxLaunchSpeed; speed += speedStep)
            {
                for (float angle = minLaunchAngle; angle <= maxLaunchAngle; angle += angleStep)
                {
                    Vector2 velocity = new(Mathf.Cos(angle * Mathf.Deg2Rad) * directionSign * speed, Mathf.Sin(angle * Mathf.Deg2Rad) * speed);
                    ShotSolution candidate = EvaluateShot(origin, launchOrigin, velocity, target);
                    if (!candidate.isValid || candidate.score >= best.score)
                    {
                        continue;
                    }

                    best = candidate;
                }
            }
        }

        return best;
    }

    ShotSolution EvaluateShot(Vector2 destination, Vector2 origin, Vector2 velocity, Unit target)
    {
        ShotSolution result = new()
        {
            isValid = true,
            origin = origin,
            velocity = velocity,
            destination = destination,
            score = 999f
        };

        Vector2 position = origin;
        Vector2 currentVelocity = velocity;
        Vector2 gravity = Physics2D.gravity * GetProjectileGravityScale();
        float drag = GetProjectileDrag();
        Vector2 targetPoint = GetTargetPoint(target);
        float bestDistance = Vector2.Distance(origin, targetPoint);
        float launchAngle = Mathf.Abs(Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        bool obstacleHit = false;
        float obstacleDistance = float.MaxValue;

        if (HitsImmediateObstacle(origin, velocity, target))
        {
            result.isValid = false;
            result.score = float.MaxValue;
            return result;
        }

        for (int i = 0; i < simulationSteps; i++)
        {
            currentVelocity += gravity * simulationTimeStep;
            currentVelocity *= Mathf.Max(0f, 1f - drag * simulationTimeStep);

            Vector2 nextPosition = position + currentVelocity * simulationTimeStep;
            float distanceToTarget = Vector2.Distance(nextPosition, targetPoint);
            bestDistance = Mathf.Min(bestDistance, distanceToTarget);
            if (distanceToTarget <= directHitRadius)
            {
                result.directHit = true;
                result.hitsPlayer = true;
                result.score = 0.04f + distanceToTarget;
                return result;
            }

            RaycastHit2D[] hits = Physics2D.LinecastAll(position, nextPosition, simulationMask);
            SortHitsByDistance(position, hits);

            foreach (RaycastHit2D hit in hits)
            {
                if (ShouldIgnoreHit(hit.collider, position, nextPosition, currentVelocity))
                {
                    continue;
                }

                Unit hitUnit = GetUnitFromCollider(hit.collider);
                if (hitUnit == target)
                {
                    result.directHit = true;
                    result.hitsPlayer = true;
                    result.score = 0.05f + Vector2.Distance(hit.point, targetPoint);
                    return result;
                }

                if (hitUnit != null && hitUnit.isPlayerTeam)
                {
                    result.hitsPlayer = true;
                    result.score = 0.8f + Vector2.Distance(hit.point, targetPoint);
                    return result;
                }

                if (hitUnit != null && !hitUnit.isPlayerTeam)
                {
                    result.friendlyFire = true;
                    result.score = 50f + Vector2.Distance(hit.point, targetPoint);
                    return result;
                }

                float splashRadius = GetProjectileSplashRadius();
                if (splashRadius > 0f)
                {
                    float impactDistance = Vector2.Distance(hit.point, targetPoint);
                    if (impactDistance <= splashRadius * 0.92f)
                    {
                        result.hitsPlayer = true;
                        result.score = 0.28f + impactDistance;
                        return result;
                    }
                }

                obstacleDistance = Vector2.Distance(origin, hit.point);
                obstacleHit = true;
                break;
            }

            if (obstacleHit)
            {
                break;
            }

            position = nextPosition;
        }

        result.score = bestDistance * 8f;
        if (obstacleHit)
        {
            if (obstacleDistance <= minimumClearFlightDistance)
            {
                result.isValid = false;
                result.score = float.MaxValue;
                return result;
            }

            result.score += 6f;
        }

        result.score += Mathf.Abs(origin.x - transform.position.x) * 0.5f;
        result.score += Mathf.Abs(velocity.x) * 0.015f;
        result.score += launchAngle * 0.02f;
        return result;
    }

    void FireSolution(ShotSolution solution)
    {
        float spreadMultiplier = solution.directHit ? 0.2f : (solution.hitsPlayer ? 0.45f : 1f);
        bool usePerfectShot =
            (solution.directHit && Random.value <= _turnDirectPerfectChance) ||
            (solution.hitsPlayer && solution.score <= perfectShotScoreThreshold && Random.value <= _turnPerfectShotChance);

        Vector2 velocity = usePerfectShot
            ? solution.velocity
            : ApplyAccuracy(solution.velocity, spreadMultiplier);
        Vector2 origin = GetLaunchOrigin();
        ProjectileController projectile = _launcher.Launch(origin, velocity, GetActiveProjectilePrefab());
        if (projectile != null && _selectedWeapon != null)
        {
            WeaponInventory.Instance?.CommitShot(_unit, _selectedWeapon);
        }
    }

    void FireFallback(Unit target)
    {
        Vector2 origin = GetLaunchOrigin();
        Vector2 toTarget = (GetTargetPoint(target) - origin).normalized;
        Vector2 fallbackVelocity = new Vector2(toTarget.x, Mathf.Max(0.6f, toTarget.y + 0.45f)).normalized * Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, 0.55f);
        ProjectileController projectile = _launcher.Launch(origin, ApplyAccuracy(fallbackVelocity, 1f), GetActiveProjectilePrefab());
        if (projectile != null && _selectedWeapon != null)
        {
            WeaponInventory.Instance?.CommitShot(_unit, _selectedWeapon);
        }
    }

    IEnumerator MoveTo(Vector2 destination)
    {
        Vector2 start = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, moveDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector2.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.position = destination;
    }

    IEnumerator RecoverFromMapEdgeIfNeeded()
    {
        if (!IsTouchingMapEdge(out Bounds mapBounds))
        {
            yield break;
        }

        if (!TryGetStandingPlatform(out Collider2D supportCollider))
        {
            yield break;
        }

        Bounds supportBounds = supportCollider.bounds;
        float halfWidth = _collider.bounds.extents.x;
        float targetX = Mathf.Clamp(supportBounds.center.x, supportBounds.min.x + halfWidth + 0.04f, supportBounds.max.x - halfWidth - 0.04f);
        float targetY = supportBounds.max.y + _collider.bounds.extents.y + 0.02f;
        Vector2 destination = new(targetX, targetY);

        if (Vector2.Distance(transform.position, destination) <= 0.05f)
        {
            yield break;
        }

        yield return MoveTo(destination);
    }

    Vector2 ApplyAccuracy(Vector2 velocity, float spreadMultiplier)
    {
        float spreadRange = (1f - _turnAccuracy) * maxSpreadDegrees * Mathf.Clamp01(spreadMultiplier);
        float randomSpread = Random.Range(-spreadRange, spreadRange);
        return Rotate(velocity, randomSpread + _turnAimOffsetDegrees + _turnMissBiasDegrees);
    }

    void RollTurnAccuracy()
    {
        float roll = Random.value;
        if (roll < 0.35f)
        {
            SetTurnAccuracy(Mathf.Clamp01(accuracyBands.x));
        }
        else if (roll < 0.65f)
        {
            SetTurnAccuracy(Mathf.Clamp01(accuracyBands.y));
        }
        else if (roll < 0.9f)
        {
            SetTurnAccuracy(Mathf.Clamp01(accuracyBands.z));
        }
        else
        {
            SetTurnAccuracy(Mathf.Clamp01(accuracyBands.w));
        }

        _turnSpecialWeaponChance = RollSpecialWeaponChance();
    }

    void SetTurnAccuracy(float value)
    {
        _turnAccuracy = Mathf.Clamp(value, 0.8f, 1f);

        if (_turnAccuracy >= 0.999f)
        {
            _turnDirectPerfectChance = 0.45f;
            _turnPerfectShotChance = 0.28f;
            _turnAimOffsetDegrees = Random.Range(-0.18f, 0.18f);
            _turnMissBiasDegrees = Random.Range(-0.2f, 0.2f);
        }
        else if (_turnAccuracy >= 0.94f)
        {
            _turnDirectPerfectChance = 0.22f;
            _turnPerfectShotChance = 0.12f;
            _turnAimOffsetDegrees = Random.Range(-0.55f, 0.55f);
            _turnMissBiasDegrees = Random.value < 0.3f ? Random.Range(-1f, 1f) : 0f;
        }
        else if (_turnAccuracy >= 0.84f)
        {
            _turnDirectPerfectChance = 0.08f;
            _turnPerfectShotChance = 0.03f;
            _turnAimOffsetDegrees = Random.Range(-1.2f, 1.2f);
            _turnMissBiasDegrees = Random.value < 0.55f ? Random.Range(-2.4f, 2.4f) : Random.Range(-0.8f, 0.8f);
        }
        else
        {
            _turnDirectPerfectChance = 0f;
            _turnPerfectShotChance = 0f;
            _turnAimOffsetDegrees = Random.Range(-2f, 2f);
            _turnMissBiasDegrees = Random.Range(-4.2f, 4.2f);
        }
    }

    ShotSolution TryFindCloseRangeShot(Unit target)
    {
        Vector2 launchOrigin = GetLaunchOrigin();
        Vector2 targetPoint = GetTargetPoint(target);
        float distanceToTarget = Vector2.Distance(launchOrigin, targetPoint);
        if (distanceToTarget > 4f)
        {
            return default;
        }

        float directionSign = Mathf.Sign(targetPoint.x - launchOrigin.x);
        if (Mathf.Approximately(directionSign, 0f))
        {
            directionSign = 1f;
        }

        ShotSolution best = default;
        best.score = float.MaxValue;

        for (float speed = minLaunchSpeed; speed <= Mathf.Min(maxLaunchSpeed, 13.5f); speed += 0.25f)
        {
            for (float angle = 2f; angle <= 34f; angle += 1f)
            {
                Vector2 velocity = new(Mathf.Cos(angle * Mathf.Deg2Rad) * directionSign * speed, Mathf.Sin(angle * Mathf.Deg2Rad) * speed);
                ShotSolution candidate = EvaluateShot(transform.position, launchOrigin, velocity, target);
                if (!candidate.isValid || candidate.score >= best.score)
                {
                    continue;
                }

                best = candidate;
            }
        }

        return best;
    }

    bool HitsImmediateObstacle(Vector2 origin, Vector2 velocity, Unit target)
    {
        Vector2 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector2.right;
        Vector2 spawnPoint = origin + direction * Mathf.Max(0.12f, _launcher.spawnClearance);
        Vector2 clearancePoint = spawnPoint + direction * immediateObstacleDistance;
        RaycastHit2D[] hits = Physics2D.LinecastAll(spawnPoint, clearancePoint, simulationMask);
        SortHitsByDistance(spawnPoint, hits);

        foreach (RaycastHit2D hit in hits)
        {
            if (ShouldIgnoreHit(hit.collider, spawnPoint, clearancePoint, velocity))
            {
                continue;
            }

            Unit hitUnit = GetUnitFromCollider(hit.collider);
            if (hitUnit == target)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    Vector2 GetLaunchOrigin()
    {
        Unit unit = GetComponent<Unit>();
        return unit != null ? unit.AimTarget : (Vector2)transform.position + Vector2.up * 0.35f;
    }

    Vector2 GetTargetPoint(Unit target)
    {
        if (target == null)
        {
            return Vector2.zero;
        }

        Collider2D targetCollider = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return new Vector2(bounds.center.x, bounds.center.y + bounds.extents.y * 0.1f);
        }

        return target.AimTarget;
    }

    Vector2 GetLaunchOffset()
    {
        return GetLaunchOrigin() - (Vector2)transform.position;
    }

    float GetProjectileGravityScale()
    {
        GameObject activeProjectilePrefab = GetActiveProjectilePrefab();
        if (_projectileBodyTemplate == null && activeProjectilePrefab != null)
        {
            _projectileBodyTemplate = activeProjectilePrefab.GetComponent<Rigidbody2D>();
        }

        return _projectileBodyTemplate != null ? _projectileBodyTemplate.gravityScale : 1f;
    }

    float GetProjectileDrag()
    {
        GameObject activeProjectilePrefab = GetActiveProjectilePrefab();
        if (_projectileBodyTemplate == null && activeProjectilePrefab != null)
        {
            _projectileBodyTemplate = activeProjectilePrefab.GetComponent<Rigidbody2D>();
        }

        return _projectileBodyTemplate != null ? _projectileBodyTemplate.linearDamping : 0f;
    }

    float GetProjectileSplashRadius()
    {
        GameObject activeProjectilePrefab = GetActiveProjectilePrefab();
        if (_launcher == null || activeProjectilePrefab == null)
        {
            return 0f;
        }

        ProjectileController projectile = activeProjectilePrefab.GetComponent<ProjectileController>();
        return projectile != null ? Mathf.Max(0f, projectile.splashRadius) : 0f;
    }

    GameObject GetActiveProjectilePrefab()
    {
        return _selectedProjectilePrefab != null ? _selectedProjectilePrefab : (_launcher != null ? _launcher.projectilePrefab : null);
    }

    void SelectWeaponForTurn(Unit target)
    {
        _selectedWeapon = null;
        _selectedProjectilePrefab = _launcher != null ? _launcher.projectilePrefab : null;
        _projectileBodyTemplate = null;

        WeaponInventory inventory = WeaponInventory.Instance;
        IReadOnlyList<WeaponInventory.WeaponEntry> unitWeapons = inventory != null
            ? inventory.GetWeaponsForUnit(_unit)
            : null;
        if (unitWeapons == null || unitWeapons.Count == 0)
        {
            return;
        }

        float distance = target != null ? Vector2.Distance(transform.position, target.transform.position) : 0f;
        WeaponInventory.WeaponEntry cannonball = null;
        List<WeaponInventory.WeaponEntry> specialCandidates = new();
        List<float> specialWeights = new();
        List<WeaponInventory.WeaponEntry> allCandidates = new();
        List<float> allWeights = new();

        foreach (WeaponInventory.WeaponEntry weapon in unitWeapons)
        {
            if (weapon == null || weapon.projectilePrefab == null)
            {
                continue;
            }

            string name = weapon.displayName != null ? weapon.displayName.ToLowerInvariant() : string.Empty;
            if (name.Contains("cannonball"))
            {
                cannonball = weapon;
            }

            float weight = GetWeaponWeight(weapon, distance);
            if (weight <= 0f)
            {
                continue;
            }

            allCandidates.Add(weapon);
            allWeights.Add(weight);

            if (!name.Contains("cannonball"))
            {
                specialCandidates.Add(weapon);
                specialWeights.Add(weight);
            }
        }

        if (allCandidates.Count == 0)
        {
            return;
        }

        bool canUseCannonball = cannonball != null && cannonball.projectilePrefab != null && cannonball.ammo != 0;
        bool useSpecial = specialCandidates.Count > 0 && Random.value < _turnSpecialWeaponChance;

        if (!useSpecial && canUseCannonball)
        {
            _selectedWeapon = cannonball;
            _selectedProjectilePrefab = _selectedWeapon.projectilePrefab;
            return;
        }

        if (useSpecial && specialCandidates.Count > 0)
        {
            int selectedSpecialIndex = SelectWeightedIndex(specialWeights);
            _selectedWeapon = specialCandidates[selectedSpecialIndex];
            _selectedProjectilePrefab = _selectedWeapon.projectilePrefab;
            return;
        }

        int selectedIndex = SelectWeightedIndex(allWeights);
        _selectedWeapon = allCandidates[selectedIndex];
        _selectedProjectilePrefab = _selectedWeapon.projectilePrefab;
    }

    float GetWeaponWeight(WeaponInventory.WeaponEntry weapon, float distance)
    {
        string name = weapon.displayName != null ? weapon.displayName.ToLowerInvariant() : string.Empty;
        if (name.Contains("nuke") || name.Contains("heavy"))
        {
            return distance <= 4.6f ? 1.35f : 0.45f;
        }

        if (name.Contains("cluster"))
        {
            return distance >= 3.2f ? 0.55f : 0.2f;
        }

        if (name.Contains("roller") || name.Contains("slider"))
        {
            return distance <= 4.4f ? 0.4f : 0.1f;
        }

        return 1.8f;
    }

    float RollSpecialWeaponChance()
    {
        float roll = Random.value;
        if (roll < 0.45f)
        {
            return Mathf.Clamp01(specialWeaponChanceBands.x);
        }

        if (roll < 0.75f)
        {
            return Mathf.Clamp01(specialWeaponChanceBands.y);
        }

        if (roll < 0.92f)
        {
            return Mathf.Clamp01(specialWeaponChanceBands.z);
        }

        return Mathf.Clamp01(specialWeaponChanceBands.w);
    }

    static int SelectWeightedIndex(List<float> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i]);
        }

        if (total <= 0f)
        {
            return 0;
        }

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative)
            {
                return i;
            }
        }

        return Mathf.Max(0, weights.Count - 1);
    }

    IEnumerable<Vector2> EnumerateCandidateOrigins()
    {
        yield return (Vector2)transform.position;

        if (!allowShortReposition)
        {
            yield break;
        }

        for (float offset = repositionStep; offset <= repositionRange; offset += repositionStep)
        {
            if (TryFindStandPosition(transform.position.x - offset, out Vector2 left))
            {
                yield return left;
            }

            if (TryFindStandPosition(transform.position.x + offset, out Vector2 right))
            {
                yield return right;
            }
        }
    }

    bool TryFindStandPosition(float targetX, out Vector2 groundedPosition)
    {
        groundedPosition = default;

        float castStartY = transform.position.y + 1.25f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(targetX, castStartY), Vector2.down, 3f, simulationMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (ShouldIgnoreGroundHit(hit.collider))
            {
                continue;
            }

            float standingY = hit.point.y + _collider.bounds.extents.y + 0.02f;
            if (Mathf.Abs(standingY - transform.position.y) > maxVerticalRepositionDelta)
            {
                continue;
            }

            Vector2 testPosition = new(targetX, standingY);
            if (IsSpaceBlocked(testPosition))
            {
                continue;
            }

            groundedPosition = testPosition;
            return true;
        }

        return false;
    }

    bool TryGetStandingPlatform(out Collider2D supportCollider)
    {
        supportCollider = null;

        Bounds bounds = _collider.bounds;
        Vector2 probeCenter = new(bounds.center.x, bounds.min.y - 0.04f);
        Vector2 probeSize = new(Mathf.Max(0.2f, bounds.size.x * 0.55f), 0.24f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f, simulationMask);
        float bestTopDistance = float.MaxValue;
        foreach (Collider2D collider in hits)
        {
            if (collider == null || collider == _collider || collider.isTrigger)
            {
                continue;
            }

            if (GetUnitFromCollider(collider) != null)
            {
                continue;
            }

            if (collider.GetComponent<MapBoundaryFrame>() != null ||
                collider.GetComponentInParent<MapBoundaryFrame>() != null)
            {
                continue;
            }

            float topDistance = Mathf.Abs(collider.bounds.max.y - bounds.min.y);
            if (topDistance > 0.22f || topDistance >= bestTopDistance)
            {
                continue;
            }

            bestTopDistance = topDistance;
            supportCollider = collider;
        }

        return supportCollider != null;
    }

    bool IsTouchingMapEdge(out Bounds mapBounds)
    {
        mapBounds = default;

        GameObject levelVisual = GameObject.Find("LevelVisual");
        if (levelVisual == null || !levelVisual.TryGetComponent(out SpriteRenderer levelRenderer) || levelRenderer.sprite == null)
        {
            return false;
        }

        mapBounds = levelRenderer.bounds;
        Bounds unitBounds = _collider.bounds;
        return unitBounds.min.x <= mapBounds.min.x + edgeRecoveryMargin ||
               unitBounds.max.x >= mapBounds.max.x - edgeRecoveryMargin;
    }

    bool IsSpaceBlocked(Vector2 position)
    {
        Bounds bounds = _collider.bounds;
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(position, bounds.size * 0.92f, 0f, simulationMask);
        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == null || overlap == _collider || overlap.isTrigger)
            {
                continue;
            }

            if (GetUnitFromCollider(overlap) != null)
            {
                return true;
            }

            if (!ShouldIgnoreGroundHit(overlap))
            {
                return true;
            }
        }

        return false;
    }

    bool ShouldIgnoreHit(Collider2D collider, Vector2 from, Vector2 to, Vector2 velocity)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        Unit hitUnit = GetUnitFromCollider(collider);
        if (hitUnit != null && hitUnit.gameObject == gameObject)
        {
            return true;
        }

        if (ShouldPassThroughOneWayPlatform(collider, from, to, velocity))
        {
            return true;
        }

        return ShouldBypassPlatform(collider);
    }

    bool ShouldIgnoreGroundHit(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (collider.GetComponent<MapBoundaryFrame>() != null ||
            collider.GetComponentInParent<MapBoundaryFrame>() != null)
        {
            return true;
        }

        return GetUnitFromCollider(collider) != null;
    }

    bool ShouldBypassPlatform(Collider2D collider)
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
        return !HasUnitOccupyingPlatform(collider) && !CoverPlatformRules.ShouldActAsCover(collider);
    }

        Bounds bounds = collider.bounds;
        if (bounds.size.y > 0.8f || bounds.size.x < bounds.size.y * 1.8f)
        {
            return false;
        }

        string objectName = collider.gameObject.name.ToLowerInvariant();
        string parentName = collider.transform.parent != null ? collider.transform.parent.name.ToLowerInvariant() : string.Empty;
        return (objectName.Contains("float") || parentName.Contains("float")) && !HasUnitOccupyingPlatform(collider);
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

    static bool HasUnitOccupyingPlatform(Collider2D collider)
    {
        Bounds bounds = collider.bounds;
        Vector2 probeCenter = new(bounds.center.x, bounds.max.y + 0.325f);
        Vector2 probeSize = new(bounds.size.x * 0.92f, 0.65f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);
        foreach (Collider2D hit in hits)
        {
            Unit unit = GetUnitFromCollider(hit);
            if (unit != null && !unit.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    static Unit GetUnitFromCollider(Collider2D collider)
    {
        return collider == null
            ? null
            : collider.GetComponent<Unit>() ?? collider.GetComponentInParent<Unit>() ?? collider.attachedRigidbody?.GetComponent<Unit>();
    }

    static void SortHitsByDistance(Vector2 origin, RaycastHit2D[] hits)
    {
        System.Array.Sort(hits, (a, b) => Vector2.Distance(origin, a.point).CompareTo(Vector2.Distance(origin, b.point)));
    }

    static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            cos * vector.x - sin * vector.y,
            sin * vector.x + cos * vector.y
        );
    }

    void SyncLegacyLaunchSpeed()
    {
        launchSpeed = Mathf.Max(8f, launchSpeed);
        accuracy = Mathf.Clamp(accuracy, 0.9f, 1f);
        perfectShotScoreThreshold = Mathf.Clamp(perfectShotScoreThreshold, 0.05f, 2f);
        accuracyBands.x = Mathf.Clamp(accuracyBands.x, 0.8f, 1f);
        accuracyBands.y = Mathf.Clamp(accuracyBands.y, 0.8f, 1f);
        accuracyBands.z = Mathf.Clamp(accuracyBands.z, 0.8f, 1f);
        accuracyBands.w = Mathf.Clamp(accuracyBands.w, 0.8f, 1f);
        maxSpreadDegrees = Mathf.Clamp(maxSpreadDegrees, 0.4f, 3.25f);
        edgeRecoveryMargin = Mathf.Clamp(edgeRecoveryMargin, 0.05f, 0.5f);
        immediateObstacleDistance = Mathf.Clamp(immediateObstacleDistance, 0.5f, 1.6f);
        minimumClearFlightDistance = Mathf.Clamp(minimumClearFlightDistance, 1f, 3.5f);
        minLaunchAngle = Mathf.Clamp(minLaunchAngle, 2f, 20f);
        maxLaunchAngle = Mathf.Clamp(maxLaunchAngle, 30f, 75f);

        if (minLaunchSpeed > maxLaunchSpeed)
        {
            (minLaunchSpeed, maxLaunchSpeed) = (maxLaunchSpeed, minLaunchSpeed);
        }

        if (launchSpeed < minLaunchSpeed || launchSpeed > maxLaunchSpeed)
        {
            float halfRange = Mathf.Max(1.2f, speedStep * 6f);
            minLaunchSpeed = Mathf.Min(minLaunchSpeed, launchSpeed - halfRange);
            maxLaunchSpeed = Mathf.Max(maxLaunchSpeed, launchSpeed + halfRange);
        }
    }
}


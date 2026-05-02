using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileController : MonoBehaviour
{
    public enum ProjectileBehavior
    {
        Standard,
        ClusterBomb,
        Roller
    }

    [Header("Damage")]
    public int directDamage = 2;
    public int splashDamage = 1;
    public float splashRadius = 1f;
    public AnimationCurve splashFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Settings")]
    public float lifetime = 8f;
    public bool isPlayerProjectile = true;
    public LayerMask hitLayers = ~0;
    public float armingTime = 0.05f;
    public float minImpactSpeed = 0.5f;
    public bool bypassUnusedPlatforms = true;
    public float platformOccupancyHeight = 0.65f;
    public float bypassPlatformMaxThickness = 0.8f;

    [Header("Behavior")]
    public ProjectileBehavior behavior = ProjectileBehavior.Standard;
    public int maxBounces = 0;
    public float bounceVelocityMultiplier = 0.72f;

    [Header("Cluster")]
    public GameObject clusterChildPrefab;
    public int clusterChildCount = 5;
    public float clusterActivationDelay = 0.35f;
    public float clusterEarlySpreadAngle = 34f;
    public float clusterLateSpreadAngle = 14f;
    public float clusterLaunchSpeed = 2.8f;
    public float clusterVelocityInheritance = 0.72f;
    public bool allowManualClusterSplit = true;
    public bool splitOnImpact = true;
    public int clusterChildDirectDamageOverride = -1;
    public int clusterChildSplashDamageOverride = -1;

    [Header("Impact Shrapnel")]
    public GameObject impactShrapnelPrefab;
    public int impactShrapnelCount = 0;
    public float impactShrapnelSpeed = 3.6f;
    public float impactShrapnelSpreadAngle = 300f;
    public float impactShrapnelVelocityInheritance = 0.15f;
    public int impactShrapnelDirectDamageOverride = -1;
    public int impactShrapnelSplashDamageOverride = -1;

    [Header("VFX")]
    public GameObject explosionPrefab;

    [Header("Audio")]
    public AudioClip hitSound;

    Rigidbody2D _rigidbody;
    Collider2D _collider;
    int _remainingBounces;
    bool _hasResolved;
    bool _registeredActive;
    bool _releaseCameraOnDestroy = true;
    float _spawnedAt;
    readonly Collider2D[] _contactBuffer = new Collider2D[8];

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _remainingBounces = maxBounces;
        _spawnedAt = Time.time;

        if (_rigidbody != null && behavior != ProjectileBehavior.Roller)
        {
            _rigidbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public void Launch(Vector2 velocity)
    {
        _spawnedAt = Time.time;
        if (_rigidbody != null && behavior != ProjectileBehavior.Roller)
        {
            _rigidbody.angularVelocity = 0f;
        }
        _rigidbody.linearVelocity = velocity;
        RegisterAsActiveProjectile();
    }

    void Update()
    {
        if (_hasResolved)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager != null && !manager.IsInsideProjectileBounds(transform.position))
        {
            ExitMapBounds();
            return;
        }

        if (Time.time - _spawnedAt >= lifetime)
        {
            HandleLifetimeExpiry();
            return;
        }

        if (TryResolvePersistentContact())
        {
            return;
        }

        if (_rigidbody.linearVelocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(_rigidbody.linearVelocity.y, _rigidbody.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (behavior == ProjectileBehavior.ClusterBomb && CanSplitCluster())
        {
            if (!allowManualClusterSplit || ReadClusterSplitPressed())
            {
                SplitIntoCluster(transform.position, _rigidbody.linearVelocity);
                return;
            }

            SplitIntoCluster(transform.position, _rigidbody.linearVelocity);
            return;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!CanProcessImpact())
        {
            return;
        }

        if (ShouldIgnoreProjectileCollision(collision.collider))
        {
            return;
        }

        if (IsMapBoundaryFrame(collision.collider))
        {
            Physics2D.IgnoreCollision(_collider, collision.collider, true);
            return;
        }

        if (ShouldPassThroughOneWayPlatform(collision.collider))
        {
            StartCoroutine(TemporarilyIgnoreCollider(collision.collider, 0.18f));
            return;
        }

        if (ShouldBypassPlatform(collision.collider))
        {
            Physics2D.IgnoreCollision(_collider, collision.collider, true);
            return;
        }

        Vector2 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        Unit directHit = GetUnitFromCollider(collision.collider);

        if (behavior == ProjectileBehavior.Roller && directHit == null && _remainingBounces > 0 && _rigidbody.linearVelocity.magnitude >= minImpactSpeed)
        {
            BounceFromSurface(collision);
            return;
        }

        Explode(hitPoint, directHit);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanProcessImpact() || other == _collider)
        {
            return;
        }

        if (ShouldIgnoreProjectileCollision(other))
        {
            return;
        }

        if (IsMapBoundaryFrame(other))
        {
            Physics2D.IgnoreCollision(_collider, other, true);
            return;
        }

        if (ShouldPassThroughOneWayPlatform(other))
        {
            StartCoroutine(TemporarilyIgnoreCollider(other, 0.18f));
            return;
        }

        if (ShouldBypassPlatform(other))
        {
            Physics2D.IgnoreCollision(_collider, other, true);
            return;
        }

        Unit directHit = GetUnitFromCollider(other);
        Vector2 hitPoint = other.ClosestPoint(transform.position);

        Explode(hitPoint, directHit);
    }

    void OnDestroy()
    {
        if (_registeredActive)
        {
            _registeredActive = false;
            GameManager.Instance?.NotifyProjectileResolved(this);
        }

        if (_releaseCameraOnDestroy)
        {
            CameraController camera = CameraController.Instance;
            if (camera != null)
            {
                camera.ReleaseFollowLock();
            }
        }
    }

    void Explode(Vector2 position, Unit directHit)
    {
        if (_hasResolved)
        {
            return;
        }

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            explosion.SetActive(true);
        }

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, position);
        }
        else
        {
            GalacticSlimefallAudio.PlayProjectileImpact(this, position);
        }

        CameraController camera = CameraController.Instance;
        if (camera != null)
        {
            camera.Shake(0.18f, 0.24f);
        }
        ApplyDamage(position, directHit);
        SpawnImpactShrapnel(position, _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero);
        if (camera != null && (GameManager.Instance == null || !GameManager.Instance.IsHitPauseActive))
        {
            camera.ReleaseFollowLock();
        }
        _releaseCameraOnDestroy = false;
        ResolveProjectile();
    }

    void ApplyDamage(Vector2 center, Unit directHit)
    {
        var hitUnits = new HashSet<Unit>();
        Unit primaryHit = null;
        int primaryDamage = 0;

        if (directHit != null)
        {
            int directAmount = ScaleDamage(directDamage, false);
            if (TryDamageUnit(directHit, directAmount, center, hitUnits))
            {
                primaryHit = directHit;
                primaryDamage = directAmount;
            }
        }

        Collider2D[] nearby = Physics2D.OverlapCircleAll(center, splashRadius, hitLayers);
        foreach (Collider2D collider in nearby)
        {
            Unit unit = GetUnitFromCollider(collider);
            if (unit == null || hitUnits.Contains(unit))
            {
                continue;
            }

            int splashAmount = EvaluateSplashDamage(collider, center);
            if (splashAmount <= 0)
            {
                continue;
            }

            if (TryDamageUnit(unit, splashAmount, center, hitUnits) && primaryHit == null)
            {
                primaryHit = unit;
                primaryDamage = splashAmount;
            }
        }

        if (primaryHit != null)
        {
            GameManager.Instance?.NotifyProjectileHit(primaryHit, primaryDamage);
        }
    }

    bool TryDamageUnit(Unit unit, int damage, Vector2 center, HashSet<Unit> hitUnits)
    {
        if (unit == null || hitUnits.Contains(unit) || damage <= 0)
        {
            return false;
        }

        hitUnits.Add(unit);

        Vector2 knockDirection = ((Vector2)unit.transform.position - center).normalized;
        if (knockDirection == Vector2.zero)
        {
            knockDirection = Vector2.up;
        }

        unit.TakeDamage(damage, knockDirection, false);
        return true;
    }

    bool TryResolvePersistentContact()
    {
        if (_collider == null || behavior == ProjectileBehavior.Roller || !CanProcessImpact())
        {
            return false;
        }

        int contactCount = _collider.GetContacts(_contactBuffer);
        for (int i = 0; i < contactCount; i++)
        {
            Collider2D contact = _contactBuffer[i];
            if (contact == null || contact == _collider)
            {
                continue;
            }

            if (ShouldIgnoreProjectileCollision(contact))
            {
                continue;
            }

            if (IsMapBoundaryFrame(contact) || ShouldPassThroughOneWayPlatform(contact) || ShouldBypassPlatform(contact))
            {
                continue;
            }

            Vector2 hitPoint = contact.ClosestPoint(transform.position);
            Unit directHit = GetUnitFromCollider(contact);
            Explode(hitPoint, directHit);
            return true;
        }

        return false;
    }

    bool ShouldIgnoreProjectileCollision(Collider2D other)
    {
        if (_collider == null || other == null || other == _collider)
        {
            return false;
        }

        if (!IsProjectileCollider(other))
        {
            return false;
        }

        Physics2D.IgnoreCollision(_collider, other, true);
        return true;
    }

    int EvaluateSplashDamage(Collider2D collider, Vector2 center)
    {
        if (collider == null || splashDamage <= 0 || splashRadius <= 0f)
        {
            return 0;
        }

        Vector2 closestPoint = collider.ClosestPoint(center);
        float distance = Vector2.Distance(center, closestPoint);
        if (distance > splashRadius)
        {
            return 0;
        }

        float normalizedDistance = Mathf.Clamp01(distance / splashRadius);
        float damageFactor = splashFalloff.Evaluate(normalizedDistance);
        return ScaleDamage(Mathf.RoundToInt(splashDamage * damageFactor), true);
    }

    int ScaleDamage(int baseDamage, bool allowZero)
    {
        float scale = GameManager.Instance != null ? GameManager.Instance.projectileDamageScale : 1f;
        int scaledDamage = Mathf.RoundToInt(baseDamage * scale);
        if (!allowZero && baseDamage > 0)
        {
            return Mathf.Max(1, scaledDamage);
        }

        return Mathf.Max(0, scaledDamage);
    }

    void BounceFromSurface(Collision2D collision)
    {
        _remainingBounces = Mathf.Max(0, _remainingBounces - 1);

        ContactPoint2D contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        Vector2 normal = contact.normal == Vector2.zero ? Vector2.up : contact.normal;
        Vector2 reflectedVelocity = Vector2.Reflect(_rigidbody.linearVelocity, normal) * bounceVelocityMultiplier;

        if (contact.point != Vector2.zero)
        {
            transform.position = contact.point + normal * 0.06f;
        }

        _rigidbody.linearVelocity = reflectedVelocity;
    }

    void HandleLifetimeExpiry()
    {
        if (behavior == ProjectileBehavior.ClusterBomb && clusterChildPrefab != null)
        {
            SplitIntoCluster(transform.position, _rigidbody.linearVelocity);
            return;
        }

        ExitMapBounds();
    }

    void ExitMapBounds()
    {
        if (_hasResolved)
        {
            return;
        }

        CameraController camera = CameraController.Instance;
        if (camera != null)
        {
            camera.ReleaseFollowLock(0f);
        }
        _releaseCameraOnDestroy = false;
        ResolveProjectile();
    }

    void SplitIntoCluster(Vector2 origin, Vector2 baseVelocity)
    {
        if (_hasResolved || clusterChildPrefab == null || clusterChildCount <= 0)
        {
            ExitMapBounds();
            return;
        }

        ProjectileController firstChild = null;
        float spreadAngle = GetCurrentClusterSpreadAngle();
        Vector2 baseDirection = baseVelocity.sqrMagnitude > 0.001f ? baseVelocity.normalized : Vector2.up;

        for (int i = 0; i < clusterChildCount; i++)
        {
            float t = clusterChildCount == 1 ? 0.5f : i / (clusterChildCount - 1f);
            float angle = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
            Vector2 spawnDirection = Rotate(baseDirection, angle);
            Vector2 childVelocity = baseVelocity * clusterVelocityInheritance + spawnDirection * clusterLaunchSpeed;

            GameObject childObject = Instantiate(clusterChildPrefab, origin + spawnDirection * 0.12f, Quaternion.identity);
            ProjectileController childProjectile = childObject.GetComponent<ProjectileController>();
            if (childProjectile == null)
            {
                continue;
            }

            childProjectile.isPlayerProjectile = isPlayerProjectile;
            childProjectile.ApplyDamageOverrides(clusterChildDirectDamageOverride, clusterChildSplashDamageOverride);
            childProjectile.Launch(childVelocity);

            if (firstChild == null)
            {
                firstChild = childProjectile;
            }
        }

        if (firstChild != null)
        {
            CameraController camera = CameraController.Instance;
            if (camera != null)
            {
                camera.FollowTarget(firstChild.transform, true);
            }
            _releaseCameraOnDestroy = false;
        }
        else
        {
            CameraController camera = CameraController.Instance;
            if (camera != null)
            {
                camera.ReleaseFollowLock();
            }
            _releaseCameraOnDestroy = false;
        }

        ResolveProjectile();
    }

    void SpawnImpactShrapnel(Vector2 origin, Vector2 baseVelocity)
    {
        if (impactShrapnelPrefab == null || impactShrapnelCount <= 0)
        {
            return;
        }

        Vector2 baseDirection = baseVelocity.sqrMagnitude > 0.001f ? baseVelocity.normalized : Vector2.up;
        float spread = Mathf.Clamp(impactShrapnelSpreadAngle, 1f, 360f);
        float angleOffset = Random.Range(0f, 360f);

        for (int i = 0; i < impactShrapnelCount; i++)
        {
            float t = impactShrapnelCount == 1 ? 0.5f : i / (impactShrapnelCount - 1f);
            float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t) + angleOffset;
            Vector2 direction = Rotate(baseDirection, angle);
            Vector2 shardVelocity = baseVelocity * impactShrapnelVelocityInheritance + direction * impactShrapnelSpeed;

            GameObject shardObject = Instantiate(impactShrapnelPrefab, origin + direction * 0.08f, Quaternion.identity);
            ProjectileController shardProjectile = shardObject.GetComponent<ProjectileController>();
            if (shardProjectile == null)
            {
                continue;
            }

            shardProjectile.isPlayerProjectile = isPlayerProjectile;
            shardProjectile.ApplyDamageOverrides(impactShrapnelDirectDamageOverride, impactShrapnelSplashDamageOverride);
            shardProjectile.Launch(shardVelocity);
        }
    }

    void ApplyDamageOverrides(int directOverride, int splashOverride)
    {
        if (directOverride >= 0)
        {
            directDamage = directOverride;
        }

        if (splashOverride >= 0)
        {
            splashDamage = splashOverride;
        }
    }

    float GetCurrentClusterSpreadAngle()
    {
        float armDuration = Mathf.Max(0.01f, lifetime - clusterActivationDelay);
        float normalizedAge = Mathf.Clamp01((Time.time - _spawnedAt - clusterActivationDelay) / armDuration);
        return Mathf.Lerp(clusterEarlySpreadAngle, clusterLateSpreadAngle, normalizedAge);
    }

    bool CanSplitCluster()
    {
        return clusterChildPrefab != null && Time.time - _spawnedAt >= clusterActivationDelay;
    }

    bool CanProcessImpact()
    {
        return !_hasResolved && Time.time - _spawnedAt >= armingTime;
    }

    bool ShouldBypassPlatform(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (IsMapBoundaryFrame(collider))
        {
            return true;
        }

        if (!bypassUnusedPlatforms)
        {
            return false;
        }

        if (collider.GetComponent<MovementOnlyBlocker>() != null ||
            collider.GetComponentInParent<MovementOnlyBlocker>() != null)
        {
            return true;
        }

        if (!IsBypassCandidate(collider))
        {
            return false;
        }

        return !HasUnitOccupyingPlatform(collider) && !CoverPlatformRules.ShouldActAsCover(collider);
    }

    static bool IsMapBoundaryFrame(Collider2D collider)
    {
        return collider != null &&
               (collider.GetComponent<MapBoundaryFrame>() != null ||
                collider.GetComponentInParent<MapBoundaryFrame>() != null);
    }

    bool ShouldPassThroughOneWayPlatform(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>() ?? collider.GetComponentInParent<PlatformEffector2D>();
        if (effector == null && !collider.usedByEffector)
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
        Vector2 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
        Bounds projectileBounds = _collider != null ? _collider.bounds : new Bounds(transform.position, Vector3.zero);
        bool alreadyOnTopSurface = projectileBounds.min.y >= platformTop - tolerance;
        bool clearlyBelowTopSurface = projectileBounds.max.y <= platformTop - tolerance;
        bool risingFromBelow = velocity.y > 0.05f && clearlyBelowTopSurface;

        if (alreadyOnTopSurface)
        {
            return false;
        }

        return risingFromBelow;
    }

    bool IsBypassCandidate(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
        {
            return false;
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
        if (bounds.size.y > bypassPlatformMaxThickness || bounds.size.x < bounds.size.y * 1.8f)
        {
            return false;
        }

        string objectName = collider.gameObject.name.ToLowerInvariant();
        string parentName = collider.transform.parent != null ? collider.transform.parent.name.ToLowerInvariant() : string.Empty;
        return objectName.Contains("float") || parentName.Contains("float");
    }

    bool HasUnitOccupyingPlatform(Collider2D collider)
    {
        Bounds bounds = collider.bounds;
        Vector2 probeCenter = new Vector2(bounds.center.x, bounds.max.y + platformOccupancyHeight * 0.5f);
        Vector2 probeSize = new Vector2(bounds.size.x * 0.92f, platformOccupancyHeight);
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

    void RegisterAsActiveProjectile()
    {
        if (_registeredActive)
        {
            return;
        }

        _registeredActive = true;
        GameManager.Instance?.NotifyProjectileSpawned(this);
    }

    void ResolveProjectile()
    {
        if (_hasResolved)
        {
            return;
        }

        _hasResolved = true;
        if (_registeredActive)
        {
            _registeredActive = false;
            GameManager.Instance?.NotifyProjectileResolved(this);
        }

        Destroy(gameObject);
    }

    IEnumerator TemporarilyIgnoreCollider(Collider2D target, float duration)
    {
        if (_collider == null || target == null)
        {
            yield break;
        }

        Physics2D.IgnoreCollision(_collider, target, true);
        yield return new WaitForSeconds(duration);

        if (_collider != null && target != null)
        {
            Physics2D.IgnoreCollision(_collider, target, false);
        }
    }

    static Unit GetUnitFromCollider(Collider2D collider)
    {
        return collider == null
            ? null
            : collider.GetComponent<Unit>() ?? collider.GetComponentInParent<Unit>() ?? collider.attachedRigidbody?.GetComponent<Unit>();
    }

    static bool IsProjectileCollider(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        return collider.GetComponent<ProjectileController>() != null ||
               collider.GetComponentInParent<ProjectileController>() != null ||
               collider.attachedRigidbody?.GetComponent<ProjectileController>() != null;
    }

    static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            cos * vector.x - sin * vector.y,
            sin * vector.x + cos * vector.y
        ).normalized;
    }

    static bool ReadClusterSplitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1);
#endif
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}





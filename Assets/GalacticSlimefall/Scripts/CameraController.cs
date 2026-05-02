using System.Collections;
using UnityEngine;
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Follow")]
    public float followSmoothTime = 0.22f;
    public Vector3 offset = new(0f, 1f, -10f);
    public Vector3 projectileOffset = new(0f, 0.15f, -10f);
    public float releaseDelay = 0.1f;

    [Header("Zoom")]
    public float defaultOrthoSize = 5.6f;
    public float projectileOrthoSize = 2.3f;
    public float zoomSmoothTime = 0.18f;

    [Header("Bounds")]
    public bool clampToLevelVisual = true;
    public SpriteRenderer cameraBoundsSprite;
    public Vector2 boundsPadding = new(0.05f, 0.05f);

    Transform _followTarget;
    float _shakeMagnitude;
    float _shakeDuration;
    float _shakeTimer;
    bool _subscribed;
    bool _followLocked;
    Camera _camera;
    Vector3 _followVelocity;
    float _zoomVelocity;
    Coroutine _releaseRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _camera = GetComponent<Camera>();
        if (_camera != null)
        {
            defaultOrthoSize = _camera.orthographicSize;
        }
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
        ResolveBoundsSprite();
    }

    void OnDisable()
    {
        if (!_subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnPlayerTurnStart -= ReturnToDefault;
        GameManager.Instance.OnEnemyTurnStart -= ReturnToDefault;
        _subscribed = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void LateUpdate()
    {
        if (_followTarget == null)
        {
            if (_followLocked)
            {
                _followLocked = false;
                _followVelocity = Vector3.zero;
                ReturnToDefault(true);
            }

            return;
        }

        float targetSize = _camera != null
            ? Mathf.SmoothDamp(_camera.orthographicSize, _followLocked ? projectileOrthoSize : defaultOrthoSize, ref _zoomVelocity, Mathf.Max(0.01f, zoomSmoothTime))
            : (_followLocked ? projectileOrthoSize : defaultOrthoSize);
        targetSize = ClampOrthoSizeToBounds(targetSize);

        Vector3 activeOffset = _followLocked ? projectileOffset : offset;
        Vector3 targetPosition = _followTarget.position + activeOffset;
        targetPosition.z = transform.position.z;
        Vector3 position = Vector3.SmoothDamp(transform.position, targetPosition, ref _followVelocity, Mathf.Max(0.01f, followSmoothTime));

        if (_shakeTimer < _shakeDuration)
        {
            _shakeTimer += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_shakeTimer / Mathf.Max(0.0001f, _shakeDuration));
            position += (Vector3)Random.insideUnitCircle * (_shakeMagnitude * t);
        }

        position = ClampPositionToBounds(position, targetSize);

        transform.position = position;

        if (_camera != null)
        {
            _camera.orthographicSize = targetSize;
        }
    }

    public void FollowTarget(Transform target, bool lockFollow = false)
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
            _releaseRoutine = null;
        }

        _followTarget = target;
        _followLocked = lockFollow;
    }

    public void ReturnToDefault()
    {
        ReturnToDefault(false);
    }

    public void ReturnToDefault(bool force)
    {
        if (_followLocked && !force)
        {
            return;
        }

        _followLocked = false;
        _followTarget = TurnManager.Instance?.GetActiveUnitForCurrentTurn()?.transform;
    }

    public void ReleaseFollowLock()
    {
        ReleaseFollowLock(releaseDelay);
    }

    public void ReleaseFollowLock(float delay)
    {
        if (_releaseRoutine != null)
        {
            StopCoroutine(_releaseRoutine);
        }

        _releaseRoutine = StartCoroutine(ReleaseRoutine(delay));
    }

    IEnumerator ReleaseRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        _followLocked = false;
        _followTarget = TurnManager.Instance?.GetActiveUnitForCurrentTurn()?.transform;
        _releaseRoutine = null;
    }

    public void Shake(float magnitude, float duration)
    {
        _shakeMagnitude = magnitude;
        _shakeDuration = duration;
        _shakeTimer = 0f;
    }

    void TrySubscribe()
    {
        if (_subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnPlayerTurnStart += ReturnToDefault;
        GameManager.Instance.OnEnemyTurnStart += ReturnToDefault;
        _subscribed = true;
    }

    Vector3 ClampPositionToBounds(Vector3 position, float orthographicSize)
    {
        if (!clampToLevelVisual || _camera == null)
        {
            return position;
        }

        ResolveBoundsSprite();
        if (cameraBoundsSprite == null)
        {
            return position;
        }

        Bounds bounds = cameraBoundsSprite.bounds;
        float verticalExtent = orthographicSize;
        float horizontalExtent = orthographicSize * _camera.aspect;

        float minX = bounds.min.x + horizontalExtent + boundsPadding.x;
        float maxX = bounds.max.x - horizontalExtent - boundsPadding.x;
        float minY = bounds.min.y + verticalExtent + boundsPadding.y;
        float maxY = bounds.max.y - verticalExtent - boundsPadding.y;

        position.x = minX > maxX ? bounds.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? bounds.center.y : Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    float ClampOrthoSizeToBounds(float orthographicSize)
    {
        if (!clampToLevelVisual || _camera == null)
        {
            return orthographicSize;
        }

        ResolveBoundsSprite();
        if (cameraBoundsSprite == null)
        {
            return orthographicSize;
        }

        Bounds bounds = cameraBoundsSprite.bounds;
        float maxHeight = Mathf.Max(0.5f, bounds.size.y * 0.5f - boundsPadding.y);
        float maxWidthAsHeight = Mathf.Max(0.5f, (bounds.size.x * 0.5f - boundsPadding.x) / Mathf.Max(0.01f, _camera.aspect));
        float maxAllowed = Mathf.Min(maxHeight, maxWidthAsHeight);
        return Mathf.Min(orthographicSize, maxAllowed);
    }

    void ResolveBoundsSprite()
    {
        if (cameraBoundsSprite != null)
        {
            return;
        }

        GameObject levelVisual = GameObject.Find("LevelVisual");
        if (levelVisual != null && levelVisual.TryGetComponent(out SpriteRenderer levelRenderer))
        {
            cameraBoundsSprite = levelRenderer;
            return;
        }

        float largestArea = 0f;
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (renderer == null || renderer.GetComponent<Unit>() != null || renderer.GetComponentInParent<Unit>() != null)
            {
                continue;
            }

            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (area <= largestArea)
            {
                continue;
            }

            largestArea = area;
            cameraBoundsSprite = renderer;
        }
    }
}


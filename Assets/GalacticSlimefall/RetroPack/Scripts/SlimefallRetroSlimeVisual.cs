using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Unit))]
public class SlimefallRetroSlimeVisual : MonoBehaviour
{
    const int FrameWidth = 96;
    const int FrameHeight = 32;
    const float PixelsPerUnit = 64f;

    public bool isPlayerTeam = true;
    public float baseScale = 1.1f;
    public float idleFps = 10f;
    public float hurtFps = 18f;
    public float landFps = 16f;
    public float deathFps = 18f;

    static readonly Dictionary<string, Sprite[]> SpriteStripCache = new();

    sealed class AnimationSet
    {
        public Sprite[] Idle;
        public Sprite[] Hurt;
        public Sprite[] Land;
        public Sprite[] Death;
        public Sprite[] JumpStart;
        public Sprite[] JumpFall;
        public Sprite JumpUp;
        public Sprite JumpDown;
    }

    static AnimationSet _playerSet;
    static AnimationSet _enemySet;

    Unit _unit;
    Rigidbody2D _rigidbody;
    Collider2D _collider;
    PlayerMovement _movement;
    SpriteRenderer _renderer;
    Transform _visualRoot;
    AnimationSet _animations;
    bool _wasGrounded;
    float _hurtElapsed = float.PositiveInfinity;
    float _landElapsed = float.PositiveInfinity;
    float _deathElapsed = float.PositiveInfinity;
    float _idleClock;
    float _jumpElapsed = float.PositiveInfinity;

    void Awake()
    {
        _unit = GetComponent<Unit>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _movement = GetComponent<PlayerMovement>();
        SetupRenderer();
        ApplyTeamVisual();
    }

    void OnEnable()
    {
        if (_unit == null)
        {
            return;
        }

        _unit.OnHealthChanged += HandleHealthChanged;
        _unit.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (_unit == null)
        {
            return;
        }

        _unit.OnHealthChanged -= HandleHealthChanged;
        _unit.OnDeath -= HandleDeath;
    }

    void Update()
    {
        if (_renderer == null || _animations == null)
        {
            return;
        }

        Vector2 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
        bool grounded = ResolveGroundedState(velocity);

        if (!grounded && _wasGrounded && velocity.y > 0.08f)
        {
            _jumpElapsed = 0f;
        }

        if (grounded && !_wasGrounded)
        {
            _landElapsed = 0f;
        }

        _wasGrounded = grounded;
        _idleClock += Time.deltaTime;
        _hurtElapsed += Time.deltaTime;
        _landElapsed += Time.deltaTime;
        _deathElapsed += Time.deltaTime;
        _jumpElapsed += Time.deltaTime;

        _renderer.sprite = ResolveFrame(grounded, velocity);
        ApplyFacing(velocity);
        ApplyScale(grounded, velocity);
    }

    public void TriggerShoot()
    {
        if (_visualRoot != null)
        {
            _visualRoot.localScale = new Vector3(baseScale * 1.08f, baseScale * 0.96f, 1f);
        }
    }

    public void SetTeam(bool playerTeam)
    {
        isPlayerTeam = playerTeam;
        if (_renderer == null)
        {
            return;
        }

        ApplyTeamVisual();
    }

    void HandleHealthChanged(int currentHp, int maxHp)
    {
        if (currentHp > 0)
        {
            _hurtElapsed = 0f;
        }
    }

    void HandleDeath()
    {
        _deathElapsed = 0f;
    }

    Sprite ResolveFrame(bool grounded, Vector2 velocity)
    {
        if (_unit != null && _unit.IsDead && _animations.Death.Length > 0)
        {
            return GetAnimatedFrame(_animations.Death, _deathElapsed, deathFps, holdLastFrame: true);
        }

        if (_hurtElapsed < GetClipDuration(_animations.Hurt, hurtFps))
        {
            return GetAnimatedFrame(_animations.Hurt, _hurtElapsed, hurtFps, holdLastFrame: false);
        }

        if (!grounded)
        {
            if (_jumpElapsed < GetClipDuration(_animations.JumpStart, 18f) && _animations.JumpStart.Length > 0)
            {
                return GetAnimatedFrame(_animations.JumpStart, _jumpElapsed, 18f, holdLastFrame: true);
            }

            if (velocity.y > 0.08f)
            {
                return _animations.JumpUp != null ? _animations.JumpUp : GetFirstFrame(_animations.Idle);
            }

            if (velocity.y < -0.08f)
            {
                return _animations.JumpDown != null ? _animations.JumpDown : GetFirstFrame(_animations.Idle);
            }

            if (_animations.JumpFall.Length > 0)
            {
                return GetAnimatedFrame(_animations.JumpFall, _idleClock, 14f, holdLastFrame: false);
            }
        }

        if (_landElapsed < GetClipDuration(_animations.Land, landFps))
        {
            return GetAnimatedFrame(_animations.Land, _landElapsed, landFps, holdLastFrame: false);
        }

        return GetAnimatedFrame(_animations.Idle, _idleClock, idleFps, holdLastFrame: false);
    }

    void ApplyFacing(Vector2 velocity)
    {
        if (Mathf.Abs(velocity.x) > 0.05f)
        {
            _renderer.flipX = velocity.x < 0f;
        }
    }

    void ApplyScale(bool grounded, Vector2 velocity)
    {
        float scaleX = baseScale;
        float scaleY = baseScale;

        if (!grounded)
        {
            if (velocity.y > 0.05f)
            {
                scaleX *= 0.96f;
                scaleY *= 1.04f;
            }
            else if (velocity.y < -0.05f)
            {
                scaleX *= 1.05f;
                scaleY *= 0.95f;
            }
        }
        else if (Mathf.Abs(velocity.x) > 0.08f)
        {
            float wobble = Mathf.Sin(Time.time * 12f) * 0.03f;
            scaleX *= 1f + wobble;
            scaleY *= 1f - wobble * 0.55f;
        }

        if (_landElapsed < 0.15f)
        {
            float t = 1f - Mathf.Clamp01(_landElapsed / 0.15f);
            scaleX *= 1f + t * 0.08f;
            scaleY *= 1f - t * 0.08f;
        }

        if (_visualRoot != null)
        {
            _visualRoot.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }

    bool ResolveGroundedState(Vector2 velocity)
    {
        if (velocity.y > 0.1f)
        {
            return false;
        }

        if (_movement != null)
        {
            return _movement.IsGroundedNow;
        }

        return IsGrounded();
    }

    bool IsGrounded()
    {
        if (_collider == null)
        {
            return false;
        }

        Bounds bounds = _collider.bounds;
        Vector2 center = new(bounds.center.x, bounds.min.y - 0.05f);
        Vector2 size = new(bounds.size.x * 0.6f, 0.08f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit == _collider || hit.isTrigger)
            {
                continue;
            }

            if (hit.attachedRigidbody != null && hit.attachedRigidbody == _rigidbody)
            {
                continue;
            }

            if (hit.GetComponent<ProjectileController>() != null || hit.GetComponentInParent<ProjectileController>() != null)
            {
                continue;
            }

            if (hit.GetComponent<Unit>() != null || hit.GetComponentInParent<Unit>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    static AnimationSet BuildAnimationSet(string color)
    {
        return new AnimationSet
        {
            Idle = LoadStrip($"Sprite Sheet - {color} Idle"),
            Hurt = LoadStrip($"Sprite Sheet - {color} Hurt"),
            Land = LoadStrip($"Sprite Sheet - {color} Jump Land"),
            Death = LoadStrip($"Sprite Sheet - {color} Death"),
            JumpStart = LoadStrip($"Sprite Sheet - {color} Jump Start-up"),
            JumpFall = LoadStrip($"Sprite Sheet - {color} Jump to Fall"),
            JumpUp = GetFirstFrame(LoadStrip($"Sprite Sheet - {color} Jump Up")),
            JumpDown = GetFirstFrame(LoadStrip($"Sprite Sheet - {color} Jump Down"))
        };
    }

    static Sprite[] LoadStrip(string resourceName)
    {
        if (SpriteStripCache.TryGetValue(resourceName, out Sprite[] cached))
        {
            return cached;
        }

        Texture2D texture = Resources.Load<Texture2D>($"Slimes/{resourceName}");
        if (texture == null)
        {
            Debug.LogWarning($"SlimefallRetroSlimeVisual: missing slime sheet '{resourceName}'.");
            SpriteStripCache[resourceName] = System.Array.Empty<Sprite>();
            return SpriteStripCache[resourceName];
        }

        texture.filterMode = FilterMode.Point;

        int frameCount = Mathf.Max(1, texture.width / FrameWidth);
        Sprite[] frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            Rect rect = new Rect(i * FrameWidth, 0f, FrameWidth, FrameHeight);
            frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.08f), PixelsPerUnit, 0u, SpriteMeshType.FullRect);
            frames[i].name = $"{resourceName}_{i}";
        }

        SpriteStripCache[resourceName] = frames;
        return frames;
    }

    static Sprite GetAnimatedFrame(Sprite[] frames, float elapsed, float fps, bool holdLastFrame)
    {
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        if (holdLastFrame)
        {
            int lastIndex = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * fps));
            return frames[lastIndex];
        }

        int index = Mathf.FloorToInt(elapsed * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;
        }

        return frames[index];
    }

    static float GetClipDuration(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0 || fps <= 0f)
        {
            return 0f;
        }

        return frames.Length / fps;
    }

    static Sprite GetFirstFrame(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    void SetupRenderer()
    {
        Transform existingRoot = transform.Find("GB_SlimeVisualRoot");
        if (existingRoot == null)
        {
            GameObject rootObject = new GameObject("GB_SlimeVisualRoot");
            existingRoot = rootObject.transform;
            existingRoot.SetParent(transform, false);
        }

        _visualRoot = existingRoot;
        _visualRoot.localPosition = new Vector3(0f, CalculateVisualYOffset(), 0f);
        _visualRoot.localRotation = Quaternion.identity;
        _visualRoot.localScale = Vector3.one * baseScale;

        _renderer = _visualRoot.GetComponent<SpriteRenderer>();
        if (_renderer == null)
        {
            _renderer = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null && rootRenderer != _renderer)
        {
            rootRenderer.enabled = false;
        }

        _renderer.sortingOrder = 10;
        _renderer.color = Color.white;
        _unit.bodyRenderer = _renderer;
    }

    void ApplyTeamVisual()
    {
        _animations = isPlayerTeam ? (_playerSet ??= BuildAnimationSet("Green")) : (_enemySet ??= BuildAnimationSet("Red"));
        if (_renderer != null)
        {
            _renderer.sprite = GetFirstFrame(_animations.Idle);
            _renderer.color = Color.white;
        }
    }

    float CalculateVisualYOffset()
    {
        if (_collider == null)
        {
            return -0.42f;
        }

        Bounds bounds = _collider.bounds;
        return -(bounds.extents.y - 0.06f);
    }
}


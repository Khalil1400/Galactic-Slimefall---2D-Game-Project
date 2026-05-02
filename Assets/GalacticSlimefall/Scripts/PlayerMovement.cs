using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Unit))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4.5f;
    public float jumpForce = 7.5f;
    public float groundedDistance = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;
    public int extraAirJumps = 1;
    public float ledgeAssistHeight = 0.42f;
    public float ledgeAssistForwardCheck = 0.18f;
    public LayerMask groundLayers = ~0;

    Rigidbody2D _rigidbody;
    Collider2D _collider;
    Unit _unit;
    float _moveInput;
    float _lastGroundedTime = float.NegativeInfinity;
    float _lastJumpPressedTime = float.NegativeInfinity;
    int _airJumpsRemaining;
    bool _isGrounded;
    bool _wasGroundedLastFrame;

    public bool IsGroundedNow => _isGrounded;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _unit = GetComponent<Unit>();

        jumpForce = Mathf.Max(jumpForce, 8.15f);
        groundedDistance = Mathf.Max(groundedDistance, 0.18f);
        jumpBufferTime = Mathf.Max(jumpBufferTime, 0.18f);
        coyoteTime = Mathf.Max(coyoteTime, 0.15f);
        ledgeAssistHeight = Mathf.Max(ledgeAssistHeight, 0.5f);
        ledgeAssistForwardCheck = Mathf.Max(ledgeAssistForwardCheck, 0.22f);
    }

    void Update()
    {
        RefreshGroundedState();

        if (!CanControl())
        {
            _moveInput = 0f;
            return;
        }

        _moveInput = ReadMoveInput();

        if (ReadJumpPressed())
        {
            _lastJumpPressedTime = Time.time;
        }
    }

    void FixedUpdate()
    {
        if (_rigidbody.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        RefreshGroundedState();

        Vector2 velocity = _rigidbody.linearVelocity;

        if (!CanControl())
        {
            velocity.x = 0f;
            _rigidbody.linearVelocity = velocity;
            return;
        }

        velocity.x = _moveInput * moveSpeed;

        bool canUseCoyoteJump = Time.time - _lastGroundedTime <= coyoteTime;
        bool hasBufferedJump = Time.time - _lastJumpPressedTime <= jumpBufferTime;

        if (hasBufferedJump && (canUseCoyoteJump || _airJumpsRemaining > 0))
        {
            velocity.y = jumpForce;
            if (!canUseCoyoteJump)
            {
                _airJumpsRemaining = Mathf.Max(0, _airJumpsRemaining - 1);
            }

            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
            _isGrounded = false;
            _wasGroundedLastFrame = false;
            GalacticSlimefallAudio.PlayJump(transform.position, !canUseCoyoteJump);
        }

        TryAssistLedge(ref velocity);
        _rigidbody.linearVelocity = velocity;
    }

    void RefreshGroundedState()
    {
        _isGrounded = ComputeIsGrounded();
        if (_isGrounded)
        {
            _lastGroundedTime = Time.time;
            _airJumpsRemaining = extraAirJumps;
            _wasGroundedLastFrame = true;
        }
        else if (_wasGroundedLastFrame)
        {
            _wasGroundedLastFrame = false;
        }
    }

    bool CanControl()
    {
        if (_unit == null || _unit.IsDead || GameManager.Instance == null)
        {
            return false;
        }

        if (!GameManager.Instance.IsCurrentTurnHumanControlled || !GameManager.Instance.IsHumanControlledUnit(_unit))
        {
            return false;
        }

        if (MainMenuController.IsMenuOpen)
        {
            return false;
        }

        if (GameManager.Instance.IsTurnTransitioning)
        {
            return false;
        }

        return TurnManager.Instance != null && TurnManager.Instance.GetActiveUnitForCurrentTurn() == _unit;
    }

    bool ComputeIsGrounded()
    {
        Bounds bounds = _collider.bounds;
        Vector2 boxCenter = new Vector2(bounds.center.x, bounds.min.y - groundedDistance * 0.5f);
        Vector2 boxSize = new Vector2(bounds.size.x * 0.7f, groundedDistance);
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, groundLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit != _collider)
            {
                return true;
            }
        }

        return false;
    }

    void TryAssistLedge(ref Vector2 velocity)
    {
        if (Mathf.Abs(_moveInput) < 0.01f || velocity.y < -0.25f || _isGrounded)
        {
            return;
        }

        Bounds bounds = _collider.bounds;
        float direction = Mathf.Sign(_moveInput);
        float probeX = bounds.center.x + direction * (bounds.extents.x + 0.02f);
        Vector2 lowerProbe = new Vector2(probeX, bounds.min.y + 0.06f);
        Vector2 upperProbe = lowerProbe + Vector2.up * ledgeAssistHeight;

        bool blockedLow = Physics2D.Raycast(lowerProbe, Vector2.right * direction, ledgeAssistForwardCheck, groundLayers);
        bool blockedHigh = Physics2D.Raycast(upperProbe, Vector2.right * direction, ledgeAssistForwardCheck, groundLayers);
        if (!blockedLow || blockedHigh)
        {
            return;
        }

        Vector2 topProbe = new Vector2(bounds.center.x + direction * (bounds.extents.x + ledgeAssistForwardCheck), bounds.max.y + ledgeAssistHeight);
        RaycastHit2D landingHit = Physics2D.Raycast(topProbe, Vector2.down, bounds.size.y + ledgeAssistHeight + 0.1f, groundLayers);
        if (!landingHit.collider)
        {
            return;
        }

        float stepHeight = landingHit.point.y - bounds.min.y;
        if (stepHeight <= 0.02f || stepHeight > ledgeAssistHeight)
        {
            return;
        }

        _rigidbody.position += Vector2.up * (stepHeight + 0.02f);
        velocity.y = Mathf.Max(velocity.y, 0f);
    }

    static float ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        float move = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                move -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                move += 1f;
            }
        }

        if (Gamepad.current != null)
        {
            float stick = Gamepad.current.leftStick.ReadValue().x;
            if (Mathf.Abs(stick) > Mathf.Abs(move))
            {
                move = stick;
            }
        }

        return Mathf.Clamp(move, -1f, 1f);
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    static bool ReadJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboardJump = Keyboard.current != null &&
                            (Keyboard.current.spaceKey.wasPressedThisFrame ||
                             Keyboard.current.wKey.wasPressedThisFrame ||
                             Keyboard.current.upArrowKey.wasPressedThisFrame);

        bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return keyboardJump || gamepadJump;
#else
        return Input.GetButtonDown("Jump");
#endif
    }

    void OnDrawGizmosSelected()
    {
        Collider2D activeCollider = _collider != null ? _collider : GetComponent<Collider2D>();
        if (activeCollider == null)
        {
            return;
        }

        Bounds bounds = activeCollider.bounds;
        Vector2 boxCenter = new Vector2(bounds.center.x, bounds.min.y - groundedDistance * 0.5f);
        Vector2 boxSize = new Vector2(bounds.size.x * 0.7f, groundedDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}









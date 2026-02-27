using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Requires a PlayerInput component on the same GameObject.
/// Set PlayerInput Behaviour to "Send Messages" or "Invoke Unity Events".
/// Uses the default Unity Input System "Player" action map with actions:
///   Move (Vector2), Look (Vector2), Jump, Sprint (used as Dash), Crouch, Fire (used as Slam)
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    //  Prediction Structs
    // ─────────────────────────────────────────────────────────────────

    public struct MoveInputData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;
        public float YRot;
        public bool Jump;
        public bool Dash;
        public bool Crouch;
        public bool Slam;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct MoveStateData : IReconcileData
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float YRot;
        public float XRot;
        public bool IsGrounded;
        public int AirJumps;
        public int DashCharges;
        public float DashCooldown;
        public float SlideCooldown;
        public float CoyoteTimer;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("Ground Movement")]
    [SerializeField] float groundSpeed = 14f;
    [SerializeField] float groundAccel = 80f;
    [SerializeField] float groundFriction = 10f;

    [Header("Air Movement")]
    [SerializeField] float airSpeed = 14f;
    [SerializeField] float airAccel = 24f;
    [SerializeField] float airControl = 0.3f;

    [Header("Jumping / Bhop")]
    [SerializeField] float jumpForce = 10f;
    [SerializeField] int maxAirJumps = 0;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.15f;
    [SerializeField] float bhopWindow = 0.1f;

    [Header("Gravity")]
    [SerializeField] float gravity = -28f;
    [SerializeField] float fastFallMult = 2f;
    [SerializeField] float terminalVelocity = -55f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 26f;
    [SerializeField] float dashDuration = 0.12f;
    [SerializeField] float dashCooldown = 0.8f;
    [SerializeField] int maxDashCharges = 2;

    [Header("Crouch & Slide")]
    [SerializeField] float crouchSpeed = 7f;
    [SerializeField] float slideBoostSpeed = 22f;
    [SerializeField] float slideFriction = 1.2f;
    [SerializeField] float slideDuration = 0.65f;
    [SerializeField] float slideCooldown = 0.35f;
    [SerializeField] float slideMinSpeed = 6f;
    [SerializeField] float crouchHeightMult = 0.5f;
    [SerializeField] float crouchTransSpeed = 12f;

    [Header("Slam")]
    [SerializeField] float slamSpeed = 50f;
    [SerializeField] float slamBounceForce = 14f;

    [Header("Wall Jump")]
    [SerializeField] float wallJumpUpForce = 8f;
    [SerializeField] float wallJumpOutForce = 10f;
    [SerializeField] float wallCheckDist = 0.62f;
    [SerializeField] LayerMask wallLayers = ~0;

    [Header("Camera")]
    [SerializeField] Transform camPivot;
    [SerializeField] float mouseSensitivity = 0.15f;
    [SerializeField] float maxLookAngle = 89f;

    // ─────────────────────────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────────────────────────

    CharacterController _cc;
    PlayerInput _playerInput;

    // Input actions resolved once from the PlayerInput component
    InputAction _moveAction;
    InputAction _lookAction;
    InputAction _jumpAction;
    InputAction _dashAction;
    InputAction _crouchAction;
    InputAction _slamAction;

    // Buffered input state
    Vector2 _moveInput;
    bool _jumpHeld;
    bool _crouchHeld;
    bool _dashPressed;   // one-shot, set by callback
    bool _slamPressed;   // one-shot, set by callback

    // Physics state (reconciled)
    Vector3 _velocity;
    float _yRot, _xRot;
    bool _isGrounded;
    int _airJumps;
    int _dashCharges;
    float _dashCooldown, _slideCooldown, _coyoteTimer;

    // Local only
    float _dashTimer, _slideTimer, _bhopTimer, _jumpBufferTimer;
    bool _isSliding, _isCrouching, _isSlamming;
    float _standingHeight, _currentColliderHeight;

    // ─────────────────────────────────────────────────────────────────
    //  Awake
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        _standingHeight = _cc.height;
        _currentColliderHeight = _standingHeight;
        _dashCharges = maxDashCharges;

        // Resolve actions from the asset by name.
        // These match the default Unity Input System "Player" action map.
        // If your action map or action names differ, change the strings below.
        var map = _playerInput.actions.FindActionMap("Player", throwIfNotFound: true);
        _moveAction = map.FindAction("Move", throwIfNotFound: true);
        _lookAction = map.FindAction("Look", throwIfNotFound: true);
        _jumpAction = map.FindAction("Jump", throwIfNotFound: true);
        _dashAction = map.FindAction("Sprint", throwIfNotFound: true);  // Sprint = Dash
        _crouchAction = map.FindAction("Crouch", throwIfNotFound: true);
        _slamAction = map.FindAction("Fire", throwIfNotFound: true);  // Fire = Slam

        // One-shot press callbacks — never miss a fast tap between frames
        _dashAction.performed += _ => _dashPressed = true;
        _slamAction.performed += _ => _slamPressed = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  FishNet
    // ─────────────────────────────────────────────────────────────────

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        TimeManager.OnTick += OnTick;
        TimeManager.OnPostTick += OnPostTick;

        // Disable input on non-owners so other players don't read local keyboard
        if (!Owner.IsLocalClient)
        {
            _playerInput.enabled = false;
            enabled = false;
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        TimeManager.OnTick -= OnTick;
        TimeManager.OnPostTick -= OnPostTick;
    }

    private void OnTick() => Replicate(default);
    private void OnPostTick() => CreateReconcile();

    public override void CreateReconcile()
    {
        Reconcile(new MoveStateData
        {
            Position = transform.position,
            Velocity = _velocity,
            YRot = _yRot,
            XRot = _xRot,
            IsGrounded = _isGrounded,
            AirJumps = _airJumps,
            DashCharges = _dashCharges,
            DashCooldown = _dashCooldown,
            SlideCooldown = _slideCooldown,
            CoyoteTimer = _coyoteTimer,
        });
    }

    [Replicate]
    private void Replicate(MoveInputData input,
                           ReplicateState state = ReplicateState.Invalid,
                           Channel channel = Channel.Unreliable)
    { }

    [Reconcile]
    private void Reconcile(MoveStateData state, Channel channel = Channel.Unreliable)
    {
        if (!IsOwner) return;
        transform.position = state.Position;
        _velocity = state.Velocity;
        _yRot = state.YRot;
        _xRot = state.XRot;
        _isGrounded = state.IsGrounded;
        _airJumps = state.AirJumps;
        _dashCharges = state.DashCharges;
        _dashCooldown = state.DashCooldown;
        _slideCooldown = state.SlideCooldown;
        _coyoteTimer = state.CoyoteTimer;
        _dashTimer = 0f;
        _slideTimer = 0f;
        _isSliding = false;
        _isSlamming = false;
        _jumpBufferTimer = 0f;
        SetColliderHeight(_standingHeight);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Update
    // ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;

        // ── Look ─────────────────────────────────────────────────────
        Vector2 look = _lookAction.ReadValue<Vector2>();
        _yRot += look.x * mouseSensitivity;
        _xRot = Mathf.Clamp(_xRot - look.y * mouseSensitivity, -maxLookAngle, maxLookAngle);
        transform.rotation = Quaternion.Euler(0f, _yRot, 0f);
        if (camPivot != null)
            camPivot.localRotation = Quaternion.Euler(_xRot, 0f, 0f);

        // ── Read input ───────────────────────────────────────────────
        _moveInput = _moveAction.ReadValue<Vector2>();
        _jumpHeld = _jumpAction.IsPressed();
        _crouchHeld = _crouchAction.IsPressed();
        bool dash = _dashPressed; _dashPressed = false;
        bool slam = _slamPressed; _slamPressed = false;

        // ── Ground check ─────────────────────────────────────────────
        bool wasGrounded = _isGrounded;
        _isGrounded = _cc.isGrounded;

        if (_isGrounded)
        {
            _coyoteTimer = coyoteTime;
            _airJumps = maxAirJumps;
            _dashCharges = maxDashCharges;
            if (!wasGrounded) _bhopTimer = bhopWindow;
            if (_isSlamming) { _velocity.y = slamBounceForce; _isSlamming = false; }
        }
        else
        {
            _coyoteTimer = Mathf.Max(0f, _coyoteTimer - dt);
        }

        // ── Timers ───────────────────────────────────────────────────
        _dashCooldown = Mathf.Max(0f, _dashCooldown - dt);
        _slideCooldown = Mathf.Max(0f, _slideCooldown - dt);
        _dashTimer = Mathf.Max(0f, _dashTimer - dt);
        _slideTimer = Mathf.Max(0f, _slideTimer - dt);
        _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);
        _bhopTimer = Mathf.Max(0f, _bhopTimer - dt);

        if (_jumpHeld) _jumpBufferTimer = jumpBufferTime;

        // ── Slam ─────────────────────────────────────────────────────
        if (slam && !_isGrounded && !_isSlamming)
        {
            _velocity = Vector3.down * slamSpeed;
            _isSlamming = true;
        }

        // ── Dash ─────────────────────────────────────────────────────
        if (dash && _dashCharges > 0 && _dashCooldown <= 0f)
        {
            Vector3 dir = GetWishDir();
            if (dir == Vector3.zero) dir = transform.forward;
            _velocity = dir * dashSpeed;
            _velocity.y = 0f;
            _dashTimer = dashDuration;
            _dashCooldown = dashCooldown;
            _dashCharges--;
        }

        // ── Crouch / Slide ───────────────────────────────────────────
        HandleCrouchAndSlide();

        // ── Wall jump ────────────────────────────────────────────────
        bool wallJumped = false;
        if (!_isGrounded && _jumpBufferTimer > 0f)
        {
            Vector3 wallNormal = GetWallNormal();
            if (wallNormal != Vector3.zero)
            {
                _velocity = wallNormal * wallJumpOutForce;
                _velocity.y = wallJumpUpForce;
                _jumpBufferTimer = 0f;
                wallJumped = true;
            }
        }

        // ── Jump / Bhop ──────────────────────────────────────────────
        if (!wallJumped && _jumpBufferTimer > 0f)
        {
            if (_isGrounded || _coyoteTimer > 0f)
            {
                bool isBhop = _bhopTimer > 0f;
                if (!isBhop)
                {
                    float hSpeed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
                    if (hSpeed > groundSpeed)
                    {
                        float scale = groundSpeed / hSpeed;
                        _velocity.x *= scale;
                        _velocity.z *= scale;
                    }
                }
                _velocity.y = jumpForce;
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                _bhopTimer = 0f;
                if (_isSliding) StopSlide();
            }
            else if (_airJumps > 0)
            {
                _velocity.y = jumpForce;
                _airJumps--;
                _jumpBufferTimer = 0f;
            }
        }

        // ── Gravity ──────────────────────────────────────────────────
        if (!_isGrounded && !_isSlamming)
        {
            float gMult = (_moveInput.y < -0.5f && _velocity.y < 0f) ? fastFallMult : 1f;
            _velocity.y += gravity * gMult * dt;
            _velocity.y = Mathf.Max(_velocity.y, terminalVelocity);
        }

        // ── Horizontal physics ───────────────────────────────────────
        if (_dashTimer <= 0f && !_isSlamming)
        {
            if (_isSliding) SlidePhysics(dt);
            else if (_isGrounded) GroundPhysics(dt);
            else AirPhysics(dt);
        }

        // ── Apply ────────────────────────────────────────────────────
        _cc.Move(_velocity * dt);
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;

        // ── Smooth collider height ───────────────────────────────────
        float targetHeight = (_isCrouching || _isSliding)
            ? _standingHeight * crouchHeightMult
            : _standingHeight;
        _currentColliderHeight = Mathf.Lerp(_currentColliderHeight, targetHeight, crouchTransSpeed * dt);
        SetColliderHeight(_currentColliderHeight);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Crouch & Slide
    // ─────────────────────────────────────────────────────────────────

    void HandleCrouchAndSlide()
    {
        float hSpeed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        if (_crouchHeld && _isGrounded)
        {
            if (!_isCrouching && !_isSliding)
            {
                if (hSpeed >= slideMinSpeed && _slideCooldown <= 0f)
                    StartSlide();
                else
                    _isCrouching = true;
            }
        }
        else if (!_crouchHeld)
        {
            if (_isSliding) StopSlide();
            _isCrouching = false;
        }

        if (_isSliding && _slideTimer <= 0f)
            StopSlide();
    }

    void StartSlide()
    {
        Vector3 dir = GetWishDir();
        if (dir == Vector3.zero) dir = transform.forward;
        _velocity.x = dir.x * slideBoostSpeed;
        _velocity.z = dir.z * slideBoostSpeed;
        _isSliding = true;
        _isCrouching = true;
        _slideTimer = slideDuration;
    }

    void StopSlide()
    {
        _isSliding = false;
        _slideCooldown = slideCooldown;
        _isCrouching = _crouchHeld;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Physics helpers
    // ─────────────────────────────────────────────────────────────────

    void GroundPhysics(float dt)
    {
        float targetSpeed = _isCrouching ? crouchSpeed : groundSpeed;
        float speed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
        if (speed > 0f)
        {
            float scale = Mathf.Max(speed - speed * groundFriction * dt, 0f) / speed;
            _velocity.x *= scale;
            _velocity.z *= scale;
        }
        Accelerate(GetWishDir(), targetSpeed, groundAccel, dt);
    }

    void AirPhysics(float dt)
    {
        Accelerate(GetWishDir(), airSpeed, airAccel * airControl, dt);
    }

    void SlidePhysics(float dt)
    {
        float speed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
        if (speed > 0f)
        {
            float scale = Mathf.Max(speed - speed * slideFriction * dt, 0f) / speed;
            _velocity.x *= scale;
            _velocity.z *= scale;
        }
    }

    void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
    {
        if (wishDir == Vector3.zero) return;
        float currentSpeed = Vector3.Dot(new Vector3(_velocity.x, 0f, _velocity.z), wishDir);
        float addSpeed = wishSpeed - currentSpeed;
        if (addSpeed <= 0f) return;
        float accelAmount = Mathf.Min(accel * dt * wishSpeed, addSpeed);
        _velocity.x += wishDir.x * accelAmount;
        _velocity.z += wishDir.z * accelAmount;
    }

    Vector3 GetWishDir()
    {
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
        return (fwd * _moveInput.y + right * _moveInput.x).normalized;
    }

    Vector3 GetWallNormal()
    {
        Vector3[] dirs = { transform.right, -transform.right, transform.forward, -transform.forward };
        foreach (var dir in dirs)
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallCheckDist, wallLayers))
                return hit.normal;
        return Vector3.zero;
    }

    void SetColliderHeight(float height)
    {
        _cc.height = height;
        _cc.center = new Vector3(0f, height * 0.5f, 0f);
    }
}
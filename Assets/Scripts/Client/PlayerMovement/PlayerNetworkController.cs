using FishNet.Object;
using FishNet.Managing.Timing;
using UnityEngine;

namespace PlayerMovement
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [SerializeField] private MoveConfig _cfg;
        [SerializeField] private Transform  _cameraTransform;
        [SerializeField] private Camera     _playerCamera;

        [Header("Interpolation")]
        [SerializeField] private float _interpSpeed = 120f;

        private MovementSimulation  _sim;
        private InputModule         _input;
        private CameraModule        _camMod;
        private CharacterController _cc;
        private PlayerState         _state;
        private Vector3             _targetPos;
        private float               _targetYaw;
        private bool                _hasTarget;

        // ── Grounded buffering ───────────────────────────────────────────────────
        // cc.isGrounded only returns true the frame *after* the controller has
        // settled on the ground. At low tick-rates (FishNet default ~20 Hz) this
        // causes a one-tick gap that breaks the slide entry condition.
        // We latch "grounded" for one extra tick so the condition is always stable.
        private bool _groundedLastTick;
        private const float GroundedLatchFrames = 2; // ticks to stay "grounded" after leaving
        private int  _groundedLatchCounter;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _cc  = GetComponent<CharacterController>();
            _sim = new MovementSimulation();
            _sim.Initialise(_cfg, transform, _cc, IsCrouchBlocked, _cameraTransform);

            _state.Position      = transform.position;
            _state.YawDegrees    = transform.eulerAngles.y;
            _state.BhopSpeedMult = 1f;
            _state.IsGrounded    = _cc.isGrounded;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _cc  = GetComponent<CharacterController>();

            _sim = new MovementSimulation();
            _sim.Initialise(_cfg, transform, _cc, IsCrouchBlocked, _cameraTransform);

            _camMod = new CameraModule();
            _camMod.Initialise(_cfg, _cameraTransform);

            if (!IsServerInitialized && !IsOwner)
                _cc.enabled = false;

            if (IsOwner)
            {
                _input = new InputModule();
                _input.Initialise();
                _cc.enabled = true;

                _state.Position      = transform.position;
                _state.YawDegrees    = transform.eulerAngles.y;
                _state.BhopSpeedMult = 1f;
                _state.IsGrounded    = _cc.isGrounded;

                if (_playerCamera)
                {
                    _playerCamera.enabled = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                }

                TimeManager.OnTick += OnOwnerTick;
            }
            else
            {
                if (_playerCamera)
                    _playerCamera.gameObject.SetActive(false);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner)
            {
                TimeManager.OnTick -= OnOwnerTick;
                _input?.Dispose();
            }
        }

        private bool IsCrouchBlocked() =>
            Physics.SphereCast(transform.position, _cc.radius, Vector3.up,
                out _, _cfg.StandHeight - _cfg.CrouchHeight + 0.05f);

        // ── Grounded helper ──────────────────────────────────────────────────────
        // Returns a latched grounded value that stays true for GroundedLatchFrames
        // ticks after cc.isGrounded goes false. This smooths over the one-tick gap
        // that CharacterController has after a Move() call at low tick rates.
        private bool GetLatchedGrounded()
        {
            if (_cc.isGrounded)
            {
                _groundedLatchCounter = (int)GroundedLatchFrames;
                return true;
            }

            if (_groundedLatchCounter > 0)
            {
                _groundedLatchCounter--;
                return true;
            }

            return false;
        }

        private void OnOwnerTick()
        {
            uint  tick = TimeManager.LocalTick;
            float dt   = (float)TimeManager.TickDelta;

            PlayerInput inp = _input.Build(tick, dt);

            // Look
            _camMod.ApplyLook(transform, inp.Look);

            // FIX: Use latched grounded so slide entry condition is stable across ticks
            _state.IsGrounded = GetLatchedGrounded();

            PlayerState next = _sim.Step(_state, inp,
                _cameraTransform.forward, _cameraTransform.right,
                transform.eulerAngles.y);

            _cc.Move(next.Velocity * inp.DeltaTime);

            // FIX: After Move(), read the real grounded state and latch it for next tick,
            // but do NOT clobber IsSliding/IsCrouching flags that Step() just set.
            bool physicsGrounded = GetLatchedGrounded();
            next.IsGrounded = physicsGrounded;
            next.Position   = transform.position;
            next.Tick       = inp.Tick;
            _state          = next;

            // Auto-align body to wall during wallrun
            bool wallRun = next.Flags.HasFlag(StateFlags.IsWallRunning);
            if (wallRun && _sim != null)
            {
                Vector3 wallNormal = _sim.WallRun.IsOnLeftWall
                    ? _sim.WallRun.LeftWallHit.normal
                    : _sim.WallRun.RightWallHit.normal;
                Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up).normalized;
                if (Vector3.Dot(wallForward, transform.forward) < 0f)
                    wallForward = -wallForward;

                float targetYaw = Mathf.Atan2(wallForward.x, wallForward.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(
                    transform.eulerAngles.y, targetYaw,
                    _cfg.WallRunYawSpeed * inp.DeltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            // Camera visuals
            bool onRight   = next.Flags.HasFlag(StateFlags.IsOnRightWall);
            bool crouching = next.Flags.HasFlag(StateFlags.IsCrouching);
            float leanRoll = _sim.Lean.LeanRoll;

            _camMod.UpdateVisuals(wallRun, onRight, crouching, leanRoll, inp.DeltaTime);

            SendInputToServer(inp, transform.eulerAngles.y);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendInputToServer(PlayerInput input, float clientYaw)
        {
            _state.IsGrounded = GetLatchedGrounded();

            PlayerState next = _sim.Step(_state, input,
                _cameraTransform.forward, _cameraTransform.right, clientYaw);

            _cc.Move(next.Velocity * input.DeltaTime);

            next.IsGrounded = GetLatchedGrounded();
            next.Position   = transform.position;
            next.Tick       = input.Tick;
            _state          = next;

            BroadcastState(next);
        }

        [ObserversRpc(BufferLast = true)]
        private void BroadcastState(PlayerState state)
        {
            _targetPos = state.Position;
            _targetYaw = state.YawDegrees;
            _hasTarget = true;
        }

        private void Update()
        {
            if (IsServerInitialized || !_hasTarget) return;

            float t = Time.deltaTime * _interpSpeed;
            transform.position = Vector3.Lerp(transform.position, _targetPos, t);

            if (!IsOwner)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.Euler(0f, _targetYaw, 0f), t);
            }
        }
    }
}
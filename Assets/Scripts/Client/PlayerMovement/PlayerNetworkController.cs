using FishNet.Object;
using FishNet.Managing.Timing;
using UnityEngine;

namespace PlayerMovement
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [SerializeField] private MoveConfig _cfg;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Camera _playerCamera;

        [Header("Interpolation")]
        [SerializeField] private float _interpSpeed = 60f;

        private MovementSimulation _sim;
        private InputModule _input;
        private CameraModule _camMod;
        private CharacterController _cc;
        private PlayerState _state;
        private Vector3 _targetPos;
        private float _targetYaw;
        private bool _hasTarget;
        // vertical rotation is now maintained by CameraModule; keep for legacy uses
        private float _verticalRotation;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _cc = GetComponent<CharacterController>();
            _sim = new MovementSimulation();
            _sim.Initialise(_cfg, transform, _cc, IsCrouchBlocked, _cameraTransform);

            _state.Position = transform.position;
            _state.YawDegrees = transform.eulerAngles.y;
            _state.BhopSpeedMult = 1f;
            _state.IsGrounded = _cc.isGrounded;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _cc = GetComponent<CharacterController>();

            // Initialise a local simulation on the client so the owner can
            // perform client-side prediction for smooth, low-latency movement.
            _sim = new MovementSimulation();
            _sim.Initialise(_cfg, transform, _cc, IsCrouchBlocked, _cameraTransform);

            // camera helper (used by owner for look/tilt, harmless for others)
            _camMod = new CameraModule();
            _camMod.Initialise(_cfg, _cameraTransform);

            // Disable the character controller on non-owned clients when the
            // server isn't running locally so only the owner simulates movement.
            if (!IsServerInitialized && !IsOwner)
                _cc.enabled = false;

            if (IsOwner)
            {
                _input = new InputModule();
                _input.Initialise();
                _cc.enabled = true;

                // Seed local state for prediction
                _state.Position = transform.position;
                _state.YawDegrees = transform.eulerAngles.y;
                _state.BhopSpeedMult = 1f;
                _state.IsGrounded = _cc.isGrounded;

                if (_playerCamera)
                {
                    _playerCamera.enabled = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
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

        private bool IsCrouchBlocked()
        {
            return Physics.SphereCast(transform.position, _cc.radius, Vector3.up,
                out _, _cfg.StandHeight - _cfg.CrouchHeight + 0.05f);
        }

        private void OnOwnerTick()
        {
            uint tick = TimeManager.LocalTick;
            float dt = (float)TimeManager.TickDelta;

            PlayerInput inp = _input.Build(tick, dt);

            // Handle look locally through the camera module
            Vector2 look = inp.Look;
            _camMod.ApplyLook(transform, look);

            // --- Local prediction: simulate movement locally before sending input ---
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;

            // inform the sim whether we're presently grounded before prediction
            _state.IsGrounded = _cc.isGrounded;

            PlayerState next = _sim.Step(_state, inp, camForward, camRight, transform.eulerAngles.y);

            // Apply predicted movement immediately for smooth responsiveness
            _cc.Move(next.Velocity * inp.DeltaTime);
            next.IsGrounded = _cc.isGrounded;
            next.Position = transform.position;
            next.Tick = inp.Tick;
            _state = next;

            // camera visual updates (tilt & height & auto wall-track)
            bool wallRun    = next.Flags.HasFlag(StateFlags.IsWallRunning);
            bool onRight    = next.Flags.HasFlag(StateFlags.IsOnRightWall);
            bool crouching  = next.Flags.HasFlag(StateFlags.IsCrouching);
            Vector3 wallNormal = wallRun ? _sim.WallRun.GetCurrentWallNormal() : Vector3.zero;
            _camMod.UpdateVisuals(wallRun, onRight, crouching, inp.DeltaTime, wallNormal, transform);

            // Send the same input to the server (server remains authoritative)
            SendInputToServer(inp, transform.eulerAngles.y);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendInputToServer(PlayerInput input, float clientYaw)
        {
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;

            // make sure simulation knows whether we're currently touching the ground
            _state.IsGrounded = _cc.isGrounded;

            PlayerState next = _sim.Step(_state, input, camForward, camRight, clientYaw);

            // apply movement and then refresh grounded flag for the next tick
            _cc.Move(next.Velocity * input.DeltaTime);
            next.IsGrounded = _cc.isGrounded;
            next.Position = transform.position;
            next.Tick = input.Tick;

            _state = next;

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
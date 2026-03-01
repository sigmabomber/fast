using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerMovement
{
    public class InputModule
    {
        private InputAction _move, _look, _jump, _dash, _slam, _sprint, _crouch;
        private bool _jumpLatch, _dashLatch, _slamLatch, _crouchJustPressed;

        public void Initialise()
        {
            var map = new InputActionMap("Player");
            _move   = map.AddAction("Move",   InputActionType.Value);
            _look   = map.AddAction("Look",   InputActionType.Value);
            _jump   = map.AddAction("Jump",   InputActionType.Button);
            _dash   = map.AddAction("Dash",   InputActionType.Button);
            _slam   = map.AddAction("Slam",   InputActionType.Button);
            _sprint = map.AddAction("Sprint", InputActionType.Button);
            _crouch = map.AddAction("Crouch", InputActionType.Button);

            _move.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");
            
            _jump.AddBinding("<Keyboard>/space");   
            _jump.AddBinding("<Gamepad>/buttonSouth");
            
            _dash.AddBinding("<Keyboard>/q");        
            _dash.AddBinding("<Gamepad>/buttonEast");
            
            _slam.AddBinding("<Keyboard>/leftCtrl"); 
            _slam.AddBinding("<Gamepad>/buttonNorth");
            
            _sprint.AddBinding("<Keyboard>/leftShift"); 
            _sprint.AddBinding("<Gamepad>/leftStickPress");
            
            _crouch.AddBinding("<Keyboard>/c");      
            _crouch.AddBinding("<Gamepad>/rightStickPress");

            _jump.performed   += _ => _jumpLatch         = true;
            _dash.performed   += _ => _dashLatch         = true;
            _slam.performed   += _ => _slamLatch         = true;
            _crouch.performed += _ => _crouchJustPressed = true;

            map.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        public Vector2 ReadLook() => _look.ReadValue<Vector2>();

        public void Dispose()
        {
            _move?.Dispose(); _look?.Dispose(); _jump?.Dispose();
            _dash?.Dispose(); _slam?.Dispose(); _sprint?.Dispose(); _crouch?.Dispose();
        }

        public PlayerInput Build(uint tick, float dt)
        {
            Vector2 moveInput = _move.ReadValue<Vector2>();
            
            var inp = new PlayerInput
            {
                Tick             = tick,
                DeltaTime        = dt,
                Move             = moveInput,
                Look             = _look.ReadValue<Vector2>(),
                Sprint           = _sprint.IsPressed(),
                Crouch           = _crouch.IsPressed(),
                JumpPressed      = _jumpLatch,
                JumpHeld         = _jump.IsPressed(),
                DashPressed      = _dashLatch,
                SlamPressed      = _slamLatch,
                CrouchJustPressed= _crouchJustPressed,
            };
            
            _jumpLatch = _dashLatch = _slamLatch = _crouchJustPressed = false;
            return inp;
        }
    }
}
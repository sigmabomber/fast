using UnityEngine;

namespace PlayerMovement
{
    public class MovementSimulation
    {
        public GroundModule Ground = new();
        public AirModule Air = new();
        public GravityModule Gravity = new();
        public DashModule Dash = new();
        public WallRunModule WallRun = new();
        public SlideModule Slide = new();

        public void Initialise(MoveConfig cfg, Transform body, CharacterController cc, 
                              System.Func<bool> crouchBlocked, Transform cameraTransform)
        {
            Ground.Initialise(cfg);
            Air.Initialise(cfg);
            Gravity.Initialise(cfg);
            Dash.Initialise(cfg);
            WallRun.Initialise(cfg, body, cc);
            Slide.Initialise(cfg, cc, crouchBlocked, cameraTransform);
        }

        public PlayerState Step(PlayerState s, PlayerInput inp,
                                Vector3 camForward, Vector3 camRight, float bodyYawDeg)
        {
            // Get body-relative directions
            Quaternion bodyRot = Quaternion.Euler(0f, bodyYawDeg, 0f);
            Vector3 forward = bodyRot * Vector3.forward;
            Vector3 right = bodyRot * Vector3.right;

            bool isGrounded = s.IsGrounded;
            bool wasGrounded = s.WasGrounded;
            s.WasGrounded = isGrounded;

            // Ground check and timers
            Air.TickTimers(ref s, wasGrounded, isGrounded, inp);

            if (!wasGrounded && isGrounded)
            {
                s.Flags &= ~StateFlags.IsSlamming;
                Air.OnLanded(ref s);
                isGrounded = s.IsGrounded;
            }

            // Wall run
            WallRun.CheckWalls();
            WallRun.Simulate(ref s, inp, forward, isGrounded);
            bool isWallRunning = s.Flags.HasFlag(StateFlags.IsWallRunning);

            // Jump
            if (inp.JumpPressed)
            {
                if (!WallRun.TryWallJump(ref s))
                    Air.TryJump(ref s, isGrounded);
                isGrounded = false;
                isWallRunning = s.Flags.HasFlag(StateFlags.IsWallRunning);
            }


            // Movement
            bool isSliding = s.Flags.HasFlag(StateFlags.IsSliding);
            bool isDashing = s.Flags.HasFlag(StateFlags.IsDashing);

            if (!isSliding && !isWallRunning && !isDashing)
            {
                if (isGrounded)
                {
                    Ground.Simulate(ref s, inp, forward, right);
                }
                else
                {
                    Air.Simulate(ref s, inp, camForward, camRight);
                }
            }

            // Abilities
            Dash.Simulate(ref s, inp, forward, right);
            Slide.Simulate(ref s, inp, forward, isGrounded);
            Gravity.Simulate(ref s, inp);

            return s;
        }
    }
}
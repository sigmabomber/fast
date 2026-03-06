using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Central simulation step. Calls all movement modules in the correct order.
    ///
    /// Step order matters — here's why each module sits where it does:
    ///
    ///   1. TickTimers        — coyote/buffer timers need the pre-step grounded state.
    ///   2. OnLanded          — resets jumps, handles bhop on landing.
    ///   3. CheckWalls/Slope  — pure queries, no velocity writes.
    ///   4. Slide             — must run before GroundModule so it can own entry-frame
    ///                          velocity and block Ground from overwriting it.
    ///   5. Jump              — can exit slide, must see updated IsSliding flag.
    ///   6. Ground / Air      — only run when no other module owns movement.
    ///   7. Dash / Slam       — independent impulse modules.
    ///   8. Gravity           — last so it can see final IsSliding / IsDashing flags.
    ///   9. Lean              — visual only, no velocity.
    /// </summary>
    public class MovementSimulation
    {
        public GroundModule   Ground   = new();
        public AirModule      Air      = new();
        public GravityModule  Gravity  = new();
        public DashModule     Dash     = new();
        public WallRunModule  WallRun  = new();
        public SlideModule    Slide    = new();
        public SlamModule     Slam     = new();
        public SlopeModule    Slope    = new();
        public LeanModule     Lean     = new();

        private MoveConfig _cfg;

        public void Initialise(MoveConfig cfg, Transform body, CharacterController cc,
                               System.Func<bool> crouchBlocked, Transform cameraTransform)
        {
            Ground.Initialise(cfg);
            Ground.InitialiseCollider(cc, crouchBlocked);
            Air.Initialise(cfg);
            Gravity.Initialise(cfg);
            Dash.Initialise(cfg);
            WallRun.Initialise(cfg, body, cc);
            Slide.Initialise(cfg, cc, crouchBlocked, cameraTransform);
            Ground.SetSlideModule(Slide);
            Slam.Initialise(cfg);
            Slope.Initialise(cfg, cc);
            Lean.Initialise(cfg, cameraTransform);

            _cfg = cfg;
        }

        public PlayerState Step(PlayerState s, PlayerInput inp,
                                Vector3 camForward, Vector3 camRight, float bodyYawDeg)
        {
            Quaternion bodyRot = Quaternion.Euler(0f, bodyYawDeg, 0f);
            Vector3 forward    = bodyRot * Vector3.forward;
            Vector3 right      = bodyRot * Vector3.right;

            bool isGrounded  = s.IsGrounded;
            bool wasGrounded = s.WasGrounded;
            s.WasGrounded    = isGrounded;

            // ── 1. Timers ─────────────────────────────────────────────────────────
            Air.TickTimers(ref s, wasGrounded, isGrounded, inp);

            // ── 2. Landing ────────────────────────────────────────────────────────
            if (!wasGrounded && isGrounded)
            {
                s.Flags &= ~StateFlags.IsSlamming;
                Air.OnLanded(ref s);
                isGrounded = s.IsGrounded;
            }

            // ── 3. Wall Run & Slope queries ───────────────────────────────────────
            WallRun.CheckWalls();
            WallRun.Simulate(ref s, inp, forward, isGrounded);
            bool isWallRunning = s.Flags.HasFlag(StateFlags.IsWallRunning);

            Slope.CheckSlope();
            Slope.Simulate(ref s, inp, isGrounded);

            // ── 4. Slide ──────────────────────────────────────────────────────────
            // FIX: No longer passing preSlideFlatVel — SlideModule reads s.Velocity directly.
            // This ensures the velocity used for entry is always current and correct.
            Slide.Simulate(ref s, inp, forward, isGrounded, Slope.CurrentSlopeNormal);
            bool isSliding = s.Flags.HasFlag(StateFlags.IsSliding);

            // ── 5. Jump ───────────────────────────────────────────────────────────
            if (inp.JumpPressed)
            {
                if (isSliding)
                {
                    // Slide-jump: preserve horizontal momentum, clear slide state
                    s.Flags  &= ~StateFlags.IsSliding;
                    s.Flags  &= ~StateFlags.IsCrouching;
                    isSliding = false;
                    // Give a small extra boost in the slide direction
                    float horizSpeed = new Vector2(s.Velocity.x, s.Velocity.z).magnitude;
                    if (horizSpeed > 0.1f)
                    {
                        Vector3 slideDir = new Vector3(s.Velocity.x, 0f, s.Velocity.z).normalized;
                        s.Velocity.x = slideDir.x * Mathf.Max(horizSpeed, _cfg.WalkSpeed);
                        s.Velocity.z = slideDir.z * Mathf.Max(horizSpeed, _cfg.WalkSpeed);
                    }
                }

                if (!WallRun.TryWallJump(ref s))
                    Air.TryJump(ref s, isGrounded);

                isGrounded    = false;
                isWallRunning = s.Flags.HasFlag(StateFlags.IsWallRunning);
            }

            // ── 6. Core Movement ──────────────────────────────────────────────────
            bool isDashing = s.Flags.HasFlag(StateFlags.IsDashing);

            if (!isSliding && !isWallRunning && !isDashing)
            {
                if (isGrounded)
                    Ground.Simulate(ref s, inp, forward, right);
                else
                    Air.Simulate(ref s, inp, camForward, camRight);
            }

            // ── 7. Dash & Slam ────────────────────────────────────────────────────
            Dash.Simulate(ref s, inp, forward, right);
            Slam.Simulate(ref s, inp, isGrounded);

            // ── 8. Gravity ────────────────────────────────────────────────────────
            Gravity.Simulate(ref s, inp);

            // ── 9. Lean (visual only) ─────────────────────────────────────────────
            Lean.Simulate(inp, inp.DeltaTime);

            return s;
        }
    }
}
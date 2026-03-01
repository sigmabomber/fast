using UnityEngine;

namespace PlayerMovement
{
    public class GravityModule
    {
        private MoveConfig _cfg;
        
        public void Initialise(MoveConfig cfg)
        {
            _cfg = cfg;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp)
        {
            if (s.Flags.HasFlag(StateFlags.IsSliding)) return;
            if (s.IsGrounded) return;
            if (s.Flags.HasFlag(StateFlags.IsWallRunning)) return;
            if (s.Flags.HasFlag(StateFlags.IsDashing)) return;

            // Apply consistent gravity always - no jump-hold bonus
            float multiplier = s.Velocity.y < 0f ? _cfg.FallMultiplier : 1f;
            s.Velocity.y += _cfg.Gravity * multiplier * inp.DeltaTime;
            s.Velocity.y = Mathf.Max(s.Velocity.y, -_cfg.MaxFallSpeed);
        }

        /// <summary>
        /// Apply upward counter-force while wallrunning to negate some gravity.
        /// This creates a more responsive feel than lerp-based gravity.
        /// </summary>
        public void ApplyWallRunGravityCounter(ref PlayerState s, PlayerInput inp)
        {
            if (!s.Flags.HasFlag(StateFlags.IsWallRunning))
                return;

            // Counter about 15% of gravity with an upward force (Dani-style)
            float gravityForce = -_cfg.Gravity * 0.15f * _cfg.GravityForceMultiplier;
            s.Velocity.y += gravityForce * inp.DeltaTime;
        }
    }
}
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

            float multiplier = (s.Velocity.y < 0f || !inp.JumpHeld) ? _cfg.FallMultiplier : 1f;
            s.Velocity.y += _cfg.Gravity * multiplier * inp.DeltaTime;
            s.Velocity.y = Mathf.Max(s.Velocity.y, -_cfg.MaxFallSpeed);
        }
    }
}
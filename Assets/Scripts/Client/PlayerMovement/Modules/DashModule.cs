using UnityEngine;

namespace PlayerMovement
{
    public class DashModule
    {
        private MoveConfig _cfg;
        
        public void Initialise(MoveConfig cfg)
        {
            _cfg = cfg;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, Vector3 forward, Vector3 right)
        {
            s.DashCooldown = Mathf.Max(s.DashCooldown - inp.DeltaTime, 0f);

            bool isDashing = s.Flags.HasFlag(StateFlags.IsDashing);

            if (inp.DashPressed && s.DashCooldown <= 0f && !isDashing)
            {
                Vector3 dir = right * inp.Move.x + forward * inp.Move.y;
                if (dir.magnitude < 0.1f) dir = forward;
                dir.Normalize();

                s.DashDirection = dir;
                s.DashTimer = _cfg.DashDuration;
                s.DashCooldown = _cfg.DashCooldown;
                s.Flags |= StateFlags.IsDashing;
                isDashing = true;
            }

            if (isDashing)
            {
                s.DashTimer -= inp.DeltaTime;
                s.Velocity = s.DashDirection * _cfg.DashForce;
                s.Velocity.y = 0f;

                if (s.DashTimer <= 0f)
                {
                    s.Flags &= ~StateFlags.IsDashing;
                    s.Velocity = s.DashDirection * (_cfg.WalkSpeed * 0.8f);
                }
            }
        }
    }
}
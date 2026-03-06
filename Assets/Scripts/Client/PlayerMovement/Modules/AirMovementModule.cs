using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Air movement matching the Karlson reference feel.
    ///
    /// The key formula (from AirMove in the reference):
    ///   addSpeed = Clamp(airSpeed - dot(horizVel, wishDir), 0, airAcceleration * dt)
    ///   velocity += wishDir * addSpeed
    ///
    /// This means:
    ///   - If you're already moving fast in the wish direction, dot is high → addSpeed ≈ 0, no acceleration.
    ///   - If you strafe perpendicular, dot ≈ 0 → full addSpeed, you accelerate into the new direction.
    ///   - Speed can build beyond airSpeed by continuously strafing, which is the intentional bhop/strafe feel.
    /// Coyote time, jump buffer, and bhop are preserved from the original system.
    /// </summary>
    public class AirModule
    {
        private MoveConfig _cfg;

        public void Initialise(MoveConfig cfg) => _cfg = cfg;

        // ── Timers ────────────────────────────────────────────────────────────────
        public void TickTimers(ref PlayerState s, bool wasGrounded, bool isGrounded, PlayerInput inp)
        {
            if (wasGrounded && !isGrounded)
                s.CoyoteTimer = _cfg.CoyoteTime;
            else
                s.CoyoteTimer = Mathf.Max(s.CoyoteTimer - inp.DeltaTime, 0f);

            s.JumpBufferTimer = Mathf.Max(s.JumpBufferTimer - inp.DeltaTime, 0f);
            if (inp.JumpPressed)
                s.JumpBufferTimer = _cfg.JumpBufferTime;
        }

        // ── Landing ───────────────────────────────────────────────────────────────
        public void OnLanded(ref PlayerState s)
        {
            s.JumpsRemaining = _cfg.MaxJumps;

            if (s.JumpBufferTimer > 0f)
            {
                s.BhopSpeedMult = Mathf.Min(s.BhopSpeedMult * _cfg.BhopSpeedBoost, _cfg.BhopMaxSpeedMult);
                PerformJump(ref s, true);
                s.IsGrounded = false;
            }
            else
            {
                s.BhopSpeedMult = 1f;
                if (s.Velocity.y < -2f) s.Velocity.y = -2f;
            }
        }

        // ── Jump ─────────────────────────────────────────────────────────────────
        public bool TryJump(ref PlayerState s, bool isGrounded)
        {
            bool canJump = isGrounded || s.CoyoteTimer > 0f || s.JumpsRemaining > 0;
            if (!canJump) return false;

            s.JumpBufferTimer = 0f;

            if (isGrounded)
                s.BhopSpeedMult = Mathf.Min(s.BhopSpeedMult * _cfg.BhopSpeedBoost, _cfg.BhopMaxSpeedMult);

            PerformJump(ref s, isGrounded);
            return true;
        }

        // ── Air Simulation — Full Control ────────────────────────────────────────
        public void Simulate(ref PlayerState s, PlayerInput inp,
                             Vector3 camForward, Vector3 camRight)
        {
            Vector3 fwd = camForward; fwd.y = 0f;
            Vector3 rgt = camRight;   rgt.y = 0f;
            if (fwd.sqrMagnitude > 0.001f) fwd.Normalize();
            if (rgt.sqrMagnitude > 0.001f) rgt.Normalize();

            Vector3 flatVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);

            if (inp.Move.magnitude > 0.1f)
            {
                Vector3 wishDir   = (rgt * inp.Move.x + fwd * inp.Move.y).normalized;
                Vector3 wishVel   = wishDir * _cfg.MaxAirSpeed;

                // Accelerate toward wish velocity — same instant-direction approach as ground
                // but with a slower acceleration so jumps still feel floaty, not snappy.
                float currentSpeed = flatVel.magnitude;
                float dot          = Vector3.Dot(flatVel.normalized, wishDir);

                // If changing direction significantly, rotate velocity toward wish dir immediately
                // so the player doesn't feel like they're fighting momentum mid-air.
                if (currentSpeed > 0.1f && dot < 0.99f)
                {
                    Vector3 newDir = Vector3.RotateTowards(
                        flatVel.normalized,
                        wishDir,
                        _cfg.AirTurnSpeed * Mathf.Deg2Rad * inp.DeltaTime,
                        0f);
                    flatVel = newDir * currentSpeed;
                }

                // Then accelerate/decelerate speed toward target
                float newSpeed = Mathf.MoveTowards(currentSpeed, _cfg.MaxAirSpeed,
                                                   _cfg.AirAcceleration * inp.DeltaTime);
                if (currentSpeed < 0.1f)
                    flatVel = wishDir * newSpeed;
                else
                    flatVel = flatVel.normalized * newSpeed;
            }

            s.Velocity.x = flatVel.x;
            s.Velocity.z = flatVel.z;
        }

        // ── Private ───────────────────────────────────────────────────────────────
        private void PerformJump(ref PlayerState s, bool wasGrounded)
        {
            s.Flags &= ~StateFlags.IsSliding;
            s.Flags &= ~StateFlags.IsCrouching;

            s.Velocity.y  = _cfg.JumpForce;
            s.CoyoteTimer = 0f;

            if (!wasGrounded)
                s.JumpsRemaining = Mathf.Max(s.JumpsRemaining - 1, 0);
        }
    }
}
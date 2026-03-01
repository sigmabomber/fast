using UnityEngine;

namespace PlayerMovement
{
    public class AirModule
    {
        private MoveConfig _cfg;
        
        public void Initialise(MoveConfig cfg)
        {
            _cfg = cfg;
        }

        public void TickTimers(ref PlayerState s, bool wasGrounded, bool isGrounded, PlayerInput inp)
        {
            // Coyote time
            if (wasGrounded && !isGrounded)
                s.CoyoteTimer = _cfg.CoyoteTime;
            else
                s.CoyoteTimer = Mathf.Max(s.CoyoteTimer - inp.DeltaTime, 0f);

            // Jump buffer
            s.JumpBufferTimer = Mathf.Max(s.JumpBufferTimer - inp.DeltaTime, 0f);
            if (inp.JumpPressed)
                s.JumpBufferTimer = _cfg.JumpBufferTime;
        }

        public void OnLanded(ref PlayerState s)
        {
            s.JumpsRemaining = _cfg.MaxJumps;

            if (s.JumpBufferTimer > 0f)
            {
                // Bhop on landing
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

        public void Simulate(ref PlayerState s, PlayerInput inp, 
                            Vector3 camForward, Vector3 camRight)
        {
            bool hasInput = inp.Move.magnitude > 0.1f;
            
            // Flatten camera vectors
            Vector3 flatCamForward = camForward;
            Vector3 flatCamRight = camRight;
            flatCamForward.y = 0f;
            flatCamRight.y = 0f;
            
            if (flatCamForward.sqrMagnitude > 0.01f)
                flatCamForward.Normalize();
            if (flatCamRight.sqrMagnitude > 0.01f)
                flatCamRight.Normalize();

            Vector3 flatVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float targetSpeed = inp.Sprint ? _cfg.SprintSpeed : _cfg.WalkSpeed;

            if (hasInput)
            {
                // Camera-relative movement direction (like working PlayerController)
                Vector3 wishDir = (flatCamRight * inp.Move.x + flatCamForward * inp.Move.y).normalized;
                Vector3 wishVel = wishDir * targetSpeed;

                float currentSpeed = flatVel.magnitude;
                
                if (currentSpeed > 0.1f)
                {
                    float dot = Vector3.Dot(flatVel.normalized, wishDir);
                    
                    if (dot < 0.95f) // Significant direction change
                    {
                        // Rotate velocity toward wish direction
                        Vector3 newDir = Vector3.RotateTowards(
                            flatVel.normalized, 
                            wishDir, 
                            _cfg.AirDirectionChangeSpeed * Mathf.Deg2Rad * inp.DeltaTime, 
                            1f
                        );
                        s.Velocity.x = newDir.x * currentSpeed;
                        s.Velocity.z = newDir.z * currentSpeed;
                    }
                    else
                    {
                        // Small direction change - accelerate
                        s.Velocity.x = Mathf.MoveTowards(s.Velocity.x, wishVel.x, 
                                            _cfg.AirAcceleration * inp.DeltaTime);
                        s.Velocity.z = Mathf.MoveTowards(s.Velocity.z, wishVel.z, 
                                            _cfg.AirAcceleration * inp.DeltaTime);
                    }
                }
                else
                {
                    // No speed - just accelerate
                    s.Velocity.x = Mathf.MoveTowards(s.Velocity.x, wishVel.x, 
                                        _cfg.AirAcceleration * inp.DeltaTime);
                    s.Velocity.z = Mathf.MoveTowards(s.Velocity.z, wishVel.z, 
                                        _cfg.AirAcceleration * inp.DeltaTime);
                }

                // Speed limit
                float newFlatSpeed = new Vector3(s.Velocity.x, 0f, s.Velocity.z).magnitude;
                if (newFlatSpeed > _cfg.MaxAirSpeed)
                {
                    Vector3 flatDir = new Vector3(s.Velocity.x, 0f, s.Velocity.z).normalized;
                    s.Velocity.x = flatDir.x * _cfg.MaxAirSpeed;
                    s.Velocity.z = flatDir.z * _cfg.MaxAirSpeed;
                }
            }
        }

        private void PerformJump(ref PlayerState s, bool wasGrounded)
        {
            Vector2 horizontalVel = new Vector2(s.Velocity.x, s.Velocity.z);
            float horizontalSpeed = horizontalVel.magnitude;
            
            s.Velocity.y = _cfg.JumpForce;
            s.CoyoteTimer = 0f;

            if (!wasGrounded)
                s.JumpsRemaining = Mathf.Max(s.JumpsRemaining - 1, 0);

            if (horizontalSpeed > 0.1f)
            {
                float boostedSpeed = Mathf.Min(horizontalSpeed * s.BhopSpeedMult,
                                              _cfg.WalkSpeed * _cfg.BhopMaxSpeedMult);
                
                Vector2 direction = horizontalVel.normalized;
                s.Velocity.x = direction.x * boostedSpeed;
                s.Velocity.z = direction.y * boostedSpeed;
            }
        }
    }
}
using UnityEngine;

namespace PlayerMovement
{
    public class GroundModule
    {
        private MoveConfig _cfg;
        
        public void Initialise(MoveConfig cfg)
        {
            _cfg = cfg;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, Vector3 forward, Vector3 right)
        {
            bool isCrouching = s.Flags.HasFlag(StateFlags.IsCrouching);
            bool isSliding = s.Flags.HasFlag(StateFlags.IsSliding);
            
            float targetSpeed = inp.Sprint ? _cfg.SprintSpeed : _cfg.WalkSpeed;
            
            // Calculate input direction: right * x (A/D) + forward * y (W/S)
            Vector3 inputDir = (right * inp.Move.x + forward * inp.Move.y);
            bool hasInput = inp.Move.magnitude > 0.1f;
            
            if (hasInput && inputDir.magnitude > 0.1f)
                inputDir.Normalize();
            
            Vector3 flatVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);

            if (hasInput)
            {
                // Project current velocity onto input direction
                float forwardSpeed = Vector3.Dot(flatVel, inputDir);
                forwardSpeed = Mathf.Max(forwardSpeed, 0f); // Discard backward component
                
                // Accelerate toward target speed
                float newSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, 
                                         _cfg.GroundAcceleration * inp.DeltaTime);
                
                s.Velocity.x = inputDir.x * newSpeed;
                s.Velocity.z = inputDir.z * newSpeed;
            }
            else
            {
                // Speed sliding: when crouching and moving forward, maintain speed with reduced friction
                bool speedSliding = isCrouching && !isSliding && flatVel.magnitude > _cfg.WalkSpeed;
                float frictionToApply = speedSliding ? _cfg.Friction * 0.4f : _cfg.Friction;
                
                // Friction when no input
                float brake = Mathf.Min(frictionToApply * inp.DeltaTime, flatVel.magnitude);
                if (flatVel.magnitude > 0.01f)
                {
                    s.Velocity.x -= flatVel.normalized.x * brake;
                    s.Velocity.z -= flatVel.normalized.z * brake;
                }
                else
                {
                    s.Velocity.x = 0f;
                    s.Velocity.z = 0f;
                }
            }
        }
    }
}
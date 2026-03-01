using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Handles slope/ramp surfing: detects when the player is on a slope and
    /// applies additional gravity and acceleration to make sliding downhill feel snappy.
    /// </summary>
    public class SlopeModule
    {
        private MoveConfig _cfg;
        private CharacterController _cc;

        public Vector3 CurrentSlopeNormal { get; private set; } = Vector3.up;
        public bool IsSurfing { get; private set; }

        public void Initialise(MoveConfig cfg, CharacterController cc)
        {
            _cfg = cfg;
            _cc = cc;
        }

        public void CheckSlope()
        {
            // Raycast downward to find the ground surface normal
            RaycastHit hit;
            Vector3 origin = _cc.transform.position;
            
            if (Physics.Raycast(origin, Vector3.down, out hit, _cc.height * 0.5f + 0.5f))
            {
                CurrentSlopeNormal = hit.normal;
                
                // Check if this is a slope (angle between 0 and maxSlopeAngle from horizontal)
                float angle = Vector3.Angle(Vector3.up, CurrentSlopeNormal);
                IsSurfing = angle > _cfg.MaxSlopeAngle && angle < 89f;
            }
            else
            {
                IsSurfing = false;
                CurrentSlopeNormal = Vector3.up;
            }
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, bool isGrounded)
        {
            if (!isGrounded || !IsSurfing)
                return;

            // Apply extra downslope acceleration
            // Get the direction along the slope (downward component)
            Vector3 slopeDir = Vector3.ProjectOnPlane(CurrentSlopeNormal, Vector3.right).normalized;
            if (slopeDir.y > 0f) slopeDir = -slopeDir; // Ensure it points downward

            // Add acceleration down the slope
            s.Velocity += slopeDir * _cfg.SurfSlopeAccel * inp.DeltaTime;
        }
    }
}

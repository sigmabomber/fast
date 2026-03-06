using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Minimal slide stub — all logic moved to GroundModule for simplicity.
    /// </summary>
    public class SlideModule
    {
        public void Initialise(MoveConfig cfg, CharacterController cc,
                               System.Func<bool> crouchBlocked, Transform cameraTransform, GroundModule groundModule = null)
        {
        }

        public bool TrySlide(ref PlayerState s, PlayerInput inp, Vector3 forward, Vector3 right)
        {
            return false;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, Vector3 forward, bool isGrounded, Vector3 slopeNormal)
        {
        }
    }
}

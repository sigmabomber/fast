using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Handles the "slam" ability: when the player presses the button while
    /// airborne the character is forced downward with a large negative velocity.
    /// The state flag is used so that the skill can only be triggered once per
    /// airtime, and it is cleared automatically by the movement simulation when
    /// the player lands.
    /// </summary>
    public class SlamModule
    {
        private MoveConfig _cfg;

        public void Initialise(MoveConfig cfg)
        {
            _cfg = cfg;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, bool isGrounded)
        {
            // Only allow slam while in the air and not already slamming.
            // Matches the behaviour in the example PlayerController script.
            if (inp.SlamPressed && !isGrounded && !s.Flags.HasFlag(StateFlags.IsSlamming))
            {
                s.Flags |= StateFlags.IsSlamming;
                s.Velocity.y = _cfg.SlamForce;
            }
        }
    }
}
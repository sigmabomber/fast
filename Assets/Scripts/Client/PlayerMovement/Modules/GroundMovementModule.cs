using UnityEngine;

namespace PlayerMovement
{
    public class GroundModule
    {
        private MoveConfig          _cfg;
        private CharacterController _cc;
        private System.Func<bool>   _crouchBlocked;
        private float               _originalHeight;
        private Vector3             _originalCenter;

        public void Initialise(MoveConfig cfg) => _cfg = cfg;

        public void InitialiseCollider(CharacterController cc, System.Func<bool> crouchBlocked)
        {
            _cc = cc;
            _crouchBlocked = crouchBlocked;
            if (_cc != null)
            {
                _originalHeight = _cc.height;
                _originalCenter = _cc.center;
            }
        }

        public void SetSlideModule(SlideModule slideModule) { }

        public void Simulate(ref PlayerState s, PlayerInput inp, Vector3 forward, Vector3 right)
        {
            bool isSliding = s.Flags.HasFlag(StateFlags.IsSliding);
            Vector3 horizontalVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float speed = horizontalVel.magnitude;

            // ── DEBUG ────────────────────────────────────────────────────────────────────
            // Remove these once the bug is confirmed fixed
            Debug.Log($"[Ground.Simulate] isSliding={isSliding} inp.Crouch={inp.Crouch} speed={speed:F2} SlideFriction={_cfg.SlideFriction}");

            // ── START SLIDE ──────────────────────────────────────────────────────────────
            if (!isSliding && inp.Crouch && speed >= _cfg.SlideThreshold)
            {
                Debug.Log($"[Ground.Simulate] >>> STARTING SLIDE at speed {speed:F2}");
                isSliding = true;
                s.Flags  |= StateFlags.IsSliding | StateFlags.IsCrouching;
                ResizeCollider(_originalHeight * _cfg.CrouchHeight / _cfg.StandHeight);
            }

            // ── UPDATE SLIDE ─────────────────────────────────────────────────────────────
            if (isSliding)
            {
                Debug.Log($"[Ground.Simulate] IN SLIDE — inp.Crouch={inp.Crouch} speed={speed:F2}");

                if (!inp.Crouch)
                {
                    Debug.Log("[Ground.Simulate] >>> STOPPING SLIDE — crouch released");
                    s.Flags      &= ~StateFlags.IsSliding;
                    s.Flags      &= ~StateFlags.IsCrouching;
                    s.Velocity.x  = 0f;
                    s.Velocity.z  = 0f;
                    ResizeCollider(_originalHeight);
                    return;
                }

                horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, _cfg.SlideFriction * inp.DeltaTime);
                speed         = horizontalVel.magnitude;
                Debug.Log($"[Ground.Simulate] slide decel applied — new speed={speed:F2} (decel={_cfg.SlideFriction * inp.DeltaTime:F4})");

                s.Velocity.x = horizontalVel.x;
                s.Velocity.z = horizontalVel.z;

                if (speed < 0.5f)
                {
                    Debug.Log("[Ground.Simulate] >>> STOPPING SLIDE — too slow");
                    s.Flags      &= ~StateFlags.IsSliding;
                    s.Velocity.x  = 0f;
                    s.Velocity.z  = 0f;
                    ResizeCollider(_originalHeight);
                    if (!inp.Crouch && !_crouchBlocked())
                        s.Flags &= ~StateFlags.IsCrouching;
                }

                return;
            }

            // ── NORMAL MOVEMENT ──────────────────────────────────────────────────────────
            Vector3 inputDir = right * inp.Move.x + forward * inp.Move.y;
            bool hasInput    = inp.Move.magnitude > 0.1f;
            if (hasInput && inputDir.sqrMagnitude > 0.01f) inputDir.Normalize();

            float targetSpeed = (inp.Sprint ? _cfg.SprintSpeed : _cfg.WalkSpeed) * s.BhopSpeedMult;

            if (hasInput)
            {
                float currentSpeed = horizontalVel.magnitude;
                float newSpeed     = Mathf.MoveTowards(currentSpeed, targetSpeed, _cfg.GroundAcceleration * inp.DeltaTime);
                horizontalVel      = inputDir * newSpeed;
            }
            else
            {
                horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, _cfg.Friction * inp.DeltaTime);
            }

            s.Velocity.x = horizontalVel.x;
            s.Velocity.z = horizontalVel.z;

            if (inp.Crouch && !_crouchBlocked())
                s.Flags |= StateFlags.IsCrouching;
            else if (!_crouchBlocked())
                s.Flags &= ~StateFlags.IsCrouching;
        }

        private void ResizeCollider(float newHeight)
        {
            if (_cc == null) return;
            _cc.height = newHeight;
            _cc.center = new Vector3(_originalCenter.x,
                                     _originalCenter.y * (newHeight / _originalHeight),
                                     _originalCenter.z);
        }
    }
}
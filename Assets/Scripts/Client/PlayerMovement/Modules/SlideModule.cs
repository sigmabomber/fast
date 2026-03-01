using UnityEngine;

namespace PlayerMovement
{
    public class SlideModule
    {
        private MoveConfig _cfg;
        private CharacterController _cc;
        private System.Func<bool> _crouchBlocked;
        private Transform _cameraTransform;
        private float _cameraCurrentY;

        public void Initialise(MoveConfig cfg, CharacterController cc,
                               System.Func<bool> crouchBlocked, Transform cameraTransform)
        {
            _cfg = cfg;
            _cc = cc;
            _crouchBlocked = crouchBlocked;
            _cameraTransform = cameraTransform;

            if (_cameraTransform != null)
                _cameraCurrentY = _cameraTransform.localPosition.y;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp,
                             Vector3 forward, bool isGrounded, Vector3 preMoveFlatVel)
        {
            bool crouchHeld = inp.Crouch;

            // ── Slide Start ──────────────────────────────────────────────────────────
            // Use pre-movement flat velocity so we capture full momentum before
            // GroundModule has a chance to apply friction and bleed it off.
            float horizSpeed = preMoveFlatVel.magnitude;

            if (inp.CrouchJustPressed && crouchHeld && isGrounded
                && !s.Flags.HasFlag(StateFlags.IsSliding)
                && horizSpeed >= _cfg.SlideThreshold)
            {
                StartSlide(ref s, preMoveFlatVel, forward);
            }

            // ── Slide Tick ───────────────────────────────────────────────────────────
            if (s.Flags.HasFlag(StateFlags.IsSliding))
            {
                Vector3 slideVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
                float slideSpeed = slideVel.magnitude;

                // Constant-magnitude drag (mirrors Dani's counter-force approach).
                // Fast slides stay fast; only the tail end bleeds out quickly.
                // Tune _cfg.SlideFriction in the range 3–8 (units/sec).
                float drag = _cfg.SlideFriction * inp.DeltaTime;
                slideVel = Vector3.MoveTowards(slideVel, Vector3.zero, drag);

                s.Velocity.x = slideVel.x;
                s.Velocity.z = slideVel.z;

                // End conditions: too slow OR player released crouch
                if (slideSpeed < 1.5f || !crouchHeld)
                    EndSlide(ref s);
            }

            // ── Normal Crouch (not sliding) ──────────────────────────────────────────
            if (!s.Flags.HasFlag(StateFlags.IsSliding))
            {
                if (crouchHeld && isGrounded)
                {
                    s.Flags |= StateFlags.IsCrouching;
                }
                else if (s.Flags.HasFlag(StateFlags.IsCrouching))
                {
                    if (!_crouchBlocked())
                        s.Flags &= ~StateFlags.IsCrouching;
                }
            }

            // ── Camera / Collider Height ─────────────────────────────────────────────
            UpdateCameraHeight(s, inp.DeltaTime);
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────────────────────────────

        private void StartSlide(ref PlayerState s, Vector3 flatVel, Vector3 forward)
        {
            s.Flags |= StateFlags.IsSliding | StateFlags.IsCrouching;

            // Direction: prefer actual velocity, fall back to camera-forward
            Vector3 slideDir = flatVel.magnitude > 0.1f ? flatVel.normalized : forward;
            float currentSpeed = flatVel.magnitude;

            // Never reduce speed on slide entry.
            // Give a small boost only if we're already at or above SlideForce;
            // otherwise snap up to SlideForce so slow-walks still feel snappy.
            float entrySpeed = Mathf.Max(currentSpeed, _cfg.SlideForce) * 1.1f;

            // Write back: preserve direction exactly, scale magnitude
            s.Velocity.x = slideDir.x * entrySpeed;
            s.Velocity.z = slideDir.z * entrySpeed;
        }

        private void EndSlide(ref PlayerState s)
        {
            s.Flags &= ~StateFlags.IsSliding;
            // Keep crouching flag — let the normal crouch block above decide
            // whether to clear it next frame (prevents a one-frame standing pop).
            // If the player has already released crouch the block above will
            // clear it immediately anyway.
        }

        private void UpdateCameraHeight(PlayerState s, float deltaTime)
        {
            if (_cameraTransform == null) return;

            bool crouched = s.Flags.HasFlag(StateFlags.IsCrouching)
                         || s.Flags.HasFlag(StateFlags.IsSliding);

            float targetCamY    = crouched ? _cfg.CrouchHeight * 0.6f  : _cfg.StandHeight * 0.45f;
            float targetCCHeight = crouched ? _cfg.CrouchHeight         : _cfg.StandHeight;

            // Smooth camera eye height
            _cameraCurrentY = Mathf.Lerp(_cameraCurrentY, targetCamY,
                                          deltaTime * _cfg.CameraCrouchSpeed);

            Vector3 pos = _cameraTransform.localPosition;
            _cameraTransform.localPosition = new Vector3(pos.x, _cameraCurrentY, pos.z);

            // Smooth collider height + re-centre
            if (Mathf.Abs(_cc.height - targetCCHeight) > 0.01f)
            {
                _cc.height = Mathf.Lerp(_cc.height, targetCCHeight, deltaTime * 15f);
                _cc.center = Vector3.up * (_cc.height * 0.5f);
            }
        }
    }
}
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

        // Separate flag so we know a slide genuinely ended this frame
        // and don't immediately re-enter via the crouch block.
        private bool _slideEndedThisFrame;

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
            _slideEndedThisFrame = false;

            // ── Slide Start ──────────────────────────────────────────────────────────
            // preMoveFlatVel is captured in MovementSimulation BEFORE GroundModule runs,
            // so it still holds full sprint momentum. This is the correct value to use.
            float horizSpeed = preMoveFlatVel.magnitude;

            if (inp.CrouchJustPressed
                && crouchHeld
                && isGrounded
                && !s.Flags.HasFlag(StateFlags.IsSliding)
                && horizSpeed >= _cfg.SlideThreshold)
            {
                StartSlide(ref s, preMoveFlatVel, forward);
            }

            // ── Slide Tick ───────────────────────────────────────────────────────────
            if (s.Flags.HasFlag(StateFlags.IsSliding))
            {
                Vector3 slideVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);

                // Apply drag first, then read the resulting speed for end-condition.
                // This prevents the "speed read before drag" stall bug where the slide
                // never officially ends because we check the old speed value.
                float drag = _cfg.SlideFriction * inp.DeltaTime;
                slideVel = Vector3.MoveTowards(slideVel, Vector3.zero, drag);

                s.Velocity.x = slideVel.x;
                s.Velocity.z = slideVel.z;

                // End conditions checked AFTER drag so speed is current
                float speedAfterDrag = slideVel.magnitude;
                if (speedAfterDrag < 1.5f || !crouchHeld)
                {
                    EndSlide(ref s);
                    _slideEndedThisFrame = true;
                }
            }

            // ── Normal Crouch (not sliding) ──────────────────────────────────────────
            // Guard: don't immediately re-crouch on the same frame the slide ended,
            // otherwise camera stays pinned low and the infinite-slide re-entry can occur.
            if (!s.Flags.HasFlag(StateFlags.IsSliding) && !_slideEndedThisFrame)
            {
                if (crouchHeld && isGrounded)
                {
                    s.Flags |= StateFlags.IsCrouching;
                }
                else
                {
                    // Hold-based crouch: only crouch while the button is held.
                    s.Flags &= ~StateFlags.IsCrouching;
                }
            }

            // If slide ended and crouch is no longer held, clear crouch immediately
            // so the camera rises and the player can walk at full height.
            if (_slideEndedThisFrame && !crouchHeld)
            {
                if (!_crouchBlocked())
                    s.Flags &= ~StateFlags.IsCrouching;
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

            // Never reduce speed on entry. Boost slightly above current speed,
            // but only use SlideForce as a floor for slow/stationary entries.
            float entrySpeed = Mathf.Max(currentSpeed * 1.1f, _cfg.SlideForce);

            s.Velocity.x = slideDir.x * entrySpeed;
            s.Velocity.z = slideDir.z * entrySpeed;
        }

        private void EndSlide(ref PlayerState s)
        {
            s.Flags &= ~StateFlags.IsSliding;
            // Also clear IsCrouching here. The normal-crouch block above will
            // re-set it next frame if the player is still holding crouch — but
            // NOT on the same frame thanks to _slideEndedThisFrame guard.
            // This ensures the camera always gets at least one frame to start rising.
            s.Flags &= ~StateFlags.IsCrouching;
        }

        private void UpdateCameraHeight(PlayerState s, float deltaTime)
        {
            if (_cameraTransform == null) return;

            bool crouched = s.Flags.HasFlag(StateFlags.IsCrouching)
                         || s.Flags.HasFlag(StateFlags.IsSliding);

            float targetCamY     = crouched ? _cfg.CrouchHeight * 0.6f : _cfg.StandHeight * 0.45f;
            float targetCCHeight = crouched ? _cfg.CrouchHeight        : _cfg.StandHeight;

            // If not crouched at all, snap back to standing height to avoid
            // lingering lerp artifacts where the controller remains low.
            if (!crouched)
            {
                _cameraCurrentY = targetCamY;
                Vector3 pos = _cameraTransform.localPosition;
                _cameraTransform.localPosition = new Vector3(pos.x, _cameraCurrentY, pos.z);

                _cc.height = targetCCHeight;
                _cc.center = Vector3.up * (_cc.height * 0.5f);
            }
            else
            {
                _cameraCurrentY = Mathf.Lerp(_cameraCurrentY, targetCamY,
                                              deltaTime * _cfg.CameraCrouchSpeed);

                Vector3 pos = _cameraTransform.localPosition;
                _cameraTransform.localPosition = new Vector3(pos.x, _cameraCurrentY, pos.z);

                if (Mathf.Abs(_cc.height - targetCCHeight) > 0.01f)
                {
                    _cc.height = Mathf.Lerp(_cc.height, targetCCHeight, deltaTime * 15f);
                    _cc.center = Vector3.up * (_cc.height * 0.5f);
                }
            }
        }
    }
}
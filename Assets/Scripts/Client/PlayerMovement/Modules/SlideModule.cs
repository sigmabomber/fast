using UnityEngine;

namespace PlayerMovement
{
    public class SlideModule
    {
        private MoveConfig _cfg;
        private CharacterController _cc;
        private System.Func<bool> _crouchBlocked;
        private Transform _cameraTransform;
        private float _cameraTargetY;
        private float _cameraCurrentY;

        public void Initialise(MoveConfig cfg, CharacterController cc, 
                              System.Func<bool> crouchBlocked, Transform cameraTransform)
        {
            _cfg = cfg;
            _cc = cc;
            _crouchBlocked = crouchBlocked;
            _cameraTransform = cameraTransform;
            
            if (_cameraTransform != null)
            {
                _cameraCurrentY = _cameraTransform.localPosition.y;
                _cameraTargetY = _cameraCurrentY;
            }
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, 
                            Vector3 forward, bool isGrounded)
        {
            bool crouchHeld = inp.Crouch;
            bool sprintHeld = inp.Sprint;
            Vector3 flatVel = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float speed = flatVel.magnitude;

            // Update timers
            s.SlideCooldown = Mathf.Max(s.SlideCooldown - inp.DeltaTime, 0f);
            s.SlideDurationTimer = Mathf.Max(s.SlideDurationTimer - inp.DeltaTime, 0f);

            // Try to start slide
            if (crouchHeld && sprintHeld && isGrounded && 
                !s.Flags.HasFlag(StateFlags.IsSliding) && 
                s.SlideCooldown <= 0f && inp.CrouchJustPressed)
            {
                if (speed >= _cfg.SprintSpeed * 0.5f)
                {
                    StartSlide(ref s, flatVel, forward);
                }
            }

            // Update slide
            if (s.Flags.HasFlag(StateFlags.IsSliding))
            {
                s.SlideCurrentSpeed = Mathf.MoveTowards(s.SlideCurrentSpeed, 0f, 
                                                        _cfg.SlideFriction * inp.DeltaTime);
                
                s.Velocity.x = s.SlideLockedDir.x * s.SlideCurrentSpeed;
                s.Velocity.z = s.SlideLockedDir.z * s.SlideCurrentSpeed;

                if (s.SlideDurationTimer <= 0f || 
                    s.SlideCurrentSpeed <= _cfg.SlideMinSpeed ||
                    !isGrounded || !crouchHeld)
                {
                    EndSlide(ref s);
                }
            }

            // Normal crouch (only when not sliding)
            if (!s.Flags.HasFlag(StateFlags.IsSliding))
            {
                if (crouchHeld && isGrounded && !sprintHeld)
                {
                    s.Flags |= StateFlags.IsCrouching;
                }
                else if (s.Flags.HasFlag(StateFlags.IsCrouching))
                {
                    if (!_crouchBlocked())
                        s.Flags &= ~StateFlags.IsCrouching;
                }
            }

            // Update camera height
            UpdateCameraHeight(s, inp.DeltaTime);
        }

        private void StartSlide(ref PlayerState s, Vector3 flatVel, Vector3 forward)
        {
            s.Flags |= StateFlags.IsSliding | StateFlags.IsCrouching;
            s.SlideDurationTimer = _cfg.SlideDuration;
            s.SlideCooldown = _cfg.SlideCooldown;
            
            s.SlideLockedDir = flatVel.magnitude > 0.1f ? flatVel.normalized : forward;
            s.SlideCurrentSpeed = Mathf.Max(flatVel.magnitude * 1.2f, _cfg.SlideForce);
            
            s.Velocity.x = s.SlideLockedDir.x * s.SlideCurrentSpeed;
            s.Velocity.z = s.SlideLockedDir.z * s.SlideCurrentSpeed;
        }

        private void EndSlide(ref PlayerState s)
        {
            s.Flags &= ~StateFlags.IsSliding;

            if (s.SlideCurrentSpeed > _cfg.WalkSpeed)
            {
                s.Velocity.x = s.SlideLockedDir.x * _cfg.WalkSpeed;
                s.Velocity.z = s.SlideLockedDir.z * _cfg.WalkSpeed;
            }
        }

        private void UpdateCameraHeight(PlayerState s, float deltaTime)
        {
            if (_cameraTransform == null) return;

            float targetY = s.Flags.HasFlag(StateFlags.IsCrouching) || 
                           s.Flags.HasFlag(StateFlags.IsSliding) 
                           ? _cfg.CrouchHeight * 0.6f 
                           : _cfg.StandHeight * 0.45f;
            
            _cameraTargetY = targetY;
            _cameraCurrentY = Mathf.Lerp(_cameraCurrentY, _cameraTargetY, 
                                         deltaTime * _cfg.CameraCrouchSpeed);
            
            Vector3 pos = _cameraTransform.localPosition;
            _cameraTransform.localPosition = new Vector3(pos.x, _cameraCurrentY, pos.z);

            // Update controller height
            float targetCCHeight = s.Flags.HasFlag(StateFlags.IsCrouching) || 
                                  s.Flags.HasFlag(StateFlags.IsSliding) 
                                  ? _cfg.CrouchHeight : _cfg.StandHeight;
            
            if (Mathf.Abs(_cc.height - targetCCHeight) > 0.01f)
            {
                _cc.height = Mathf.Lerp(_cc.height, targetCCHeight, deltaTime * 15f);
                _cc.center = Vector3.up * (_cc.height / 2f);
            }
        }
    }
}
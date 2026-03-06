using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// Lean module — Straftat corner peeking.
    ///
    /// Holding Q or E (mapped via PlayerInput.LeanLeft / LeanRight) tilts the
    /// camera and shifts it laterally so the player can peek around cover without
    /// moving their feet. The body yaw does NOT rotate; only the camera offset changes.
    ///
    /// Integration notes:
    ///   - Call Simulate() inside MovementSimulation.Step() after all movement modules.
    ///   - Apply LeanCameraOffset in CameraModule.UpdateVisuals by shifting localPosition.x.
    ///   - LeanRoll is blended into the camera's Z-rotation alongside WallRun tilt.
    /// </summary>
    public class LeanModule
    {
        private MoveConfig _cfg;
        private Transform _cameraTransform;

        private float _currentRoll;
        private float _currentOffset;

        /// <summary>Current roll in degrees (Z rotation applied to camera).</summary>
        public float LeanRoll => _currentRoll;

        /// <summary>Current horizontal camera offset in metres.</summary>
        public float LeanOffset => _currentOffset;

        public void Initialise(MoveConfig cfg, Transform cameraTransform)
        {
            _cfg = cfg;
            _cameraTransform = cameraTransform;
        }

        public void Simulate(PlayerInput inp, float dt)
        {
            // Determine target lean based on input
            // LeanLeft / LeanRight are new fields added to PlayerInput
            float targetRoll   = 0f;
            float targetOffset = 0f;

            if (inp.LeanLeft && !inp.LeanRight)
            {
                targetRoll   =  _cfg.LeanAngle;
                targetOffset = -_cfg.LeanPeekOffset;
            }
            else if (inp.LeanRight && !inp.LeanLeft)
            {
                targetRoll   = -_cfg.LeanAngle;
                targetOffset =  _cfg.LeanPeekOffset;
            }

            // Smooth lerp to target
            _currentRoll   = Mathf.Lerp(_currentRoll,   targetRoll,   dt * _cfg.LeanSpeed);
            _currentOffset = Mathf.Lerp(_currentOffset, targetOffset, dt * _cfg.LeanSpeed);

            // Apply directly to camera transform
            if (_cameraTransform == null) return;
            var p = _cameraTransform.localPosition;
            _cameraTransform.localPosition = new Vector3(_currentOffset, p.y, p.z);
        }
    }
}
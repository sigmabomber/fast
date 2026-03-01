using UnityEngine;

namespace PlayerMovement
{
    public class CameraModule
    {
        private MoveConfig _cfg;
        private Transform  _cam;
        private float _pitch;
        private float _tilt;
        private float _camY;

        public Vector3 CameraForward => _cam.forward;

        public void Initialise(MoveConfig cfg, Transform cam)
        {
            _cfg  = cfg;
            _cam  = cam;
            _camY = cam.localPosition.y;
        }

        // Call before Step() so movement uses updated look direction
        public void ApplyLook(Transform body, Vector2 delta)
        {
            body.Rotate(Vector3.up * delta.x * _cfg.MouseSensitivity);
            _pitch -= delta.y * _cfg.MouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
        }

        public void UpdateVisuals(bool wallRunning, bool onRightWall,
                                  bool crouching, float dt, Vector3 wallNormal = default, Transform body = null)
        {
            float targetTilt = wallRunning
                ? (onRightWall ? -_cfg.WallRunCameraTilt : _cfg.WallRunCameraTilt)
                : 0f;
            _tilt = Mathf.Lerp(_tilt, targetTilt, dt * _cfg.CameraTiltSpeed);
            
            // Auto-rotate body to face along the wall when wall-running
            if (wallRunning && wallNormal.magnitude > 0.1f && body != null)
            {
                // Calculate direction along the wall (perpendicular to normal)
                Vector3 wallDir = Vector3.Cross(wallNormal, Vector3.up).normalized;
                float targetYaw = Mathf.Atan2(wallDir.x, wallDir.z) * Mathf.Rad2Deg;
                
                // Smoothly interpolate body rotation toward wall direction
                float currentYaw = body.eulerAngles.y;
                float diffYaw = Mathf.DeltaAngle(currentYaw, targetYaw);
                float newYaw = currentYaw + Mathf.Clamp(diffYaw, -90f * dt, 90f * dt);
                body.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
            
            _cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f)
                               * Quaternion.Euler(0f, 0f, _tilt);

            float targetY = crouching
                ? _cfg.CrouchHeight * 0.6f
                : _cfg.StandHeight  * 0.45f;
            _camY = Mathf.Lerp(_camY, targetY, dt * _cfg.CameraCrouchSpeed);
            var p = _cam.localPosition;
            _cam.localPosition = new Vector3(p.x, _camY, p.z);
        }
    }
}
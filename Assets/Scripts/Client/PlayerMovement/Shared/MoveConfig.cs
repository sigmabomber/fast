using UnityEngine;

namespace PlayerMovement
{
    [CreateAssetMenu(menuName = "PlayerMovement/MoveConfig", fileName = "MoveConfig")]
    public class MoveConfig : ScriptableObject
    {
       [Header("Ground Movement")]
        public float WalkSpeed          = 7f;
        public float SprintSpeed        = 12f;
        public float GroundAcceleration = 80f;   // high: reach target speed in ~1 frame
        public float Friction           = 25f;   // high: stop dead the moment input releases
        public float StopSpeed          = 4f;

        [Header("Air Movement")]
        public float MaxAirSpeed     = 7f;    // target horizontal speed while airborne
        public float AirAcceleration = 30f;   // how fast speed reaches MaxAirSpeed
        public float AirTurnSpeed    = 720f;  // degrees/sec direction rotation in air — high = full control

        [Header("Jump")]
        public float JumpForce          = 14f;
        public float Gravity            = -30f;
        public float FallMultiplier     = 1.5f;
        public float MaxFallSpeed       = 80f;
        public int   MaxJumps           = 2;
        public float CoyoteTime         = 0.12f;
        public float JumpBufferTime     = 0.18f;


        [Header("Bhop")]
        public float BhopSpeedBoost     = 1.10f;   // Per-hop multiplier
        public float BhopMaxSpeedMult   = 2.5f;    // Hard cap
        [Header("Slide")]
        public float SlideThreshold     = 4f;      // Speed needed to start slide (matches Karlson)
        public float SlideBoostFactor   = 1.1f;    // Unused in new system
        public float SlideMinBoost      = 12f;     // Force applied on slide start (slideForce in Karlson)
        public float SlideFriction = 10f;  
        public float SlopeSlideBoost    = 30f;     // Unused in new system
        public float SlideExitMomentumBuffer = 0.2f; // Unused in new system


        [Header("Wall Run")]
        public float WallRunSpeed            = 22f;
        public float WallRunGravity          = -6f;   // gentle fall while on wall
        public float WallRunTime             = 2.0f;
        public float WallRunJumpForce        = 14f;
        public float WallRunJumpSideForce    = 10f;
        public float WallRunJumpForwardForce = 7f;
        public float WallRunYawSpeed         = 200f;
        public float WallCheckDistance       = 0.75f;
        public LayerMask WallLayer           = ~0;

        [Header("Lean")]
        public float LeanAngle           = 16f;    // degrees to tilt body/camera
        public float LeanSpeed           = 12f;    // lerp speed
        public float LeanPeekOffset      = 0.3f;   // horizontal camera offset in metres

        [Header("Dash")]
        public float DashForce           = 35f;
        public float DashDuration        = 0.12f;
        public float DashCooldown        = 0.9f;

        [Header("Slam")]
        public float SlamForce           = -55f;

        [Header("Crouch & Stand")]
        public float CrouchHeight        = 1.0f;
        public float StandHeight         = 2.0f;
        public float CameraCrouchSpeed   = 16f;

        [Header("Slope")]
        public float MaxSlopeAngle            = 40f;
        public float SurfSlopeAccel           = 50f;   // used by SlopeModule
        public float SurfGravityMultiplier    = 1.5f;

        [Header("Gravity Tuning")]
        public float GravityForceMultiplier   = 1.0f;  // used by GravityModule wall-run counter

        [Header("Camera")]
        public float MouseSensitivity    = 0.15f;
        public float WallRunCameraTilt   = 8f;
        public float CameraTiltSpeed     = 14f;
    }
}
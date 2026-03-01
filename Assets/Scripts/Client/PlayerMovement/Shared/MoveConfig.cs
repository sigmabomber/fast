using UnityEngine;

namespace PlayerMovement
{
    [CreateAssetMenu(menuName = "PlayerMovement/MoveConfig", fileName = "MoveConfig")]
    public class MoveConfig : ScriptableObject
    {
        [Header("Movement")]
        // original values from the reference script; keeps pacing familiar
        public float WalkSpeed          = 18f;
        public float SprintSpeed        = 24f;
        public float GroundAcceleration = 80f;
        public float AirAcceleration    = 20f;
        public float Friction           = 30f;

        [Header("Jump")]
        public float JumpForce       = 13f;   // slight boost to compensate for lower gravity
        public float Gravity         = -25f;  // reduced for gentler arc
        public float FallMultiplier  = 1.4f;  // reduced for smoother descent
        public float MaxFallSpeed    = 60f;
        public float GravityForceMultiplier = 1.0f; // force-based gravity counter (Dani style)
        public int   MaxJumps        = 2;

        [Header("Bhop")]
        public float BhopSpeedBoost  = 1.12f;
        public float BhopMaxSpeedMult= 2.0f;
        public float JumpBufferTime  = 0.15f;
        public float CoyoteTime      = 0.12f;

        [Header("Dash")]
        public float DashForce    = 30f;
        public float DashDuration = 0.10f;
        public float DashCooldown = 0.8f;

        [Header("Wall Run")]
        public float     WallRunSpeed            = 20f;
        public float     WallRunGravity          = -15f; // steep downward slide while on wall
        public float     WallRunJumpForce        = 13f;
        public float     WallRunJumpSideForce    = 9f;
        public float     WallRunJumpForwardForce = 6f;
        public float     WallRunYawSpeed         = 180f;
        public float     WallRunInitialUpForce   = 5f;  // minimal upward impulse when entering wallrun
        public float     WallRunEscapeForce      = 30f;  // push away when cancelling wallrun
        public float     WallCheckDistance       = 0.7f;
        public float     WallRunTime             = 1.5f;
        public LayerMask WallLayer = ~0;

        [Header("Slope Surfing")]
        public float MaxSlopeAngle      = 35f;  // max angle to be considered "floor"
        public float SurfSlopeAccel      = 50f; // extra accel down slope when surfing
        public float SurfGravityMultiplier = 1.5f; // gravity boost on slopes

        [Header("Slam")]
        public float SlamForce = -45f;

        [Header("Crouch & Slide")]
        public float CrouchHeight      = 1.0f;
        public float StandHeight       = 2.0f;
        public float CrouchSpeed       = 8f;
        public float SlideForce        = 28f;
        public float SlideDuration     = 0.8f;
        public float SlideFriction     = 8f;
        public float SlideMinSpeed     = 4f;
        public float SlideCooldown     = 0.4f;

        [Header("Air Control")]
        public float AirDirectionChangeSpeed = 15f;
        public bool  AirControlRequiresInput = true;
        public float MaxAirSpeed             = 30f;    // normal max speed
        public float MaxAirStrafeSpeed       = 60f;    // can build this via strafing
        public float AirStrafeAcceleration   = 25f;    // extra accel perpendicular to velocity

        [Header("Camera")]
        public float MouseSensitivity   = 0.15f;
        public float WallRunCameraTilt  = 5f;
        public float CameraTiltSpeed    = 12f;
        public float CameraCrouchSpeed  = 14f;
    }
}
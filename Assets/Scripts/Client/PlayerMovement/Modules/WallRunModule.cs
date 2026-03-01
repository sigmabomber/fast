using UnityEngine;

namespace PlayerMovement
{
    public class WallRunModule
    {
        private MoveConfig _cfg;
        private Transform _body;
        private CharacterController _cc;
        
        public bool IsOnLeftWall { get; private set; }
        public bool IsOnRightWall { get; private set; }
        public RaycastHit LeftWallHit { get; private set; }
        public RaycastHit RightWallHit { get; private set; }

        public Vector3 GetCurrentWallNormal()
        {
            if (IsOnLeftWall) return LeftWallHit.normal;
            if (IsOnRightWall) return RightWallHit.normal;
            return Vector3.zero;
        }

        public void Initialise(MoveConfig cfg, Transform body, CharacterController cc)
        {
            _cfg = cfg;
            _body = body;
            _cc = cc;
        }

        public void CheckWalls()
        {
            RaycastHit lh, rh;
            // cast from approximately mid-body rather than the root position
            Vector3 origin = _body.position + Vector3.up * (_cc.height * 0.5f);

            // draw debug rays so the designer can see where we're checking
            Debug.DrawRay(origin, -_body.right * _cfg.WallCheckDistance, Color.cyan);
            Debug.DrawRay(origin,  _body.right * _cfg.WallCheckDistance, Color.cyan);

            IsOnLeftWall = Physics.Raycast(origin, -_body.right,
                                out lh, _cfg.WallCheckDistance, _cfg.WallLayer);
            IsOnRightWall = Physics.Raycast(origin, _body.right,
                                out rh, _cfg.WallCheckDistance, _cfg.WallLayer);
            LeftWallHit = lh;
            RightWallHit = rh;
        }

        public void Simulate(ref PlayerState s, PlayerInput inp, Vector3 forward, bool isGrounded)
        {
            // Wallrun ends immediately if grounded
            if (isGrounded)
            {
                s.Flags &= ~StateFlags.IsWallRunning;
                s.Flags &= ~StateFlags.IsOnLeftWall;
                s.Flags &= ~StateFlags.IsOnRightWall;
                return;
            }

            bool canWallRun = (IsOnLeftWall || IsOnRightWall) && !isGrounded;

            if (canWallRun)
            {
                if (!s.Flags.HasFlag(StateFlags.IsWallRunning))
                {
                    s.WallRunTimer = _cfg.WallRunTime;
                    s.JumpsRemaining = 1;
                }
                
                s.Flags |= StateFlags.IsWallRunning;
                
                if (IsOnLeftWall)
                {
                    s.Flags |= StateFlags.IsOnLeftWall;
                    s.Flags &= ~StateFlags.IsOnRightWall;
                }
                else if (IsOnRightWall)
                {
                    s.Flags |= StateFlags.IsOnRightWall;
                    s.Flags &= ~StateFlags.IsOnLeftWall;
                }
                else
                {
                    s.Flags &= ~StateFlags.IsOnLeftWall;
                    s.Flags &= ~StateFlags.IsOnRightWall;
                }

                s.WallRunTimer -= inp.DeltaTime;
                if (s.WallRunTimer <= 0f)
                {
                    s.Flags &= ~StateFlags.IsWallRunning;
                    s.Flags &= ~StateFlags.IsOnLeftWall;
                    s.Flags &= ~StateFlags.IsOnRightWall;
                    return;
                }

                // Calculate direction along the wall, perpendicular to the normal
                Vector3 wallNormal = IsOnLeftWall ? LeftWallHit.normal : RightWallHit.normal;
                Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up).normalized;
                
                // Ensure wallForward points in a sensible direction relative to movement
                if (Vector3.Dot(wallForward, forward) < 0f)
                    wallForward = -wallForward;

                s.Velocity.x = wallForward.x * _cfg.WallRunSpeed;
                s.Velocity.z = wallForward.z * _cfg.WallRunSpeed;
                // Faster lerp to gravity so the downward slide is pronounced
                s.Velocity.y = Mathf.Lerp(s.Velocity.y, _cfg.WallRunGravity, inp.DeltaTime * 12f);
            }
            else
            {
                s.Flags &= ~StateFlags.IsWallRunning;
                s.Flags &= ~StateFlags.IsOnLeftWall;
                s.Flags &= ~StateFlags.IsOnRightWall;
            }
        }

        public bool TryWallJump(ref PlayerState s)
        {
            if (!s.Flags.HasFlag(StateFlags.IsWallRunning)) return false;

            Vector3 wallNormal = IsOnLeftWall ? LeftWallHit.normal : RightWallHit.normal;
            s.Velocity = wallNormal * _cfg.WallRunJumpSideForce;
            s.Velocity.y = _cfg.WallRunJumpForce;
            s.Flags &= ~StateFlags.IsWallRunning;
            return true;
        }
    }
}
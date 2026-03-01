using System;
using UnityEngine;
using FishNet.Serializing;

namespace PlayerMovement
{
    [Serializable]
    public struct PlayerInput
    {
        public uint    Tick;
        public float   DeltaTime;
        public Vector2 Move;
        public Vector2 Look;
        public bool    Sprint;
        public bool    Crouch;
        public bool    JumpPressed;   // one-shot: true only on press tick
        public bool    JumpHeld;      // hold state: for gravity curve
        public bool    DashPressed;
        public bool    SlamPressed;
        public bool    CrouchJustPressed; // for slide trigger
    }

    [Serializable]
    public struct PlayerState
    {
        public uint       Tick;
        public Vector3    Position;
        public float      YawDegrees;
        public Vector3    Velocity;
        public StateFlags Flags;

        // Grounded history for landing detection
        public bool    WasGrounded;

        // Jump / bhop
        public int     JumpsRemaining;
        public float   CoyoteTimer;
        public float   JumpBufferTimer;
        public float   BhopSpeedMult;

        // Dash
        public float   DashCooldown;
        public float   DashTimer;
        public Vector3 DashDirection;

        // Wall run
        public float   WallRunTimer;

        // Helpers
        public bool IsGrounded
        {
            get => Flags.HasFlag(StateFlags.IsGrounded);
            set => Flags = value
                ? (Flags | StateFlags.IsGrounded)
                : (Flags & ~StateFlags.IsGrounded);
        }
    }

    [Flags]
    public enum StateFlags : byte
    {
        None         = 0,
        IsGrounded   = 1 << 0,
        IsWallRunning= 1 << 1,
        IsOnLeftWall = 1 << 2,
        IsOnRightWall= 1 << 3,
        IsDashing    = 1 << 4,
        IsSlamming   = 1 << 5,
        IsCrouching  = 1 << 6,
        IsSliding    = 1 << 7,
    }

    [Serializable]
    public struct ServerHitResult
    {
        public uint   Tick;
        public int    ShooterObjId;
        public int    TargetObjId;
        public float  Damage;
        public bool   WasValid;
        public string RejectReason;
    }

    [Serializable]
    public struct TickSnapshot
    {
        public uint    Tick;
        public Vector3 Position;
        public float   YawDegrees;
        public Bounds  Hitbox;
    }

    public static class NetSerializer
    {
        public static void WritePlayerInput(this Writer w, PlayerInput v)
        {
            w.WriteUInt32(v.Tick);
            w.WriteSingle(v.DeltaTime);
            w.WriteVector2(v.Move);
            w.WriteVector2(v.Look);
            byte b = 0;
            if (v.Sprint)            b |= 1;
            if (v.Crouch)            b |= 2;
            if (v.JumpPressed)       b |= 4;
            if (v.JumpHeld)          b |= 8;
            if (v.DashPressed)       b |= 16;
            if (v.SlamPressed)       b |= 32;
            if (v.CrouchJustPressed) b |= 64;
            w.WriteByte(b);
        }
        public static PlayerInput ReadPlayerInput(this Reader r)
        {
            var v = new PlayerInput();
            v.Tick      = r.ReadUInt32();
            v.DeltaTime = r.ReadSingle();
            v.Move      = r.ReadVector2();
            v.Look      = r.ReadVector2();
            byte b = r.ReadByte();
            v.Sprint            = (b & 1)  != 0;
            v.Crouch            = (b & 2)  != 0;
            v.JumpPressed       = (b & 4)  != 0;
            v.JumpHeld          = (b & 8)  != 0;
            v.DashPressed       = (b & 16) != 0;
            v.SlamPressed       = (b & 32) != 0;
            v.CrouchJustPressed = (b & 64) != 0;
            return v;
        }

        public static void WritePlayerState(this Writer w, PlayerState s)
        {
            w.WriteUInt32(s.Tick);
            w.WriteVector3(s.Position);
            w.WriteSingle(s.YawDegrees);
            w.WriteVector3(s.Velocity);
            w.WriteByte((byte)s.Flags);
            w.WriteBoolean(s.WasGrounded);
            w.WriteInt32(s.JumpsRemaining);
            w.WriteSingle(s.CoyoteTimer);
            w.WriteSingle(s.JumpBufferTimer);
            w.WriteSingle(s.BhopSpeedMult);
            w.WriteSingle(s.DashCooldown);
            w.WriteSingle(s.DashTimer);
            w.WriteVector3(s.DashDirection);
            w.WriteSingle(s.WallRunTimer);
        }
        public static PlayerState ReadPlayerState(this Reader r)
        {
            var s = new PlayerState();
            s.Tick              = r.ReadUInt32();
            s.Position          = r.ReadVector3();
            s.YawDegrees        = r.ReadSingle();
            s.Velocity          = r.ReadVector3();
            s.Flags             = (StateFlags)r.ReadByte();
            s.WasGrounded       = r.ReadBoolean();
            s.JumpsRemaining    = r.ReadInt32();
            s.CoyoteTimer       = r.ReadSingle();
            s.JumpBufferTimer   = r.ReadSingle();
            s.BhopSpeedMult     = r.ReadSingle();
            s.DashCooldown      = r.ReadSingle();
            s.DashTimer         = r.ReadSingle();
            s.DashDirection     = r.ReadVector3();
            s.WallRunTimer      = r.ReadSingle();
            return s;
        }
    }
}
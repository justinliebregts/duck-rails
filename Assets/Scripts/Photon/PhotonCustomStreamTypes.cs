using System;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using Game.Animation;
using UnityEngine;

/// <summary>
/// PUN custom type — wire format: head as world floats, hands as short
/// offsets from the head, rotations as euler angles quantized to shorts.
/// 36-byte pose plus 3 bytes of finger curl. Six fingers, 3 bits each.
/// Passengers append a 24-byte cabin snapshot (arrival, landing index, position, rotation).
/// In memory it's a plain runtime pose; quantization exists only inside Serialize/Deserialize.
/// </summary>
public struct PosePacket
{
    public Vector3 HeadPos, RHandPos, LHandPos;
    public Quaternion HeadRot, RHandRot, LHandRot;
    public FingerCurl Fingers;
    // Optional 24-byte cabin snapshot. Keep the base pose in world space for
    // unmatched/expired arrivals; playback derives cabin-relative poses on receive.
    public int CabinArrival, CabinLanding;
    public Vector3 CabinPosition;
    public Quaternion CabinRotation;

    const int PacketBytes = 39;
    const int PassengerPacketBytes = PacketBytes + 24;

    // ---- wire constants (single tuning point) ----
    public const byte TypeCode = 255;          // same code your old game used — freeze it once shipped
    const float OffsetRange = 2f;              // max |hand − head| meters (real max ≈ 1.2)
    const float DegPerShort = 180f / 32767f;   // ≈ 0.0055° per step
    const float ShortPerDeg = 32767f / 180f;

    public static PosePacket Capture(Transform head, Transform lHand, Transform rHand, FingerCurl fingers) => new PosePacket
    {
        HeadPos  = head.position,  HeadRot  = head.rotation,
        LHandPos = lHand.position, LHandRot = lHand.rotation,
        RHandPos = rHand.position, RHandRot = rHand.rotation,
        Fingers  = fingers,
    };

    public BodyPose ToBodyPose() => new BodyPose
    {
        headPos = HeadPos, headRot = HeadRot,
        lHandPos = LHandPos, lHandRot = LHandRot,
        rHandPos = RHandPos, rHandRot = RHandRot,
        fingers = Fingers,
    };

    // ---------------- codec ----------------
    public static byte[] Serialize(object o)
    {
        var p = (PosePacket)o;
        var b = new byte[p.CabinArrival > 0 ? PassengerPacketBytes : PacketBytes];
        Encode(p, b);
        return b;
    }

    static void Encode(PosePacket p, byte[] b)
    {
        int i = 0;
        WriteFloat3(b, ref i, p.HeadPos);
        WriteOffset(b, ref i, p.RHandPos - p.HeadPos);
        WriteOffset(b, ref i, p.LHandPos - p.HeadPos);
        WriteQuat(b, ref i, p.HeadRot);
        WriteQuat(b, ref i, p.RHandRot);
        WriteQuat(b, ref i, p.LHandRot);
        WriteFingers(b, ref i, p.Fingers);
        if (p.CabinArrival > 0)
        {
            WriteInt(b, ref i, p.CabinArrival);
            WriteInt(b, ref i, p.CabinLanding);
            WriteFloat3(b, ref i, p.CabinPosition);
            WriteQuat(b, ref i, p.CabinRotation);
        }
    }

    static readonly byte[] CodecBuffer = new byte[PassengerPacketBytes];
    static readonly Unity.Profiling.ProfilerMarker CodecMarker = new Unity.Profiling.ProfilerMarker("Pose.Codec");
    public static short SerializeStream(StreamBuffer output, object value)
    {
        using var sample = CodecMarker.Auto();
        var packet = (PosePacket)value;
        short length = (short)(packet.CabinArrival > 0 ? PassengerPacketBytes : PacketBytes);
        lock (CodecBuffer)
        {
            Encode(packet, CodecBuffer);
            output.Write(CodecBuffer, 0, length);
        }
        return length;
    }
    public static object DeserializeStream(StreamBuffer input, short length)
    {
        using var sample = CodecMarker.Auto();
        if (length != PacketBytes && length != PassengerPacketBytes) throw new ArgumentException("Invalid pose packet length");
        lock (CodecBuffer)
        {
            if (input.Read(CodecBuffer, 0, length) != length) throw new ArgumentException("Truncated pose packet");
            return Decode(CodecBuffer, length);
        }
    }

    public static object Deserialize(byte[] data) => Decode(data, data.Length);

    static object Decode(byte[] data, int length)
    {
        if (length != PacketBytes && length != PassengerPacketBytes)
        {
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write($"PosePacket: expected {PacketBytes} bytes, got {data.Length} — client version mismatch?");
            }
            return new PosePacket { HeadRot = Quaternion.identity };
        }
        int i = 0;
        var p = new PosePacket();
        p.HeadPos  = ReadFloat3(data, ref i);
        p.RHandPos = p.HeadPos + ReadOffset(data, ref i);
        p.LHandPos = p.HeadPos + ReadOffset(data, ref i);
        p.HeadRot  = ReadQuat(data, ref i);
        p.RHandRot = ReadQuat(data, ref i);
        p.LHandRot = ReadQuat(data, ref i);
        p.Fingers  = ReadFingers(data, ref i);
        if (length == PassengerPacketBytes)
        {
            p.CabinArrival = ReadInt(data, ref i);
            p.CabinLanding = ReadInt(data, ref i);
            p.CabinPosition = ReadFloat3(data, ref i);
            p.CabinRotation = ReadQuat(data, ref i);
        }
        return p;
    }

    // ---------------- primitives (little-endian, matches every platform we ship) ----------------
    static void WriteInt(byte[] b, ref int i, int value)
    {
        b[i++] = (byte)value; b[i++] = (byte)(value >> 8);
        b[i++] = (byte)(value >> 16); b[i++] = (byte)(value >> 24);
    }
    static int ReadInt(byte[] b, ref int i)
    {
        int value = b[i] | b[i + 1] << 8 | b[i + 2] << 16 | b[i + 3] << 24;
        i += 4;
        return value;
    }
    [StructLayout(LayoutKind.Explicit)]
    struct FloatIntUnion { [FieldOffset(0)] public float F; [FieldOffset(0)] public int I; }

    internal static void WriteFloat(byte[] b, ref int i, float v)
    {
        var u = new FloatIntUnion { F = v };
        b[i] = (byte)u.I; b[i+1] = (byte)(u.I >> 8); b[i+2] = (byte)(u.I >> 16); b[i+3] = (byte)(u.I >> 24);
        i += 4;
    }
    internal static float ReadFloat(byte[] b, ref int i)
    {
        var u = new FloatIntUnion { I = b[i] | (b[i+1] << 8) | (b[i+2] << 16) | (b[i+3] << 24) };
        i += 4; return u.F;
    }
    internal static void WriteShort(byte[] b, ref int i, short v) { b[i] = (byte)v; b[i+1] = (byte)(v >> 8); i += 2; }
    internal static short ReadShort(byte[] b, ref int i) { short v = (short)(b[i] | (b[i+1] << 8)); i += 2; return v; }

    static void WriteFloat3(byte[] b, ref int i, Vector3 v) { WriteFloat(b, ref i, v.x); WriteFloat(b, ref i, v.y); WriteFloat(b, ref i, v.z); }
    static Vector3 ReadFloat3(byte[] b, ref int i) { float x = ReadFloat(b, ref i), y = ReadFloat(b, ref i), z = ReadFloat(b, ref i); return new Vector3(x, y, z); }

    static void WriteOffset(byte[] b, ref int i, Vector3 o)
    {
        WriteShort(b, ref i, QuantAxis(o.x));
        WriteShort(b, ref i, QuantAxis(o.y));
        WriteShort(b, ref i, QuantAxis(o.z));
    }
    static Vector3 ReadOffset(byte[] b, ref int i)
    {
        short x = ReadShort(b, ref i), y = ReadShort(b, ref i), z = ReadShort(b, ref i);
        return new Vector3(x, y, z) * (OffsetRange / short.MaxValue);
    }
    static void WriteEuler(byte[] b, ref int i, Vector3 e)
    {
        WriteShort(b, ref i, QuantAngle(e.x));
        WriteShort(b, ref i, QuantAngle(e.y));
        WriteShort(b, ref i, QuantAngle(e.z));
    }
    static Vector3 ReadEuler(byte[] b, ref int i)
    {
        short x = ReadShort(b, ref i), y = ReadShort(b, ref i), z = ReadShort(b, ref i);
        return new Vector3(x, y, z) * DegPerShort;
    }

    const int FingerSteps = 7;

    static int QuantFinger(float v) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * FingerSteps), 0, FingerSteps);
    static float DequantFinger(int level) => level / (float)FingerSteps;

    static void WriteFingers(byte[] b, ref int i, FingerCurl f)
    {
        int packed = QuantFinger(f.rightThumb)
            | (QuantFinger(f.rightIndex) << 3)
            | (QuantFinger(f.rightPinky) << 6)
            | (QuantFinger(f.leftThumb) << 9)
            | (QuantFinger(f.leftIndex) << 12)
            | (QuantFinger(f.leftPinky) << 15);
        b[i] = (byte)packed;
        b[i + 1] = (byte)(packed >> 8);
        b[i + 2] = (byte)(packed >> 16);
        i += 3;
    }

    static FingerCurl ReadFingers(byte[] b, ref int i)
    {
        int packed = b[i] | (b[i + 1] << 8) | (b[i + 2] << 16);
        i += 3;
        return new FingerCurl
        {
            rightThumb = DequantFinger(packed & 7),
            rightIndex = DequantFinger((packed >> 3) & 7),
            rightPinky = DequantFinger((packed >> 6) & 7),
            leftThumb = DequantFinger((packed >> 9) & 7),
            leftIndex = DequantFinger((packed >> 12) & 7),
            leftPinky = DequantFinger((packed >> 15) & 7),
        };
    }

    static short QuantAxis(float v) => (short)Mathf.Round(Mathf.Clamp(v, -OffsetRange, OffsetRange) * (short.MaxValue / OffsetRange));
    internal static short QuantAngle(float deg)
    {
        deg = Mathf.Repeat(deg + 180f, 360f) - 180f;   // wrap to [-180, 180)
        return (short)Mathf.Round(deg * ShortPerDeg);
    }

    // ---- quaternion codec: smallest-three, 2-bit index + 3x10-bit = 4 bytes ----
    // ~0.14° worst-case error, ~0.04° typical. No singularities, no trig.
    const float SqrtHalf = 0.70710678f;
    const float U10ToComp = SqrtHalf / 511.5f;    // dequant scale
    const float CompToU10 = 511.5f / SqrtHalf;    // quantize scale

    static uint QuantComp(float v)
    {
        int u = Mathf.RoundToInt((v * CompToU10 + 511.5f));
        return (uint)Mathf.Clamp(u, 0, 1023);
    }
    static float DequantComp(uint u) => (u - 511.5f) * U10ToComp;

    internal static void WriteQuat(byte[] b, ref int i, Quaternion q)
    {
        float x = q.x, y = q.y, z = q.z, w = q.w;
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y), az = Mathf.Abs(z), aw = Mathf.Abs(w);

        int m; float largest;
        if (aw >= ax && aw >= ay && aw >= az) { m = 3; largest = w; }
        else if (ax >= ay && ax >= az)        { m = 0; largest = x; }
        else if (ay >= az)                    { m = 1; largest = y; }
        else                                  { m = 2; largest = z; }

        if (largest < 0f) { x = -x; y = -y; z = -z; w = -w; }  // canonicalize q vs -q

        uint packed = (uint)m << 30;   // then the three non-m components, x→w order
        int shift = 20;
        if (m != 0) { packed |= QuantComp(x) << shift; shift -= 10; }
        if (m != 1) { packed |= QuantComp(y) << shift; shift -= 10; }
        if (m != 2) { packed |= QuantComp(z) << shift; shift -= 10; }
        if (m != 3) { packed |= QuantComp(w) << shift; }

        b[i] = (byte)packed; b[i+1] = (byte)(packed >> 8); b[i+2] = (byte)(packed >> 16); b[i+3] = (byte)(packed >> 24);
        i += 4;
    }

    internal static Quaternion ReadQuat(byte[] b, ref int i)
    {
        uint packed = (uint)(b[i] | (b[i+1] << 8) | (b[i+2] << 16) | (b[i+3] << 24)); i += 4;
        int m = (int)(packed >> 30) & 3;
        float c0 = DequantComp((packed >> 20) & 1023);
        float c1 = DequantComp((packed >> 10) & 1023);
        float c2 = DequantComp(packed & 1023);
        float d  = Mathf.Sqrt(Mathf.Max(0f, 1f - c0*c0 - c1*c1 - c2*c2));

        switch (m)
        {
            case 0:  return new Quaternion( d, c0, c1, c2);
            case 1:  return new Quaternion(c0,  d, c1, c2);
            case 2:  return new Quaternion(c0, c1,  d, c2);
            default: return new Quaternion(c0, c1, c2,  d);
        }
    }
}

/// <summary>
/// Fixed 21-byte enemy pose. Position is 3 floats, rotation is the same 4-byte
/// smallest-three quat as <see cref="PosePacket"/>, then one flags byte and
/// aim yaw/pitch as shorts. Senders ping-pong two instances so UnreliableOnChange
/// can compare the previous sample. Receives come from a pool because PUN
/// deserializes the whole batch before callbacks.
/// </summary>
public sealed class EnemyPosePacket
{
    public const byte TypeCode = 254;
    public const int PacketBytes = 21;
    public const float AimDistance = 4f;
    public const byte AimFlag = 4;
    public const byte JumpFlag = 8;
    public const byte KnockFlag = 16;

    const float DegPerShort = 180f / 32767f;

    public Vector3 Position;
    public Quaternion Rotation = Quaternion.identity;
    public byte Flags;
    public float AimYaw;
    public float AimPitch;

    public byte State => (byte)(Flags & 3);
    public bool HasAim => (Flags & AimFlag) != 0;
    public bool Knocked => (Flags & KnockFlag) != 0;

    public override bool Equals(object other)
    {
        EnemyPosePacket packet = other as EnemyPosePacket;
        if (packet == null) return false;
        return Position == packet.Position
            && Rotation == packet.Rotation
            && Flags == packet.Flags
            && AimYaw == packet.AimYaw
            && AimPitch == packet.AimPitch;
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode() ^ (Rotation.GetHashCode() << 2) ^ Flags;
    }

    readonly byte[] _wire = new byte[PacketBytes];

    static readonly EnemyPosePacket[] Pool = new EnemyPosePacket[8];
    static int _pooled;

    public void Set(Vector3 position, Quaternion rotation, byte state, bool aim, float aimYaw, float aimPitch)
    {
        Position = position;
        Rotation = rotation;
        Flags = (byte)((state & 3) | (aim ? AimFlag : 0));
        AimYaw = aimYaw;
        AimPitch = aimPitch;
    }

    public Vector3 AimPoint(Vector3 origin)
    {
        float yaw = AimYaw * Mathf.Deg2Rad;
        float pitch = AimPitch * Mathf.Deg2Rad;
        float cp = Mathf.Cos(pitch);
        Vector3 dir = new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp);
        return origin + dir * AimDistance;
    }

    public static void AimAngles(Vector3 origin, Vector3 point, out float yaw, out float pitch)
    {
        Vector3 dir = point - origin;
        float horiz = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        pitch = Mathf.Atan2(dir.y, horiz) * Mathf.Rad2Deg;
    }

    public static byte[] Serialize(object o)
    {
        EnemyPosePacket p = (EnemyPosePacket)o;
        byte[] b = p._wire;
        int i = 0;
        PosePacket.WriteFloat(b, ref i, p.Position.x);
        PosePacket.WriteFloat(b, ref i, p.Position.y);
        PosePacket.WriteFloat(b, ref i, p.Position.z);
        PosePacket.WriteQuat(b, ref i, p.Rotation);
        b[i++] = p.Flags;
        PosePacket.WriteShort(b, ref i, PosePacket.QuantAngle(p.AimYaw));
        PosePacket.WriteShort(b, ref i, PosePacket.QuantAngle(p.AimPitch));
        return b;
    }

    public static object Deserialize(byte[] data)
    {
        EnemyPosePacket p = Rent();
        if (data == null || data.Length != PacketBytes)
        {
            {
                if (DuckzLog.AllowError(out var logNotice2))
                    logNotice2.Write($"EnemyPosePacket: expected {PacketBytes} bytes, got {(data == null ? 0 : data.Length)}.");
            }
            p.Position = default;
            p.Rotation = Quaternion.identity;
            p.Flags = 0;
            p.AimYaw = 0f;
            p.AimPitch = 0f;
            return p;
        }

        int i = 0;
        float x = PosePacket.ReadFloat(data, ref i);
        float y = PosePacket.ReadFloat(data, ref i);
        float z = PosePacket.ReadFloat(data, ref i);
        p.Position = new Vector3(x, y, z);
        p.Rotation = PosePacket.ReadQuat(data, ref i);
        p.Flags = data[i++];
        p.AimYaw = PosePacket.ReadShort(data, ref i) * DegPerShort;
        p.AimPitch = PosePacket.ReadShort(data, ref i) * DegPerShort;
        return p;
    }

    public static void Return(EnemyPosePacket packet)
    {
        if (packet == null || _pooled >= Pool.Length) return;
        Pool[_pooled++] = packet;
    }

    static EnemyPosePacket Rent()
    {
        if (_pooled > 0) return Pool[--_pooled];
        return new EnemyPosePacket();
    }
}

/// <summary>Registers the custom type before any connection can possibly happen.</summary>
public static class PhotonCustomTypes
{
    static bool _done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        if (_done) return; _done = true;
        PhotonPeer.RegisterType(typeof(PosePacket), PosePacket.TypeCode,
                                PosePacket.SerializeStream, PosePacket.DeserializeStream);
        PhotonPeer.RegisterType(typeof(EnemyPosePacket), EnemyPosePacket.TypeCode,
                                EnemyPosePacket.Serialize, EnemyPosePacket.Deserialize);
    }
}

using System;
using Unity.Mathematics;
using UnityEngine;

public class GRMathUtils {
    public static float MapRange(float value, float inputStart, float inputEnd, float outputStart, float outputEnd) {
      float result = outputStart + ((outputEnd - outputStart) / (inputEnd - inputStart)) * (value - inputStart);
      return result;
    }

    public static bool ReadBool(ref byte[] buffer, ref int bufferIndex) {
  		bool result = BitConverter.ToBoolean(buffer, bufferIndex);
  		bufferIndex += sizeof(bool);
  		return result;
  	}

  	public static byte ReadByte(ref byte[] buffer, ref int bufferIndex) {
  		byte result = (byte)buffer[bufferIndex];
  		bufferIndex += sizeof(byte);
  		return result;
  	}

    public static short ReadS16(ref byte[] buffer, ref int bufferIndex) {
  		short result = BitConverter.ToInt16(buffer, bufferIndex);
  		bufferIndex += sizeof(short);
  		return result;
  	}

    public static int ReadS32(ref byte[] buffer, ref int bufferIndex) {
  		int result = BitConverter.ToInt32(buffer, bufferIndex);
  		bufferIndex += sizeof(int);
  		return result;
  	}

    public static float ReadFloat(ref byte[] buffer, ref int bufferIndex) {
      float result;
      result = BitConverter.ToSingle(buffer, bufferIndex);
      bufferIndex += 4;
      return result;
    }

    public static Vector3 ReadVector3(ref byte[] buffer, ref int bufferIndex) {
      Vector3 result;
      result.x = BitConverter.ToSingle(buffer, bufferIndex);
      result.y = BitConverter.ToSingle(buffer, bufferIndex+4);
      result.z = BitConverter.ToSingle(buffer, bufferIndex+8);
      bufferIndex += 12;
      return result;
    }

    public static Quaternion ReadQuaternion(ref byte[] buffer, ref int bufferIndex) {
      Quaternion result;
      result.x = BitConverter.ToSingle(buffer, bufferIndex);
      result.y = BitConverter.ToSingle(buffer, bufferIndex+4);
      result.z = BitConverter.ToSingle(buffer, bufferIndex+8);
      result.w = BitConverter.ToSingle(buffer, bufferIndex+12);
      bufferIndex += 16;
      return result;
    }

    public static void WriteByte(byte b, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(b), 0, dest, bufferIndex, sizeof(byte));
      bufferIndex += sizeof(byte);
    }

    public static void WriteBool(bool b, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(b), 0, dest, bufferIndex, sizeof(bool));
      bufferIndex += sizeof(bool);
    }

    public static void WriteS16(short x, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(x), 0, dest, bufferIndex, sizeof(short));
      bufferIndex += sizeof(short);
    }

    public static void WriteS32(int x, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(x), 0, dest, bufferIndex, sizeof(int));
      bufferIndex += sizeof(int);
    }

    public static void WriteFloat(float x, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(x), 0, dest, bufferIndex, sizeof(float));
      bufferIndex += sizeof(float);
    }

    public static void WriteV3(Vector3 v, ref byte[] dest, ref int bufferIndex) {
      Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, dest, bufferIndex, sizeof(float));
      Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, dest, bufferIndex + sizeof(float), sizeof(float));
      Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, dest, bufferIndex + sizeof(float) * 2, sizeof(float));
      bufferIndex += sizeof(float)*3;
    }

  public const float angleToShort = 32767f / 360f;
  public const float shortToAngle = 360f / 32767f;
  public static void WriteEulerAngleAsShort(Vector3 angle, ref byte[] dest, ref int bufferIndex) {
    Buffer.BlockCopy(BitConverter.GetBytes((short)(angle.x * angleToShort)), 0, dest, bufferIndex, sizeof(short));
    Buffer.BlockCopy(BitConverter.GetBytes((short)(angle.y * angleToShort)), 0, dest, bufferIndex + sizeof(short), sizeof(short));
    Buffer.BlockCopy(BitConverter.GetBytes((short)(angle.z * angleToShort)), 0, dest, bufferIndex + sizeof(short) * 2, sizeof(short));
    bufferIndex += sizeof(short)*3;
  }

  public static void WriteV3AsShort(Vector3 v, ref byte[] dest, ref int bufferIndex) {
    Buffer.BlockCopy(BitConverter.GetBytes((short)(v.x * 1000f)), 0, dest, bufferIndex, sizeof(short));
    Buffer.BlockCopy(BitConverter.GetBytes((short)(v.y * 1000f)), 0, dest, bufferIndex + sizeof(short), sizeof(short));
    Buffer.BlockCopy(BitConverter.GetBytes((short)(v.z * 1000f)), 0, dest, bufferIndex + sizeof(short) * 2, sizeof(short));
    bufferIndex += sizeof(short)*3;
  }

  public static byte PackNibbles(byte low, byte high) {
    return (byte)((high << 4) | (low & 0x0F));
  }

  public static void UnpackNibbles(byte packed, ref byte low, ref byte high) {
    low  = (byte)(packed & 0x0F);
    high = (byte)(packed >> 4);
  }

  public static byte GetHighNibble(byte packed) {
    return (byte)(packed >> 4);
  }

  public static byte GetLowNibble(byte packed) {
    return (byte)(packed & 0x0F);
  }

  public static Vector3 ReadShortAsV3(ref byte[] buffer, ref int bufferIndex) {
    Vector3 result;
    result.x = BitConverter.ToInt16(buffer, bufferIndex) / 1000f;
    result.y = BitConverter.ToInt16(buffer, bufferIndex+2) / 1000f;
    result.z = BitConverter.ToInt16(buffer, bufferIndex+4) / 1000f;
    bufferIndex += 6;
    return result;
  }

  public static Vector3 ReadShortAsEulerAngle(ref byte[] buffer, ref int bufferIndex) {
    Vector3 result;
    result.x = BitConverter.ToInt16(buffer, bufferIndex) * shortToAngle;
    result.y = BitConverter.ToInt16(buffer, bufferIndex+2) * shortToAngle;
    result.z = BitConverter.ToInt16(buffer, bufferIndex+4) * shortToAngle;
    bufferIndex += 6;
    return result;
  }


  public static float MapRangeAndClamp(float value, float inputStart, float inputEnd, float outputStart, float outputEnd) {
    float result = outputStart + ((outputEnd - outputStart) / (inputEnd - inputStart)) * (value - inputStart);
    if(outputEnd > outputStart) {
      if(result > outputEnd) result = outputEnd;
      if(result < outputStart) result = outputStart;
    } else {
      if(result < outputEnd) result = outputEnd;
      if(result > outputStart) result = outputStart;
    }
    return result;
  }

  public static int StableStringHash(string s) {
    unchecked
    {
        int hash = 23;
        foreach (char c in s) {
            hash = hash * 31 + c;
        }
        return hash;
    }
  }

  public static Vector3 ClosestPointOnLine(Vector3 startLinePoint, Vector3 endLinePoint, Vector3 point) {
      // Vector3 vVector1 = point - startLinePoint;
      // Vector3 vVector2 = (endLinePoint - startLinePoint).normalized;

      // float distance = Vector3.Distance(startLinePoint, endLinePoint);
      // float dot = Vector3.Dot(vVector2, vVector1);

      // if (dot <= 0) return startLinePoint;
      // if (dot >= distance) return endLinePoint;

      // Vector3 vVector3 = vVector2 * dot;
      // Vector3 closestPoint = startLinePoint + vVector3;
      // return closestPoint;
      Vector3 ab = endLinePoint - startLinePoint;
      float t = Vector3.Dot(point - startLinePoint, ab) / Vector3.Dot(ab, ab);
      t = Mathf.Clamp01(t);
      return startLinePoint + ab * t;
  }

  public static Vector3 ClosestPointOnLine(Vector3 startLinePoint, Vector3 endLinePoint, Vector3 point, out float t)
  {
      Vector3 ab = endLinePoint - startLinePoint;
      t = Vector3.Dot(point - startLinePoint, ab) / Vector3.Dot(ab, ab);
      t = Mathf.Clamp01(t);
      return startLinePoint + ab * t;
  }

  public static Vector3 V3ComponentWiseMultiply(Vector3 a, Vector3 b) {
    return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
  }

  //p0 = start
  //p1 = control point
  //p2 = end
  public static Vector3 GetPointOnBezier (Vector3 p0, Vector3 p1, Vector3 p2, float t) {
		t = Mathf.Clamp01(t);
		float oneMinusT = 1f - t;
		return
			oneMinusT * oneMinusT * p0 +
			2f * oneMinusT * t * p1 +
			t * t * p2;
	}

  public static DecomposedQuaternion DecompQuat(Quaternion q, Vector3 direction) {
    DecomposedQuaternion result = new DecomposedQuaternion();
    Vector3 axis = new Vector3(q.x, q.y, q.z);
    Vector3 projection = Vector3.Project(axis, direction);
    result.twist = new Quaternion(projection.x, projection.y, projection.z, q.w).normalized;
    result.swing = q * Quaternion.Inverse(result.twist);
    return result;
  }

  public static quaternion FromTo(float3 from, float3 to)
    {
        float d = math.clamp(math.dot(from, to), -1f, 1f);
        if (d > 1f - 1e-6f) return quaternion.identity;
        if (d < -1f + 1e-6f) // exact 180°: axis undefined, pick any perpendicular
        {
            float3 axis = math.cross(from, new float3(1, 0, 0));
            if (math.lengthsq(axis) < 1e-8f) axis = math.cross(from, new float3(0, 1, 0));
            return quaternion.AxisAngle(math.normalize(axis), math.PI);
        }
        return math.normalize(new quaternion(new float4(math.cross(from, to), 1f + d)));
    }

}

public struct DecomposedQuaternion {
  public Quaternion twist;
  public Quaternion swing;
}

public static class PackingUtil {
  public static byte PackBoolsIntoByte(params bool[] bools) {
    byte result = 0;
    for(int i = 0; i < bools.Length; i++) {
      if(bools[i]) {
        result = (byte)(result | (1 << i));
      }
    }
    return result;
  }

  public static void PackBool8(this ref byte output, bool input, int bitPosition) {
    output = (byte)(output | ((input ? 1 : 0) << bitPosition));
  }

  public static void UnPackBool8(this byte input, ref bool output, int bitPosition) {
    output = (input & (1 << bitPosition)) != 0;
  }

  public static bool UnPackBool8Direct(this byte input, int bitPosition) {
    return ((input & (1 << bitPosition)) != 0);
  }

  public static void PackBool16(this ref short output, bool input, int bitPosition) {
    output = (short)(output | ((input ? 1 : 0) << bitPosition));
  }

  public static void UnPackBool16(this short input, ref bool output, int bitPosition) {
    output = (input & (1 << bitPosition)) != 0;
  }

  public static void PutV3InFloatArray(this Vector3 v3, ref float[] fArray, ref int index) {
    fArray[index++] = v3.x;
    fArray[index++] = v3.y;
    fArray[index++] = v3.z;
  }

  public static void PopulateV3FromFloatArray(this ref Vector3 v3, ref float[] fArray, int index) {
    v3.x = fArray[index];
    v3.y = fArray[index+1];
    v3.z = fArray[index+2];
  }
}
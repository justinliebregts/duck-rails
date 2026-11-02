using UnityEngine;

/// <summary>Reads joint bending, not pinch strength. Supports the SDK's OVR and OpenXR skeleton layouts.</summary>
public sealed class OvrHandCurlSource
{
    OVRPlugin.HandState state;
    OVRPlugin.Skeleton2 skeleton;
    readonly Vector3[] positions = new Vector3[26];
    readonly Quaternion[] rotations = new Quaternion[26];
    float lastThumb;

    public bool TryRead(bool left, out TrackedHandInput.Curls curls)
    {
        curls = default;
        if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render,
                left ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight, ref state)
            || (state.Status & OVRPlugin.HandStatus.HandTracked) == 0
            || (state.Status & OVRPlugin.HandStatus.SystemGestureInProgress) != 0
            || state.HandConfidence != OVRPlugin.TrackingConfidence.High) return false;
        // Low-confidence fingers must not create grip/trigger edges.
        if (state.FingerConfidences == null || state.FingerConfidences.Length < 5) return false;
        for (int i = 1; i < 5; i++)
            if (state.FingerConfidences[i] != OVRPlugin.TrackingConfidence.High) return false;

        bool xr = OVRPlugin.HandSkeletonVersion == OVRHandSkeletonVersion.OpenXR;
        if (xr)
        {
            if (state.BonePositions == null || state.BonePositions.Length < 26) return false;
            for (int i = 0; i < 26; i++) positions[i] = Vector(state.BonePositions[i]);
        }
        else
        {
            if (skeleton.Bones == null && !OVRPlugin.GetSkeleton2(left
                    ? OVRPlugin.SkeletonType.HandLeft : OVRPlugin.SkeletonType.HandRight, ref skeleton)) return false;
            if (!BuildLegacyPositions(skeleton, state.BoneRotations, positions, rotations)) return false;
        }
        if (!TryMeasure(positions, xr, out curls)) return false;
        if (state.FingerConfidences[0] == OVRPlugin.TrackingConfidence.High) lastThumb = curls.thumb;
        else curls.thumb = lastThumb;
        return true;
    }

    // Curl is invariant under the common coordinate conversion/root rotation, so stay in SDK space.
    public static bool BuildLegacyPositions(OVRPlugin.Skeleton2 bind, OVRPlugin.Quatf[] localRotations,
        Vector3[] points, Quaternion[] worldRotations)
    {
        const int count = (int)OVRPlugin.BoneId.Hand_End;
        if (bind.Bones == null || bind.NumBones < count || localRotations == null
            || localRotations.Length < (int)OVRPlugin.BoneId.Hand_MaxSkinnable
            || points.Length < count || worldRotations.Length < count) return false;
        for (int i = 0; i < count; i++)
        {
            var bone = bind.Bones[i];
            int parent = bone.ParentBoneIndex;
            if (parent >= i) return false;
            Vector3 p = Vector(bone.Pose.Position);
            var value = i < (int)OVRPlugin.BoneId.Hand_MaxSkinnable ? localRotations[i] : bone.Pose.Orientation;
            Quaternion q = new Quaternion(value.x, value.y, value.z, value.w);
            if (parent >= 0)
            {
                points[i] = points[parent] + worldRotations[parent] * p;
                worldRotations[i] = worldRotations[parent] * q;
            }
            else { points[i] = p; worldRotations[i] = q; }
        }
        return true;
    }

    public static bool TryMeasure(Vector3[] p, bool xr, out TrackedHandInput.Curls result)
    {
        result = default;
        if (p == null || p.Length < (xr ? 26 : 24)) return false;
        if (xr)
        {
            result.thumb = Bend(p[2], p[3], p[4], p[5], 120f);
            result.index = Bend(p[7], p[8], p[9], p[10], 160f);
            result.middle = Bend(p[12], p[13], p[14], p[15], 160f);
            result.ring = Bend(p[17], p[18], p[19], p[20], 160f);
            result.pinky = Bend(p[22], p[23], p[24], p[25], 160f);
        }
        else
        {
            result.thumb = Bend(p[3], p[4], p[5], p[19], 120f);
            result.index = Bend(p[6], p[7], p[8], p[20], 160f);
            result.middle = Bend(p[9], p[10], p[11], p[21], 160f);
            result.ring = Bend(p[12], p[13], p[14], p[22], 160f);
            result.pinky = Bend(p[16], p[17], p[18], p[23], 160f);
        }
        return Finite(result.thumb) && Finite(result.index) && Finite(result.middle)
            && Finite(result.ring) && Finite(result.pinky);
    }

    static float Bend(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float closedAngle)
    {
        Vector3 ab = b - a, bc = c - b, cd = d - c;
        if (ab.sqrMagnitude < 1e-10f || bc.sqrMagnitude < 1e-10f || cd.sqrMagnitude < 1e-10f
            || !Finite(ab.sqrMagnitude) || !Finite(bc.sqrMagnitude) || !Finite(cd.sqrMagnitude)) return float.NaN;
        return Mathf.Clamp01((Vector3.Angle(ab, bc) + Vector3.Angle(bc, cd)) / closedAngle);
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static Vector3 Vector(OVRPlugin.Vector3f value) => new Vector3(value.x, value.y, value.z);
}

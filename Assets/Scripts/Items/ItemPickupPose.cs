using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Maps a display mesh back to the real prefab's root, including model child offsets.
public struct ItemPickupPose
{
    static readonly List<MeshFilter> displayMeshes = new List<MeshFilter>(8);
    static readonly List<MeshFilter> sourceMeshes = new List<MeshFilter>(8);

    public Vector3 position;
    public Quaternion rotation;
    public Vector3 startScale;
    public Vector3 fullScale;
    public Vector3 localGrip;

    public static ItemPickupPose Capture(GameObject prefab, Transform display, Vector3 gripPosition)
    {
        Matrix4x4 pose = display.localToWorldMatrix;
        display.GetComponentsInChildren(true, displayMeshes);
        Grabbable item = prefab.GetComponent<Grabbable>();
        GrabSurfaceData data = item != null ? item.GrabSurface : null;
        if (data != null)
        {
            foreach (MeshFilter target in displayMeshes)
            {
                if (target.sharedMesh != data.ReferenceMesh) continue;
                pose = target.transform.localToWorldMatrix * data.ReferenceToRoot.inverse;
                displayMeshes.Clear();
                return FromMatrix(prefab, pose, gripPosition);
            }
        }
        else
        {
            prefab.GetComponentsInChildren(true, sourceMeshes);
            foreach (MeshFilter source in sourceMeshes)
            {
                if (source.sharedMesh == null) continue;
                foreach (MeshFilter target in displayMeshes)
                {
                    if (target.sharedMesh != source.sharedMesh) continue;
                    Matrix4x4 model = prefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    pose = target.transform.localToWorldMatrix * model.inverse;
                    sourceMeshes.Clear();
                    displayMeshes.Clear();
                    return FromMatrix(prefab, pose, gripPosition);
                }
            }
            sourceMeshes.Clear();
        }
        displayMeshes.Clear();
        // Custom miniature meshes should use the same item-local axes as the prefab.
        return FromMatrix(prefab, pose, gripPosition);
    }

    static ItemPickupPose FromMatrix(GameObject prefab, Matrix4x4 pose, Vector3 gripPosition)
    {
        var result = new ItemPickupPose
        {
            rotation = pose.rotation,
            startScale = pose.lossyScale,
            fullScale = prefab.transform.localScale,
            localGrip = pose.inverse.MultiplyPoint3x4(gripPosition)
        };
        // Spawn at the final root pose; growth offsets it around the same grabbed point.
        result.position = gripPosition - result.rotation * Vector3.Scale(result.fullScale, result.localGrip);
        return result;
    }

    // Resolve against the shared rendered surface before spawning/networking.
    // Use the display's scale for nearest-point distance; retain item-local contact during growth.
    public bool ClampGripToSurface(GameObject prefab, Vector3 gripPosition)
    {
        Grabbable item = prefab.GetComponent<Grabbable>();
        GrabSurfaceData data = item != null ? item.GrabSurface : null;
        if (data == null) return false;
        Matrix4x4 displayShape = Matrix4x4.TRS(Vector3.zero, rotation, startScale);
        Vector3 query = displayShape.MultiplyPoint3x4(localGrip);
        if (!data.TryClosestPoint(displayShape, query, out Vector3 closest)) return false;
        localGrip = displayShape.inverse.MultiplyPoint3x4(closest);
        position = gripPosition - rotation * Vector3.Scale(fullScale, localGrip);
        return true;
    }

    public Grabbable Spawn(GameObject prefab, PlayerHand hand)
    {
        bool networkGrabbable = prefab.GetComponent<PhotonGrabbable>() != null;
        double started = networkGrabbable && PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
        GameObject go;
        if (PhotonNetwork.InRoom && networkGrabbable)
        {
            PhotonPreparePool.Register(prefab);
            PhotonPlayer player = PhotonPlayer.Local;
            Transform source = player != null ? player.HandSource(hand.IsControllerLTouch) : null;
            if (source == null) source = hand.transform;
            Quaternion inverse = Quaternion.Inverse(source.rotation);
            go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, position, rotation, 0,
                new object[] { "pickup-pose-v1", startScale, fullScale, localGrip, started,
                    PhotonNetwork.LocalPlayer.ActorNumber, hand.IsControllerLTouch,
                    inverse * (position - source.position), inverse * rotation });
        }
        else
        {
            go = Object.Instantiate(prefab, position, rotation);
            if (go != null && go.TryGetComponent(out Grabbable local))
                local.BeginPickupGrowth(startScale, fullScale, localGrip, started);
        }
        return go != null ? go.GetComponent<Grabbable>() : null;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visual-only prop: one mesh, no camera/UI/physics. Visibility belongs to each viewing camera.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public sealed class RemoteLIVCamera : MonoBehaviour
{
    public const float MaxDistance = 108f;
    static readonly List<RemoteLIVCamera> Active = new List<RemoteLIVCamera>(8);
    static readonly List<Camera> Views = new List<Camera>(8);
    MeshRenderer mesh;

    void Awake() => mesh = GetComponent<MeshRenderer>();
    void OnEnable()
    {
        if (mesh == null) mesh = GetComponent<MeshRenderer>();
        if (Active.Count == 0)
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        Active.Add(this);
    }
    void OnDisable()
    {
        Active.Remove(this);
        if (Active.Count == 0)
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            Views.Clear();
        }
    }
    static void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        Views.Add(camera);
        ApplyAll(camera);
    }
    static void AfterCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Views.Count == 0 || Views[Views.Count - 1] != camera) return;
        Views.RemoveAt(Views.Count - 1);
        if (Views.Count > 0) ApplyAll(Views[Views.Count - 1]);
    }
    static void ApplyAll(Camera camera)
    {
        EyePositions(camera, out var left, out var right);
        foreach (var prop in Active)
            if (prop != null) prop.mesh.forceRenderingOff = !WithinRange(prop.transform.position, left, right);
    }
    public void ApplyView(Camera camera)
    {
        if (mesh == null) mesh = GetComponent<MeshRenderer>();
        mesh.forceRenderingOff = !VisibleFrom(camera, transform.position);
    }
    public static bool VisibleFrom(Camera camera, Vector3 position)
    {
        if (camera == null) return false;
        EyePositions(camera, out var left, out var right);
        return WithinRange(position, left, right);
    }
    static bool WithinRange(Vector3 position, Vector3 left, Vector3 right) =>
        (position - left).sqrMagnitude <= MaxDistance * MaxDistance
        || (position - right).sqrMagnitude <= MaxDistance * MaxDistance;

    static void EyePositions(Camera camera, out Vector3 left, out Vector3 right)
    {
        // A hard radial ceiling, independent of fog settings or the owner's distance.
        // Either stereo eye can keep the prop visible at the boundary.
        if (camera.stereoEnabled)
        {
            left = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3);
            right = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.GetColumn(3);
            return;
        }
        left = right = camera.transform.position;
    }
}

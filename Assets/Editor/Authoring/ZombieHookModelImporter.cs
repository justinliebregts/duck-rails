using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The FBX includes the rigid hook in its single skinned mesh. Separate it once
// on import, preserving its exact shape and UVs, so the runtime can move it independently.
sealed class ZombieHookModelImporter : AssetPostprocessor
{
    void OnPostprocessModel(GameObject model)
    {
        if (assetPath != "Assets/Models/ZombieHook/ZombieHook.fbx") return;
        context.DependsOnSourceAsset("Assets/Editor/Authoring/ZombieHookModelImporter.cs");
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = skin.bones;
        int wristIndex = Array.FindIndex(bones, t => t.name == "hand.R");
        var wrist = bones[wristIndex];
        var mesh = skin.sharedMesh;
        var weights = mesh.boneWeights;
        var source = mesh.vertices;
        var normals = mesh.normals;
        var uv = mesh.uv;
        var toWrist = mesh.bindposes[wristIndex];
        var normalMatrix = toWrist.inverse.transpose;
        var remap = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var tipNormals = new List<Vector3>();
        var tipUV = new List<Vector2>();
        var hookTriangles = new List<int>();
        var bodyTriangles = new List<int>();
        var triangles = mesh.triangles;
        Vector3 forwardPoint = Vector3.zero;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var weight = weights[triangles[i]];
            bool onHook = weight.boneIndex0 == wristIndex && weight.weight0 > 0.999f;
            for (int j = i; j < i + 3; j++)
            {
                int index = triangles[j];
                if (!onHook) { bodyTriangles.Add(index); continue; }
                if (!remap.TryGetValue(index, out int mapped))
                {
                    mapped = positions.Count;
                    remap.Add(index, mapped);
                    Vector3 point = toWrist.MultiplyPoint3x4(source[index]);
                    positions.Add(point);
                    tipNormals.Add(normalMatrix.MultiplyVector(normals[index]).normalized);
                    tipUV.Add(uv[index]);
                    if (point.y > forwardPoint.y) forwardPoint = point;
                }
                hookTriangles.Add(mapped);
            }
        }
        if (hookTriangles.Count == 0) throw new InvalidOperationException("ZombieHook has no rigid right-hand hook.");
        mesh.SetTriangles(bodyTriangles, 0);
        var hookMesh = new Mesh { name = "ChainHookHead" };
        hookMesh.SetVertices(positions);
        hookMesh.SetNormals(tipNormals);
        hookMesh.SetUVs(0, tipUV);
        hookMesh.SetTriangles(hookTriangles, 0);
        // A generated mesh must belong to the import artifact to survive a reload.
        context.AddObjectToAsset("ChainHookHead", hookMesh);
        var tip = Child(wrist, "HookHead", Vector3.zero);
        tip.gameObject.AddComponent<MeshFilter>().sharedMesh = hookMesh;
        tip.gameObject.AddComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterial;
        Child(tip, "HookPoint", forwardPoint);
        Child(wrist, "GroundContact.R", forwardPoint);
        var left = Child(Array.Find(bones, t => t.name == "forearm.L"), "hand.L", Vector3.up * 0.185f);
        Child(left, "GroundContact.L", Vector3.up * 0.06f);
    }

    static Transform Child(Transform parent, string name, Vector3 offset)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = offset;
        return t;
    }
}

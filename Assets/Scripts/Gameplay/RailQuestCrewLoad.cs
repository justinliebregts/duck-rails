#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Game.Animation;
using Photon.Pun;
using UnityEngine;

/// <summary>Local render/IK workload, owned exclusively by an armed Quest benchmark.</summary>
public sealed class RailQuestCrewLoad : IDisposable
{
    readonly GameObject root;
    readonly RemotePlayerBody[] bodies;
    readonly FingerRig[] fingers;
    readonly Transform[] tops, bottoms;
    readonly Quaternion[] topRest, bottomRest;
    readonly Vector3[] topScale, bottomScale;
    readonly Light[] lights;
    public int RigCount => bodies.Length;
    public int LightCount => lights.Length;
    public long PoseApplications { get; private set; }
    public long LipApplications { get; private set; }
    public string LightSettings { get; }

    public RailQuestCrewLoad(int totalCrew)
    {
        if (totalCrew != 4 && totalCrew != 8) throw new ArgumentException("Crew workload must be four or eight total ducks.");
        if (!(PhotonNetwork.PrefabPool is DefaultPool pool) || !pool.ResourceCache.TryGetValue("PhotonPlayer", out var prefab))
            throw new InvalidOperationException("Production PhotonPlayer prefab is not registered.");
        var source = prefab.GetComponentInChildren<RemotePlayerBody>(true);
        var flashlight = Resources.Load<GameObject>("Rails/RailFlashlight");
        var lightSource = flashlight != null ? flashlight.GetComponentInChildren<Light>(true) : null;
        if (!source || !lightSource) throw new InvalidOperationException("Missing production rig or flashlight.");
        root = new GameObject("Quest benchmark local crew");
        UnityEngine.Object.DontDestroyOnLoad(root);
        bodies = new RemotePlayerBody[totalCrew - 1]; fingers = new FingerRig[bodies.Length];
        tops = new Transform[bodies.Length]; bottoms = new Transform[bodies.Length];
        topRest = new Quaternion[bodies.Length]; bottomRest = new Quaternion[bodies.Length];
        topScale = new Vector3[bodies.Length]; bottomScale = new Vector3[bodies.Length];
        lights = new Light[totalCrew];
        try
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                // Clone only the untouched prefab's visual/IK subtree: no PhotonPlayer,
                // ownership, voice, physics, player registries or live posed bones.
                var body = UnityEngine.Object.Instantiate(source, root.transform);
                body.name = "Benchmark duck " + (i + 1);
                body.gameObject.SetActive(true);
                bodies[i] = body;
                fingers[i] = body.GetComponentInChildren<FingerRig>(true);
                var transforms = body.GetComponentsInChildren<Transform>(true);
                tops[i] = transforms.FirstOrDefault(t => t.name == "Lip_T");
                bottoms[i] = transforms.FirstOrDefault(t => t.name == "Lip_B");
                if (!tops[i] || !bottoms[i] || !body.LeftArm || !body.RightArm || !fingers[i])
                    throw new InvalidOperationException("Incomplete production duck rig.");
                topRest[i] = tops[i].localRotation; bottomRest[i] = bottoms[i].localRotation;
                topScale[i] = tops[i].localScale; bottomScale[i] = bottoms[i].localScale;
                foreach (var label in body.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.text = "";
            }
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i] = UnityEngine.Object.Instantiate(lightSource, root.transform);
                lights[i].name = "Benchmark flashlight " + (i + 1);
                lights[i].gameObject.SetActive(true);
                lights[i].enabled = true;
                // One local-held light gets the same priority as HandFlashlight.
                if (i == 0) lights[i].renderMode = LightRenderMode.ForcePixel;
            }
            LightSettings = $"{lightSource.type}; range={lightSource.range}; intensity={lightSource.intensity}; angle={lightSource.spotAngle}; shadows={lightSource.shadows}; remotePriority={lightSource.renderMode}; one local ForcePixel";
        }
        catch { Dispose(); throw; }
    }

    public void Pose(Transform head, float seconds)
    {
        if (!head) throw new InvalidOperationException("Crew workload lost its tracked head.");
        var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
        for (int i = 0; i < bodies.Length; i++)
        {
            float phase = seconds * 1.7f + i * 1.31f;
            // Stable common slots between both workloads. Keep all rigs ahead of
            // each measured view; this deliberately stresses simultaneous visibility.
            int slot = i == 0 ? 0 : (i % 2 == 1 ? (i + 1) / 2 : -i / 2);
            Vector3 center = head.position + yaw * new Vector3(slot * .85f, .04f * Mathf.Sin(phase), 3.2f + Mathf.Abs(slot) * .25f);
            var facing = yaw * Quaternion.Euler(0, 180 + 18 * Mathf.Sin(phase * .4f), 0);
            var pose = new BodyPose {
                headPos = center, headRot = facing * Quaternion.Euler(8 * Mathf.Sin(phase), 0, 0),
                lHandPos = center + facing * new Vector3(-.3f - .1f * Mathf.Sin(phase), -.25f + .17f * Mathf.Sin(phase * 1.3f), .25f),
                rHandPos = center + facing * new Vector3(.3f + .1f * Mathf.Cos(phase), -.25f + .17f * Mathf.Cos(phase * 1.2f), .25f),
                lHandRot = facing * Quaternion.Euler(20 * Mathf.Sin(phase), 0, -30),
                rHandRot = facing * Quaternion.Euler(20 * Mathf.Cos(phase), 0, 30)
            };
            bodies[i].ApplyPose(pose);
            float curl = .5f + .45f * Mathf.Sin(phase);
            fingers[i].Apply(new FingerCurl { leftThumb = curl, leftIndex = curl, leftPinky = curl,
                rightThumb = 1 - curl, rightIndex = 1 - curl, rightPinky = 1 - curl });
            // Same lip angle/width range as PhotonVoiceMouth, synthetic loudness.
            float open = .5f + .5f * Mathf.Sin(phase * 5);
            tops[i].localRotation = topRest[i] * Quaternion.Euler(-65 * open, 0, 0);
            bottoms[i].localRotation = bottomRest[i] * Quaternion.Euler(65 * open, 0, 0);
            tops[i].localScale = new Vector3(Mathf.Lerp(1, .6f, open), topScale[i].y, topScale[i].z);
            bottoms[i].localScale = new Vector3(Mathf.Lerp(1, .6f, open), bottomScale[i].y, bottomScale[i].z);
            PoseApplications++; LipApplications++;
        }
        for (int i = 0; i < lights.Length; i++)
        {
            float phase = seconds * .8f + i * 1.31f;
            Vector3 position = i == 0 ? head.position + yaw * new Vector3(.3f, -.25f, .25f) : bodies[i - 1].RightHand.position;
            lights[i].transform.SetPositionAndRotation(position, yaw * Quaternion.Euler(12 + 12 * Mathf.Sin(phase), 65 * Mathf.Sin(phase * .7f), 0));
        }
    }

    public bool Healthy => root && bodies.All(b => b && b.isActiveAndEnabled) && lights.All(l => l && l.isActiveAndEnabled);
    public void Dispose() { if (root) UnityEngine.Object.Destroy(root); }
}
#endif

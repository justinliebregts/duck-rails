using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

// Explicitly invoked test probe. Samples before rendering, after all LateUpdates.
public sealed class RailWornEquipmentLiveProbe
{
    string label;
    double until;
    int frames, samples, missing, cargoFrames, syncFrames;
    float maxPosition, maxAngle;
    Vector3 trainStart;
    int lastFrame = -1;
    bool requireMoving;
    float minSpeed = float.PositiveInfinity, maxSpeed;
    readonly HashSet<int> views = new HashSet<int>();
    static readonly List<GameObject> spawned = new List<GameObject>();
    static readonly Dictionary<string, string> saved = new Dictionary<string, string>();

    public static void Wear()
    {
        if (!PhotonNetwork.InRoom || PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady || GR.Player.instance == null)
            throw new InvalidOperationException("A ready authenticated expedition is required.");
        if (spawned.Count != 0) throw new InvalidOperationException("Clean up previous test equipment first.");
        foreach (string slot in new[] { "head", "eyes" })
        foreach (string suffix in new[] { "", ".room", ".identity" })
        {
            string key = "equipment." + slot + suffix;
            saved[key] = UserPlayerPrefs.GetString(key, null);
        }
        foreach (string prefab in new[] { "Rails/RailHelmet", "Rails/RailGoggles" })
        {
            var go = PhotonNetwork.Instantiate(prefab, GR.Player.instance.helmetAnchor.position, Quaternion.identity);
            spawned.Add(go);
            RailRemoteBridge.Call(go.GetComponent<HeadEquipment>(), "Equip", GR.Player.instance.helmetAnchor);
        }
    }

    public static void Cleanup()
    {
        foreach (var go in spawned)
            if (go != null && go.GetComponent<PhotonView>().IsMine) PhotonNetwork.Destroy(go);
        spawned.Clear();
        foreach (var pair in saved)
            if (pair.Value == null) UserPlayerPrefs.DeleteKey(pair.Key);
            else UserPlayerPrefs.SetString(pair.Key, pair.Value);
        UserPlayerPrefs.Save();
        saved.Clear();
    }

    public static void Measure(string label, bool requireMoving = false)
    {
        var probe = new RailWornEquipmentLiveProbe();
        probe.label = label;
        probe.requireMoving = requireMoving;
        probe.until = Time.realtimeSinceStartupAsDouble + 8;
        probe.trainStart = RailTrain.Instance.transform.position;
        RenderPipelineManager.beginCameraRendering += probe.Sample;
    }

    void Sample(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying || RailTrain.Instance == null) { RenderPipelineManager.beginCameraRendering -= Sample; return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        frames++;
        minSpeed = Mathf.Min(minSpeed, RailTrain.Instance.Speed);
        maxSpeed = Mathf.Max(maxSpeed, RailTrain.Instance.Speed);
        int seen = 0;
        foreach (var gear in UnityEngine.Object.FindObjectsByType<HeadEquipment>(FindObjectsSortMode.None))
        {
            var view = gear.GetComponent<PhotonView>();
            if (view == null || view.IsMine || !gear.IsEquipped) continue;
            var wearer = PhotonPlayer.ForActor(view.OwnerActorNr);
            if (wearer == null || wearer.helmetAnchor == null) continue;
            var anchor = wearer.helmetAnchor.transform;
            seen++; samples++; views.Add(view.ViewID);
            maxPosition = Mathf.Max(maxPosition, Vector3.Distance(gear.transform.position, anchor.position));
            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(gear.transform.rotation, anchor.rotation));
            if ((bool)gear.GetComponent<RailCargo>().CaptureFrame()[0]) cargoFrames++;
            if (gear.GetComponent<PhotonTransformView>().enabled) syncFrames++;
        }
        if (seen != 2) missing++;
        if (Time.realtimeSinceStartupAsDouble < until) return;
        float displacement = Vector3.Distance(trainStart, RailTrain.Instance.transform.position);
        bool pass = frames >= 60 && views.Count == 2 && missing == 0 && maxPosition < .001f && maxAngle < .1f && cargoFrames == 0 && syncFrames == 0 && (!requireMoving || displacement > 10);
        string summary = $"{(pass ? "PASS" : "FAIL")} {label}: actor={PhotonNetwork.LocalPlayer.ActorNumber} clients={PhotonNetwork.CurrentRoom.PlayerCount} frames={frames} samples={samples} remoteViews={views.Count} missingFrames={missing} maxPosition={maxPosition:F6}m maxAngle={maxAngle:F4}deg cargoFrames={cargoFrames} syncFrames={syncFrames} trainDisplacement={displacement:F3}m speed={minSpeed:F2}..{maxSpeed:F2}m/s";
        Directory.CreateDirectory("Temp/worn-equipment");
        File.WriteAllText("Temp/worn-equipment/" + label + ".txt", summary);
        Debug.Log(summary);
        RenderPipelineManager.beginCameraRendering -= Sample;
    }
}

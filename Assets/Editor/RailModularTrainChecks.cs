#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailModularTrainChecks
{
    static readonly List<string> evidence = new List<string>();
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Train check failed: " + message);
        evidence.Add("PASS " + message);
    }
    [MenuItem("Duck Rails/Train/Validate Modular Train")]
    public static void Run()
    {
        evidence.Clear();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RailModularTrainBuilder.TrainPath);
        Check(asset != null, "Assembly prefab exists");
        var root = PrefabUtility.LoadPrefabContents(RailModularTrainBuilder.TrainPath);
        try
        {
            var a = root.GetComponent<RailTrainAssembly>(); var t = root.GetComponent<RailTrain>();
            Check(a != null && t != null && t.cabin != null, "Assembly preserves RailTrain and passenger carrier");
            var cabin = new SerializedObject(t.cabin).FindProperty("passengerCabin").objectReferenceValue as BoxCollider;
            bool narrow=root.transform.Find("Yard Mule shell/Single wood deck")!=null;
            Check(cabin != null && cabin.isTrigger && cabin.size == new Vector3(narrow?4:5, 2.8f, 10), "Passenger volume matches deck footprint");
            Check(root.GetComponentsInChildren<RailFurnaceModule>(true).Length == 1, "Exactly one furnace");
            foreach (var module in new[] { a.chair, a.controls, a.radio, a.furnace.transform })
                Check(PrefabUtility.IsAnyPrefabInstanceRoot(module.gameObject), module.name + " is a nested prefab");
            Check(Mathf.Abs(a.chair.localPosition.x) < .001f && Mathf.Abs(a.controls.localPosition.x) < .001f, "Chair and controls centered");
            Check(a.furnace.transform.localPosition.x < 0 && a.radio.localPosition.x > 0, "Furnace left, radio right");
            Check(a.supplyPoints.Length == 12 && a.supplyPoints.All(x => x != null && x.IsChildOf(root.transform)), "All twelve functional supply spawn references retained");
            Check(a.results != null && !a.results.gameObject.activeSelf, "Results UI retained and initially hidden");
            var controls = root.GetComponentsInChildren<RailControl>(true);
            foreach (var kind in new[] { RailControl.Action.Drive, RailControl.Action.Horn, RailControl.Action.Radio })
                Check(controls.Count(c => c.action == kind) == 1, "Exactly one " + kind + " control");
            var drive = controls.Single(c => c.action == RailControl.Action.Drive);
            Check(drive.handle != null && drive.handle.GetComponent<Collider>().isTrigger, "Throttle retains grip collider without a hand-locomotion wall");
            Check(drive.transform.IsChildOf(a.controls), "Throttle moves with cockpit control station");
            Check(a.fuelGauge.transform.localPosition.x < 0 && controls.Single(c => c.action == RailControl.Action.Horn).transform.localPosition.x > 0, "Gauge left and horn right of throttle");
            foreach (var hold in root.GetComponentsInChildren<RailHoldControl>(true))
                Check(hold.button != null && hold.button.cap != null && hold.button.GetComponent<BoxCollider>().isTrigger && !hold.button.diagnostic,
                    (hold.radio ? "Radio" : "Horn") + " has the existing momentary contact/lease interaction");
            Check(controls.Single(c => c.action == RailControl.Action.Radio).transform.IsChildOf(a.radio), "Radio talk interaction moves with table");
            var f = a.furnace;
            Check(f.Contains(f.insertion.position), "Coal center accepted inside hollow chamber");
            Check(!f.AcceptsSample(new Vector3(.7f, 0, 0)), "Coal outside mouth rejected even inside old radius");
            Check(!f.AcceptsSample(new Vector3(0, .5f, 0)), "Coal above insertion volume rejected");
            var original = f.transform.localPosition; var orientation = f.transform.localRotation;
            f.transform.localPosition += new Vector3(.1f, .2f, -.5f); f.transform.localRotation = Quaternion.Euler(0, 75, 0);
            Check(f.Contains(f.insertion.TransformPoint(new Vector3(.25f, -.15f, .2f))) && f.AcceptsSample(new Vector3(.2f, 0, 0)), "Relocated/rotated furnace carries its acceptance volume");
            Check(!f.AcceptsSample(new Vector3(.7f, 0, 0)), "Relocation keeps rejection boundary");
            f.transform.localPosition = original; f.transform.localRotation = orientation;
            Physics.SyncTransforms();
            var solids = root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            var mouthRay = new Ray(f.transform.TransformPoint(new Vector3(1.2f, .7f, 0)), -f.transform.right);
            float nearest = float.MaxValue;
            foreach (var c in f.GetComponentsInChildren<Collider>()) if (c.Raycast(mouthRay, out var hit, 4)) nearest = Mathf.Min(nearest, hit.distance);
            Check(nearest > 1.7f && nearest < 2, "Open mouth ray passes through cavity and hits rear wall");
            var sight = new Ray(root.transform.TransformPoint(new Vector3(0, .5f, 3.65f)), root.transform.forward);
            Check(!solids.Any(c => c.Raycast(sight, out _, 2)), "0.5m centered cockpit sightline clears solid geometry");
            foreach (float x in new[] { -1f, 0, 1f })
            {
                var ray = new Ray(root.transform.TransformPoint(new Vector3(x, .3f, -5.5f)), root.transform.forward);
                Check(!solids.Any(c => c.Raycast(ray, out _, 2)), "Rear boarding approach clear at X=" + x);
            }
            foreach (float sign in new[] { -1f, 1f })
            {
                var ray = new Ray(root.transform.TransformPoint(new Vector3(sign * 3, .3f, 0)), -sign * root.transform.right);
                Check(!solids.Any(c => c.Raycast(ray, out _, 1)), "Mid-deck side opening clear on " + sign);
            }
            Check(f.fire != null && f.smoke != null && f.fire.transform.IsChildOf(f.transform) && f.smoke.transform.IsChildOf(f.transform), "Both effects travel with furnace");
            Check(f.smoke.transform.localPosition.y > 3.5f && f.smoke.main.maxParticles == 24, "Tall stack smoke emitter has bounded particle count");
            RailFurnaceModule.SetEmission(f.smoke, true); Check(f.smoke.emission.enabled, "Smoke emission starts");
            RailFurnaceModule.SetEmission(f.smoke, false); Check(!f.smoke.emission.enabled, "Smoke emission stops");
            var yardMule = root.transform.Find("Yard Mule shell");
            if (yardMule)
            {
                var activeLights = root.GetComponentsInChildren<Light>().Where(l => l.enabled).ToArray();
                Check(activeLights.Length == 1 && activeLights[0].type == LightType.Spot && activeLights[0].shadows == LightShadows.None,
                    "Yard Mule uses one active shadowless spotlight; legacy lighting disabled");
                Check(!a.chair.gameObject.activeSelf, "Yard Mule chair hidden without breaking module reference");
                Check(activeLights[0].transform.localPosition.y > 1.8f && activeLights[0].transform.forward.y < 0,
                    "Roof spotlight is above canopy and aims downward");
            }
            else Check(root.GetComponentsInChildren<Light>(true).Length == 3, "Legacy blockout has one headlight and two side lamps");
            Check(root.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Modules do not introduce independent moving rigidbodies");
            Check(root.GetComponentsInChildren<Transform>(true).All(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject) == 0), "No missing scripts in assembly");
            var world = Object.FindFirstObjectByType<RailWorld>();
            if (world != null && world.train != null && world.train.GetComponent<RailTrainAssembly>() != null)
            {
                var live = world.train.GetComponent<RailTrainAssembly>(); var run = world.GetComponent<RailExpedition>();
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(world.train) == RailModularTrainBuilder.TrainPath, "Active scene uses assembly prefab");
                Check(run.furnace == live.furnace.insertion && run.gauge == live.fuelGauge && run.results == live.results, "Expedition links point into installed prefab");
                Check(run.supplyPoints.SequenceEqual(live.supplyPoints), "Scene supply links preserved");
            }
            Directory.CreateDirectory("docs/validation/modular-train");
            File.WriteAllLines("docs/validation/modular-train/editmode-checks.txt", new[] { DateTime.UtcNow.ToString("O") }.Concat(evidence));
            Debug.Log("Modular train: " + evidence.Count + " checks passed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Duck Rails/Train/Render Blockout Views")]
    public static void RenderViews()
    {
        Directory.CreateDirectory("output/train-blockout");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailModularTrainBuilder.TrainPath);
        var preview = new PreviewRenderUtility();
        try
        {
            var model = Object.Instantiate(prefab); model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.AddSingleGO(model);
            foreach (var label in model.GetComponentsInChildren<TMPro.TMP_Text>()) label.ForceMeshUpdate(true, true);
            preview.ambientColor = new Color(.38f, .4f, .44f);
            preview.lights[0].intensity = 1.5f; preview.lights[0].transform.rotation = Quaternion.Euler(45, -35, 0);
            preview.lights[1].intensity = .8f; preview.lights[1].transform.rotation = Quaternion.Euler(20, 145, 0);
            var camera = preview.camera; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .068f, .08f); camera.nearClipPlane = .05f; camera.farClipPlane = 100;
            var names = new[] { "three-quarter", "front", "back", "side", "top", "cockpit" };
            var positions = new[] { new Vector3(11, 10, -14), new Vector3(0, 1.4f, 18), new Vector3(0, 1.4f, -18), new Vector3(18, 1.4f, 0), new Vector3(0, 20, 0), new Vector3(0, .5f, 3.5f) };
            for (int i = 0; i < names.Length; i++)
            {
                camera.orthographic = i != 5; camera.orthographicSize = i == 0 ? 7 : i == 4 ? 6 : i == 3 ? 6 : 4;
                camera.fieldOfView = 85;
                camera.transform.position = positions[i];
                camera.transform.rotation = i == 4 ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation((i == 5 ? new Vector3(0, .35f, 5) : new Vector3(0, .8f, 0)) - positions[i]);
                preview.BeginStaticPreview(new Rect(0, 0, 1400, 1000)); preview.Render(true);
                var texture = preview.EndStaticPreview();
                File.WriteAllBytes("output/train-blockout/" + names[i] + ".png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            }
        }
        finally { preview.Cleanup(); }
        Debug.Log("Rendered six views from the same prefab to output/train-blockout.");
    }
}
#endif

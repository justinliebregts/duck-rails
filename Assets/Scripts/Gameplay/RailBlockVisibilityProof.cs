#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Offline Play Mode regression checks against actual authored route assets.</summary>
public sealed class RailBlockVisibilityProof : MonoBehaviour
{
    const string Output = "docs/validation/block-visibility/play-mode.txt";
    readonly List<string> lines = new List<string>();
    public string Result { get; private set; } = "Running";
    void Check(bool value, string label)
    {
        lines.Add((value ? "PASS " : "FAIL ") + label);
        File.WriteAllLines(Output, lines);
        if (!value) { Result = "FAIL " + label; throw new InvalidOperationException(label); }
    }
    static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    static int Payloads => Enumerable.Range(0, SceneManager.sceneCount)
        .Count(i => SceneManager.GetSceneAt(i).path.EndsWith("Lighting.unity"));
    static Renderer Marker(Transform parent, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        return go.GetComponent<Renderer>();
    }
    public IEnumerator Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        int initialMaps = LightmapSettings.lightmaps.Length;
        var camera = new GameObject("Local viewer").AddComponent<Camera>();
        camera.tag = "MainCamera";
        var station = Instantiate(Prefab("Assets/DuckRails/StartingStation.prefab"), new Vector3(0, 0, -10), Quaternion.identity).GetComponent<RailBlock>();
        var finale = Instantiate(Prefab("Assets/DuckRails/Blocks/bridge-finale-v1.prefab")).GetComponent<RailBlock>();
        foreach (var behaviour in finale.GetComponentsInChildren<MonoBehaviour>())
            if (!(behaviour is RailBlock) && !(behaviour is RailBakedBlockLighting)) behaviour.enabled = false;
        var root = new GameObject("Visibility route proof");
        var route = root.AddComponent<RailRoute>();
        var data = new GameObject("Inactive gameplay data"); data.SetActive(false);
        route.expedition = data.AddComponent<RailExpedition>();
        route.manager = data.AddComponent<Game.AI.EnemyManager>();
        route.startingStation = station; route.finale = finale; route.finaleRoot = finale.transform;
        route.catalog = new[] { "convenience-store", "container-yard", "farm-silos", "skyscraper-works", "hospital-district" }
            .Select(id => Prefab("Assets/DuckRails/Blocks/" + id + "-v1.prefab").GetComponent<RailBlock>()).ToArray();
        // Suppress store sign-in/wallet callbacks in this offline fixture before activation.
        var shop = Instantiate(Prefab("Assets/DuckRails/Economy/RailShop.prefab"), data.transform);
        route.midpointShop = shop.transform;
        shop.SetActive(false);
        foreach (var behaviour in shop.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        shop.transform.SetParent(root.transform);
        var disabled = Marker(station.transform, "Disabled renderer sentinel"); disabled.enabled = false;
        var hidden = Marker(station.transform, "Pre-hidden renderer sentinel"); hidden.forceRenderingOff = true;
        var networked = Marker(station.transform, "Network object sentinel");
        networked.gameObject.AddComponent<Photon.Pun.PhotonView>();
        var independent = Marker(null, "Train / loose object outside scenery");

        for (int round = 0; round < 3; round++)
        {
            var chosen = round == 0 ? route.catalog : round == 1 ? Enumerable.Reverse(route.catalog).ToArray() : route.catalog.Take(1).ToArray();
            camera.transform.position = new Vector3(0, 2, 70);
            route.Build(RailRoute.ChooseOrdered("visibility-" + round, chosen, 456));
            var visibility = route.GetComponent<RailBlockVisibility>();
            Check(visibility != null, "Runtime route automatically installs visibility, round " + round);
            float timeout = Time.realtimeSinceStartup + 120;
            while ((!route.Ready || Payloads != chosen.Length + 2) && Time.realtimeSinceStartup < timeout) yield return null;
            Check(route.Ready && Payloads == chosen.Length + 2, "All lighting payloads remain loaded, round " + round);
            var blocks = new[] { station }.Concat(route.Blocks).Concat(new[] { finale }).ToArray();
            var renderers = blocks.Select(b => b.GetComponentsInChildren<Renderer>(true).Where(r => r != networked).ToArray()).ToArray();
            var shopRenderers = shop.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponentInParent<Grabbable>(true) == null).ToArray();
            var shopItems = shop.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponentInParent<Grabbable>(true) != null).ToArray();
            var colliders = blocks.SelectMany(b => b.GetComponentsInChildren<Collider>(true)).ToDictionary(c => c, c => (c.enabled, c.gameObject.activeSelf));
            var behaviours = blocks.SelectMany(b => b.GetComponentsInChildren<MonoBehaviour>(true)).ToDictionary(b => b, b => b.enabled);
            var enabled = renderers.SelectMany(r => r).ToDictionary(r => r, r => r.enabled);
            int midpoint = 1 + RailRoute.MidpointBlockIndex(chosen.Length);
            void Verify(int index, string label)
            {
                bool correct = true;
                for (int b = 0; b < blocks.Length; b++)
                    correct &= renderers[b].All(r => r.forceRenderingOff == (Math.Abs(b - index) > 1 || r == hidden));
                correct &= shopRenderers.All(r => r.forceRenderingOff == (Math.Abs(midpoint - index) > 1));
                Check(correct, label + ": scenery and midpoint shop window");
                Check(!networked.forceRenderingOff && !independent.forceRenderingOff && shopItems.All(r => !r.forceRenderingOff), label + ": independent/networked/grabbable rendering retained");
            }
            for (int direction = 0; direction < 2; direction++)
            {
                for (int step = 0; step < blocks.Length; step++)
                {
                    int b = direction == 0 ? step : blocks.Length - 1 - step;
                    foreach (float offset in new[] { -.01f, 0, blocks[b].length * .5f })
                    {
                        // Large X/Y deliberately covers rooftops, underground and lateral exploration.
                        camera.transform.position = new Vector3(direction == 0 ? -60 : 60, direction == 0 ? -20 : 80, blocks[b].transform.position.z + offset);
                        yield return null;
                        Verify(offset < 0 ? Math.Max(0, b - 1) : b, "Round " + round + " direction " + direction + " block " + b + " offset " + offset);
                    }
                }
            }
            camera.transform.position = new Vector3(0, 2, finale.transform.position.z + finale.length + 500);
            yield return null; Verify(blocks.Length - 1, "Beyond finale / dawn ride");
            camera.transform.position = new Vector3(0, 2, -500);
            yield return null; Verify(0, "Teleport back before station");
            Check(colliders.All(p => p.Key.enabled == p.Value.Item1 && p.Key.gameObject.activeSelf == p.Value.Item2) &&
                behaviours.All(p => p.Key.enabled == p.Value) && enabled.All(p => p.Key.enabled == p.Value),
                "Collision, gameplay behaviours and authored renderer.enabled states unchanged, round " + round);
            Check(route.Ready && Payloads == chosen.Length + 2, "Visibility leaves all baked lighting ready, round " + round);
            lines.Add("INFO round " + round + ": total scenery renderers=" + renderers.Sum(r => r.Length) +
                ", suppressed from station=" + renderers.SelectMany(r => r).Count(r => r.forceRenderingOff));

            visibility.enabled = false;
            Check(renderers.SelectMany(r => r).All(r => r.forceRenderingOff == (r == hidden)) && shopRenderers.All(r => !r.forceRenderingOff), "Disable restores renderer flags");
            visibility.enabled = true;
            yield return null; Verify(0, "Re-enable reapplies window");
            camera.enabled = false;
            yield return null;
            Check(renderers.SelectMany(r => r).All(r => r.forceRenderingOff == (r == hidden)), "Missing viewer fails open");
            camera.enabled = true;
            yield return null; Verify(0, "Viewer returns");
            visibility.ApplyViewerPosition(new Vector3(0, 0, float.NaN));
            Check(renderers.SelectMany(r => r).All(r => r.forceRenderingOff == (r == hidden)), "Invalid viewer fails open");
            yield return null;
            route.enabled = false;
            yield return null;
            Check(renderers.SelectMany(r => r).All(r => r.forceRenderingOff == (r == hidden)), "Disabled route restores visibility");
            route.enabled = true;
            yield return null; Verify(0, "Route re-enabled");
        }
        Destroy(root);
        yield return null;
        Check(!station.GetComponentsInChildren<Renderer>(true).Any(r => r != hidden && r.forceRenderingOff) &&
            !finale.GetComponentsInChildren<Renderer>(true).Any(r => r.forceRenderingOff), "Destroy route restores surviving station and finale");
        Destroy(station.gameObject); Destroy(finale.gameObject); Destroy(data); Destroy(camera.gameObject); Destroy(independent.gameObject);
        float cleanup = Time.realtimeSinceStartup + 120;
        do { yield return null; } while (Payloads != 0 && Time.realtimeSinceStartup < cleanup);
        Check(Payloads == 0 && LightmapSettings.lightmaps.Length == initialMaps, "Teardown restores lighting baseline");
        Result = "PASS";
        lines.Add("PASS Offline actual-route visibility proof. Headset performance and real-client acceptance remain open.");
        File.WriteAllLines(Output, lines);
        Debug.Log("BLOCK VISIBILITY: " + lines.Count(l => l.StartsWith("PASS ")) + " checks passed");
    }
}
#endif

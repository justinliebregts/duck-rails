#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Offline Play Mode proof, only present in the editor-generated validation scene.</summary>
public sealed class RailStationLightingProof : MonoBehaviour
{
    public GameObject stationPrefab;
    public Transform trainVisual;
    public Transform duckVisual;
    public Transform itemVisual;
    public string Result { get; private set; } = "Running";
    readonly StringBuilder log = new StringBuilder();
    const string Output = "output/starting-station-lighting";
    void Check(bool pass, string label)
    {
        log.AppendLine((pass ? "PASS " : "FAIL ") + label);
        File.WriteAllText(Output + "/runtime-proof.txt", log.ToString());
        if (!pass) { Result = "FAIL " + label; throw new InvalidOperationException(label); }
    }
    IEnumerator Await(RailBakedBlockLighting block)
    {
        float end = Time.realtimeSinceStartup + 30;
        while (!block.Ready && string.IsNullOrEmpty(block.Failure) && Time.realtimeSinceStartup < end) yield return null;
        Check(block.Ready, "Lighting ready: " + block.name + " " + block.Failure);
    }
    int ProbeCount => Enumerable.Range(0,SceneManager.sceneCount).Sum(i=>
        LightProbes.GetSharedLightProbesForScene(SceneManager.GetSceneAt(i))?.countSelf ?? 0);
    int PayloadCount => Enumerable.Range(0, SceneManager.sceneCount).Count(i=>SceneManager.GetSceneAt(i).path.EndsWith("StartingStationLighting.unity"));
    void Bindings(RailBakedBlockLighting block)
    {
        var maps=LightmapSettings.lightmaps;
        Check(block.bindings.Length>0 && block.bindings.All(b=>b.renderer!=null && b.renderer.lightmapIndex>=0 && b.renderer.lightmapIndex<maps.Length &&
            maps[b.renderer.lightmapIndex].lightmapColor==b.color && b.renderer.lightmapScaleOffset==b.scaleOffset),"All prefab renderer lightmap indices and UV transforms restored");
    }
    SphericalHarmonicsL2 Sample(Vector3 p) { LightProbes.GetInterpolatedProbe(p,null,out var sh); return sh; }
    float Difference(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
    { float max=0;for(int c=0;c<3;c++)for(int i=0;i<9;i++)max=Mathf.Max(max,Mathf.Abs(a[c,i]-b[c,i]));return max; }
    public IEnumerator Start()
    {
        Directory.CreateDirectory(Output);
        int baseProbes=ProbeCount, baseMaps=LightmapSettings.lightmaps.Length;
        Check(baseProbes==0,"Proof starts without baked probes");
        var first=Instantiate(stationPrefab,Vector3.zero,Quaternion.identity).GetComponent<RailBakedBlockLighting>();
        yield return Await(first); Bindings(first);
        int perBlock=ProbeCount;
        Check(perBlock>0 && PayloadCount==1,"Companion loads native baked probes without geometry duplication");
        var points=new[]{new Vector3(7,1.5f,80),new Vector3(18,1.5f,80),new Vector3(0,1.5f,25)};
        var reference=points.Select(Sample).ToArray();
        Check(Difference(reference[0],reference[2])>.03f,"Platform lighting differs from dark boundary");
        Capture("origin",new Vector3(-16,6,57),new Vector3(4,1.5f,80));
        Vector3 offset=new Vector3(240,0,320);
        var second=Instantiate(stationPrefab,offset,Quaternion.identity).GetComponent<RailBakedBlockLighting>();
        yield return Await(second); Bindings(first);Bindings(second);
        Check(ProbeCount==perBlock*2 && PayloadCount==2,"Two block instances retain independent probe payloads");
        for(int i=0;i<points.Length;i++)Check(Difference(reference[i],Sample(points[i]+offset))<.003f,"Translated probe coefficients match at sample "+i);
        Capture("translated",offset+new Vector3(-16,6,57),offset+new Vector3(4,1.5f,80));
        Destroy(first.gameObject);
        yield return WaitCount(perBlock,1);
        Bindings(second);
        Check(Difference(reference[0],Sample(points[0]+offset))<.003f,"Remaining instance survives first-instance unload");
        Destroy(second.gameObject);
        yield return WaitCount(baseProbes,0);
        Check(LightmapSettings.lightmaps.Length==baseMaps,"Lightmap registrations return to baseline after unload");
        // Real gameplay placement is Z=-10. Exercise repeat load and dark-to-light-to-dark motion.
        var gameplay=Instantiate(stationPrefab,new Vector3(0,0,-10),Quaternion.identity).GetComponent<RailBakedBlockLighting>();
        yield return Await(gameplay);Bindings(gameplay);
        Check(ProbeCount==perBlock && PayloadCount==1,"Replay loads exactly one fresh probe payload");
        var before=Sample(new Vector3(7,1.5f,25));
        var lit=Sample(new Vector3(7,1.5f,70));
        Check(Difference(before,lit)>.03f,"Gameplay placement changes sampled light entering the station");
        for(int i=0;i<=80;i++)
        {
            float z=Mathf.Lerp(30,130,i/80f);
            if(trainVisual!=null)trainVisual.position=new Vector3(0,1,z);
            if(duckVisual!=null)duckVisual.position=new Vector3(7,.85f,z);
            if(itemVisual!=null)itemVisual.position=new Vector3(6,1.3f,z);
            yield return null;
            if(i==32)Capture("gameplay-moving-subjects",new Vector3(3,2.8f,z-7),new Vector3(7,1.5f,z));
        }
        if(duckVisual!=null)Check(duckVisual.GetComponentsInChildren<Renderer>().All(r=>r.lightProbeUsage==LightProbeUsage.BlendProbes),"Moving duck visual receives conventional probes");
        var flash=new GameObject("Temporary flashlight check").AddComponent<Light>();
        flash.type=LightType.Spot;flash.range=16;flash.intensity=12;flash.spotAngle=70;flash.shadows=LightShadows.None;
        flash.transform.SetPositionAndRotation(new Vector3(3,2,66),Quaternion.LookRotation(new Vector3(8,1,74)-new Vector3(3,2,66)));
        flash.enabled=false;yield return null;Capture("flashlight-off",new Vector3(1,2,63),new Vector3(8,1,74));
        flash.enabled=true;yield return null;Capture("flashlight-on",new Vector3(1,2,63),new Vector3(8,1,74));
        Destroy(flash.gameObject);
        Destroy(gameplay.gameObject);yield return WaitCount(baseProbes,0);
        // Cancellation during an in-flight load must also release its additive scene.
        var cancelled=Instantiate(stationPrefab,Vector3.zero,Quaternion.identity);
        yield return null;Destroy(cancelled);yield return new WaitForSeconds(.5f);yield return WaitCount(baseProbes,0);
        Result="PASS";log.AppendLine("PASS Completed offline Play Mode lifecycle/translation proof. No networking or headset-performance claim.");
        File.WriteAllText(Output+"/runtime-proof.txt",log.ToString());Debug.Log(log.ToString());
    }
    IEnumerator WaitCount(int count,int payloads)
    {
        float end=Time.realtimeSinceStartup+30;
        do {yield return null;} while((ProbeCount!=count||PayloadCount!=payloads)&&Time.realtimeSinceStartup<end);
        Check(ProbeCount==count && PayloadCount==payloads,"Cleanup restores probe/scene counts "+count+"/"+payloads);
    }
    public static void Capture(string name,Vector3 position,Vector3 target)
        => CaptureTo(Output,name,position,target);
    public static void CaptureTo(string output,string name,Vector3 position,Vector3 target)
    {
        Directory.CreateDirectory(output);
        var go=new GameObject("Lighting proof camera",typeof(Camera));var cam=go.GetComponent<Camera>();
        var rt=new RenderTexture(1000,700,24);var tex=new Texture2D(1000,700,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try {go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));cam.fieldOfView=75;cam.nearClipPlane=.05f;cam.farClipPlane=200;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=RenderSettings.fogColor;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1000,700),0,0);tex.Apply();File.WriteAllBytes(output+"/"+name+".png",tex.EncodeToPNG());}
        finally {RenderTexture.active=old;cam.targetTexture=null;DestroyImmediate(go);DestroyImmediate(rt);DestroyImmediate(tex);}
    }
}
#endif

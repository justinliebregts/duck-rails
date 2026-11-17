#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RailBridgeTerminalChecks
{
    [MenuItem("Duck Rails/Checks/Bridge Terminal")]
    public static void Run()
    {
        int count=0;
        void Check(bool good,string name){if(!good)throw new Exception(name);count++;Debug.Log("TERMINAL PASS: "+name);}
        var root=PrefabUtility.LoadPrefabContents(RailBridgeTerminalArt.Finale);
        try
        {
            var anchor=root.transform.Find("Env/Lower bridge button");var hold=anchor.GetComponent<RailHoldControl>();var control=hold.lever;
            Check(control && control.IsBridgeLever && hold.Interactable && hold.button==null,"Retained anchor uses lever, no pressable button");
            Check(anchor.GetComponentsInChildren<PressableButton>().Length==0,"Legacy floor button inactive");
            var train=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Train/Prefabs/CockpitControls.prefab").GetComponentsInChildren<RailControl>().First(x=>x.action==RailControl.Action.Drive);
            Check(control.palmOffset==train.palmOffset && control.returnDuration==train.returnDuration,"Exact train palm offset and return duration");
            var collider=control.handle.GetComponent<BoxCollider>();
            Check(collider.isTrigger && collider.size==train.handle.GetComponent<BoxCollider>().size && collider.center==train.handle.GetComponent<BoxCollider>().center,"Exact train grip volume, non-solid");
            Check(Mathf.Abs(collider.transform.TransformPoint(collider.center).y-8-.4485f)<.001f,"Neutral grip is 44.85 cm above balcony deck");
            Check(!control.BridgeForwardHeld,"Unheld lever starts inactive");
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(RailControl).GetMethod("CacheHandleRest",flags).Invoke(control,null);
            var pose=typeof(RailControl).GetMethod("PoseDrive",flags);
            pose.Invoke(control,new object[]{0f});var back=collider.transform.TransformPoint(collider.center);
            pose.Invoke(control,new object[]{1f});var forward=collider.transform.TransformPoint(collider.center);
            Check(forward.z>back.z && Mathf.Abs(forward.y-back.y)<.0001f,"Lever moves forward with constant end-stop grip height");
            Check(!RailDriveLease.ForwardDetent(false,.8f) && RailDriveLease.ForwardDetent(false,.85f) && !RailDriveLease.ForwardDetent(true,.7f),"Forward hold uses train detent, pulling back stops");
            var terminal=anchor.GetComponentInChildren<RailBridgeTerminal>();
            Check(terminal.percentage.font==AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset"),"Progress uses CutePixel SDF");
            foreach(float p in new[]{0f,.123f,.5f,.999f,1f})
            {
                terminal.Present(p,false);
                Check(terminal.percentage.text==RailBridgeTerminal.Percent(p)+"%","Percentage at "+p);
                Check(Mathf.Abs(terminal.progressFill.localScale.x-terminal.barWidth*p)<.00001f,"Continuous fill at "+p);
            }
            Check(RailBridgeTerminal.Percent(.999f)==99,"100% cannot appear before fully lowered");
            Check(terminal.mechanism.clip && terminal.mechanism.loop && !terminal.mechanism.playOnAwake && terminal.mechanism.spatialBlend==1,"Authored spatial mechanical loop, silent on load");
            Check(AssetDatabase.GetAssetPath(terminal.mechanism.clip)==RailBridgeTerminalArt.MechanismClip,"User-supplied BridgeLower_Loop assigned");
            Check(Mathf.Approximately(RailBridgeTerminal.StepMotor(0,true,.18f,.18f,.22f),1),"Motor reaches speed in 180 ms");
            Check(RailBridgeTerminal.StepMotor(1,false,.22f,.18f,.22f)==0,"Motor stops in 220 ms");
            float partial=RailBridgeTerminal.StepMotor(1,false,.11f,.18f,.22f);
            Check(partial>0 && partial<1 && RailBridgeTerminal.StepMotor(partial,true,.02f,.18f,.22f)>partial,"Rapid restart reverses smoothly from current speed");
            var gold=root.transform.Find("Env/Finale gold deposit");
            Check(Vector3.Distance(gold.localPosition,new Vector3(1.02f,8,70.52f))<.01f,"Existing gold crate placement preserved");
            Check(anchor.GetComponentsInChildren<MeshFilter>().Sum(x=>x.sharedMesh.triangles.Length/3)<2400,"Terminal geometry below 2400 triangles");
            Check(anchor.GetComponentsInChildren<Transform>().All(x=>!x.name.ToLower().Contains("wire")&&!x.name.ToLower().Contains("conduit")),"No rear cable");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var existing=SceneManager.GetSceneByPath("Assets/Scenes/Expedition.unity");bool opened=!existing.isLoaded;
        var scene=opened?EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity",OpenSceneMode.Additive):existing;
        try
        {
            var run=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<RailExpedition>(true)).First();
            Check(run.bridgeButton.GetComponent<RailHoldControl>().lever!=null,"Expedition reference inherits active terminal");
            Check(run.bridgeButton.GetComponentsInChildren<RailBridgeTerminal>().Length==1,"Exactly one terminal in production control");
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        RailBridgeHoldChecks.Run();RailDriveChecks.Run();
        File.WriteAllText("output/bridge-terminal/checks.txt",$"{DateTime.UtcNow:O}\nUnity {Application.unityVersion}\n{count} terminal checks; 17 bridge hold checks; 27 train drive checks passed.\nEdit Mode only; no multiplayer/headset/audio listening acceptance.\n");
        Debug.Log("TERMINAL: "+count+" checks passed");
    }

    [MenuItem("Duck Rails/Checks/Preview Bridge Terminal")]
    public static void Preview()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var previous=RenderTexture.active;
        GameObject root=null;RenderTexture rt=null;Texture2D image=null;
        try
        {
            root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailBridgeTerminalArt.Finale),scene);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.lightmapIndex=-1;r.gameObject.layer=30;}
            foreach(var l in root.GetComponentsInChildren<Light>(true))l.enabled=false;
            var terminal=root.GetComponentInChildren<RailBridgeTerminal>();terminal.Present(.47f,false);terminal.percentage.ForceMeshUpdate();
            var cameraGo=new GameObject("Terminal review camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var cam=cameraGo.AddComponent<Camera>();cam.scene=scene;cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.045f,.065f,.075f);
            cam.transform.position=new Vector3(-2f,10.5f,67.2f);cam.transform.LookAt(new Vector3(.0f,8.25f,70.7f));cam.orthographic=true;cam.orthographicSize=1.55f;cam.nearClipPlane=.05f;cam.farClipPlane=200;
            foreach(var pos in new[]{new Vector3(-3,12,67),new Vector3(3,10,70)})
            {
                var g=new GameObject("Review fill");SceneManager.MoveGameObjectToScene(g,scene);g.transform.position=pos;var l=g.AddComponent<Light>();l.type=LightType.Point;l.range=20;l.intensity=4;l.cullingMask=1<<30;
            }
            rt=new RenderTexture(1200,900,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            image=new Texture2D(1200,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();
            File.WriteAllBytes("output/bridge-terminal/unity-installed.png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);if(image)Object.DestroyImmediate(image);if(rt)Object.DestroyImmediate(rt);}
    }
}
#endif

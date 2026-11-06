#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailCockpitArtChecks
{
    const string Evidence="docs/validation/yard-mule";
    [MenuItem("Duck Rails/Art/Validate Integrated Cockpit")]
    public static void Run()
    {
        var lines=new List<string>();
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new InvalidOperationException(message);lines.Add("PASS "+message);};
        var root=PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try
        {
            var assembly=root.GetComponent<RailTrainAssembly>();var controls=assembly.controls;var art=controls.Find("Integrated dash art");
            check(art!=null,"Integrated dash is inherited by the existing control module");
            var gauge=assembly.fuelGauge;
            check(Vector3.Angle(-gauge.transform.forward,new Vector3(0,Mathf.Cos(25*Mathf.Deg2Rad),-Mathf.Sin(25*Mathf.Deg2Rad)))<.01f,"Gauge faces up and toward the driver at 25 degrees");
            check(gauge.rectTransform.sizeDelta.x>=.56f,"Larger fuel readout retains assembly binding");
            var drive=controls.GetComponentsInChildren<RailControl>().Single(c=>c.action==RailControl.Action.Drive);
            check(drive.transform.localRotation==Quaternion.identity&&drive.handle.GetComponent<Collider>().isTrigger,"Upright throttle and original trigger grip are preserved");
            var horn=controls.GetComponentsInChildren<RailHoldControl>().Single();
            check(horn.button!=null&&horn.button.GetComponent<Collider>().isTrigger,"Existing momentary horn contact is retained");
            check(Vector3.Dot(horn.button.transform.TransformVector(horn.button.pressOffset).normalized,-(Quaternion.Euler(-25,0,0)*Vector3.up))>.999f,"Horn depresses into its angled panel");
            check(art.GetComponentsInChildren<Collider>().All(c=>c.gameObject.layer==6&&!c.isTrigger),"Dash has solid environment collision");
            check(art.GetComponentsInChildren<Light>().Length==0,"Emissive trim adds no lights");
            int edges=0,triangles=0;float worst=0;
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var v=mesh.vertices;var uv=mesh.uv;var ids=mesh.triangles;triangles+=ids.Length/3;
                for(int i=0;i<ids.Length;i+=3)for(int j=0;j<3;j++)
                {
                    int a=ids[i+j],b=ids[i+(j+1)%3];float error=Mathf.Abs(Vector2.Distance(uv[a],uv[b])*256/Vector3.Distance(v[a],v[b])/32-1);worst=Mathf.Max(worst,error);edges++;
                    if(float.IsNaN(error)||error>.003f)throw new Exception("Cockpit UV distortion: "+filter.name);
                }
                check((GameObjectUtility.GetStaticEditorFlags(filter.gameObject)&StaticEditorFlags.ContributeGI)==0,filter.name+" is non-static and does not bake GI");
            }
            lines.Add("PASS "+edges+" UV edges at 32 square texels/metre; worst relative error "+worst);
            lines.Add("Art geometry: "+triangles+" triangles; 3 renderers; 1 shared 256-square atlas material");
            var fuel=controls.GetComponent<RailCockpitFuelBar>();var saved=fuel.segments.sharedMesh;var bounds=saved.bounds;
            foreach(float value in new[]{0f,10f,40f,100f,-1f,20f})
            {
                fuel.SetFuel(value);int expected=value<=0?0:value==10?18:value==20?30:60;
                check(fuel.segments.sharedMesh.triangles.Length==expected,"Fuel bar handles "+value+" fuel (capacity 40)");
                check(fuel.segments.sharedMesh.bounds==bounds,"Fuel update retains visible mesh bounds");
            }
            check(saved.triangles.Length==60&&fuel.segments.sharedMesh!=saved,"Fuel updates leave the source mesh unchanged");
            var solids=root.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger&&c.enabled).ToArray();
            check(!solids.Any(c=>c.Raycast(new Ray(new Vector3(0,.5f,3.65f),Vector3.forward),out _,2)),"Forward duck-eye sightline remains clear at 0.5m");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Directory.CreateDirectory(Evidence);File.WriteAllLines(Evidence+"/cockpit-checks.txt",lines);Debug.Log(string.Join("\n",lines));
    }
    public static void Preview()
    {
        var p=new PreviewRenderUtility();
        try
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailYardMuleArt.Train));p.AddSingleGO(model);
            model.GetComponentInChildren<RailCockpitFuelBar>().SetFuel(10);
            foreach(var text in model.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);
            foreach(var mr in model.GetComponentsInChildren<Renderer>()){mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;mr.receiveShadows=false;}
            p.ambientColor=new Color(.5f,.5f,.5f);p.lights[0].intensity=1.4f;p.lights[0].transform.rotation=Quaternion.Euler(50,150,0);p.lights[1].intensity=.8f;p.lights[1].transform.rotation=Quaternion.Euler(30,-40,0);
            var c=p.camera;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.19f,.21f,.23f);c.nearClipPlane=.02f;c.farClipPlane=80;c.fieldOfView=76;
            for(int view=0;view<2;view++)
            {
                c.transform.position=view==0?new Vector3(-.8f,1.05f,3.05f):new Vector3(0,.5f,3.65f);c.transform.LookAt(new Vector3(0,view==0?.22f:.25f,4.6f));
                for(int frame=0;frame<2;frame++){p.BeginStaticPreview(new Rect(0,0,1400,800));p.Render(true);var im=p.EndStaticPreview();if(frame==1)File.WriteAllBytes(Evidence+"/cockpit-"+(view==0?"integrated":"eye")+".png",im.EncodeToPNG());Object.DestroyImmediate(im);}
            }
        }
        finally{p.Cleanup();}
    }
}
#endif

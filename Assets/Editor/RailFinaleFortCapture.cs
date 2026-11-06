#if UNITY_EDITOR
using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Object=UnityEngine.Object;
public static class RailFinaleFortCapture {
 public static void Capture()
 {
  RailCanyon.Guard();EditorSceneManager.OpenScene(RailFinaleFort.BakeScene);Directory.CreateDirectory("output/finale-fort");var go=new GameObject("Review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.55f,.69f,.8f);cam.farClipPlane=500;var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.transform.rotation=Quaternion.Euler(48,30,0);
  try{RenderSettings.fog=false;RenderSettings.ambientLight=new(.65f,.65f,.65f);RenderSettings.ambientIntensity=1.3f;foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){r.lightmapIndex=-1;r.enabled=true;}Shot("fortress",new(-56,43,14),new(0,4,57));Shot("traffic-jam",new(-48,28,-14),new(0,0,19));Shot("button-deck",new(10,14,61),new(0,8,71));Shot("happy-land",new(0,4,163),new(0,4,260));Shot("full-layout",new(0,230,150),new(0,0,150.1f));
   Shot("bridge-raised",new(-44,23,111),new(0,2,87));Shot("river-recovery",new(-4,2,104),new(-26,-5,84));
   Object.FindFirstObjectByType<RailBlock>().transform.Find("Bridge hinge").localRotation=Quaternion.identity;
   Shot("bridge-lowered",new(-25,13,103),new(0,-1,94));}
  finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailFinaleFort.BakeScene);}
  void Shot(string n,Vector3 eye,Vector3 target){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=65;var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/finale-fort/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
 }
}
#endif

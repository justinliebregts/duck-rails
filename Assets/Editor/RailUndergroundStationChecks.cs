#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class RailUndergroundStationChecks
{
 static int count;
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);count++;}
 [MenuItem("Duck Rails/Authoring/Station/Validate underground greybox")]
 public static void Run(){count=0;if(SceneManager.GetActiveScene().path!=RailStationLightingAuthoring.BakeScene)throw new Exception("Open station bake scene");Physics.SyncTransforms();var b=Object.FindFirstObjectByType<RailBlock>();Check(b.track.Length==2&&b.track[1]==new Vector3(0,0,160)&&b.stop.localPosition==new Vector3(0,0,80),"Retained track");
 foreach(float x in new[]{-11.3f,11.3f})for(int flight=0;flight<2;flight++)for(int i=0;i<14;i++){float z=(flight==0?82.32f:75.76f)-(i+.5f)*.34f;float y=(flight==0?1:3.55f)+(i+1)*.17f;Walk(new Vector3(x,y,z));}
 foreach(float x in new[]{-11.3f,11.3f}){Walk(new Vector3(x,3.55f,76.5f));Walk(new Vector3(x,1,83.5f));}
 for(float x=-11;x<=11;x++)Walk(new Vector3(x,6.1f,69.5f));
 foreach(float x in new[]{-4.5f,4.5f})for(float z=72;z<=91;z++)Walk(new Vector3(x,1,z));
 foreach(var p in new[]{new Vector3(-6,1,87.9f),new Vector3(6,1,87.5f),new Vector3(6,1,90.5f)})Walk(p);
 for(float z=80;z<=150;z+=1)Check(!Physics.CheckBox(new Vector3(0,2.65f,z+2),new Vector3(2.59f,2.35f,7),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Train envelope blocked at "+z);
 var data=b.GetComponent<RailBakedBlockLighting>();Check(data.bindings.Length>0&&data.bindings.All(v=>v.renderer&&v.color),"Current lighting bindings");Check(b.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Baked fixtures only");
 Directory.CreateDirectory("docs/validation/station-greybox");File.WriteAllText("docs/validation/station-greybox/checks.txt","PASS "+count+" physical/track/lighting checks. Stair treads, landings, bridge, aisles, doors and forward train sweep. Editor geometry evidence only; headset traversal and actual side boarding unverified.\n");Debug.Log("PASS station checks: "+count);}
 static void Walk(Vector3 p){Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var hit,.2f,1<<6)&&Mathf.Abs(hit.point.y-p.y)<.03f,"Missing floor "+p);Check(!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.8f,.22f,1<<6,QueryTriggerInteraction.Ignore),"Blocked walk/headroom "+p);}
 [MenuItem("Duck Rails/Authoring/Station/Fit departure shop in Expedition")]
 public static void FitShop(){var route=Object.FindFirstObjectByType<RailRoute>();var shop=route.startingStation.transform.Find("Departure Shop");if(!shop)throw new Exception("Missing departure shop");shop.localPosition=new Vector3(-9.5f,1,89);shop.localRotation=Quaternion.identity;foreach(Transform t in shop){if(t.name=="Shop wall"||t.name=="Shop roof")t.gameObject.SetActive(false);}
 var items=shop.GetComponentsInChildren<StoreWallItem>(true);for(int i=0;i<items.Length;i++){var t=items[i].transform;if(i<4){t.localPosition=new Vector3(-2.6f,1.1f,-1.9f+i*1.3f);t.localRotation=Quaternion.Euler(0,-90,0);}else{t.localPosition=new Vector3(-.8f+(i-4)*1.3f,1.1f,2.2f);t.localRotation=Quaternion.identity;}PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
 var exchange=shop.GetComponentInChildren<RailGoldReceiver>(true);exchange.transform.localPosition=new Vector3(-.8f,0,-2.1f);exchange.transform.localRotation=Quaternion.Euler(0,180,0);PrefabUtility.RecordPrefabInstancePropertyModifications(exchange.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(shop);foreach(Transform t in shop){if(t.name=="Shop wall"||t.name=="Shop roof")PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
 // Retain sign but fit it to the room.
 foreach(var text in shop.GetComponentsInChildren<TMPro.TMP_Text>(true))if(text.text.StartsWith("SUPPLIES")){text.transform.localPosition=new Vector3(0,2.7f,2.5f);text.rectTransform.sizeDelta=new Vector2(6,1);text.fontSize=1.3f;PrefabUtility.RecordPrefabInstancePropertyModifications(text);PrefabUtility.RecordPrefabInstancePropertyModifications(text.transform);}
 EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Debug.Log("Departure shop refit with "+items.Length+" full-size displays.");}
 [MenuItem("Duck Rails/Authoring/Station/Capture bake scene views")]
 public static void Capture(){Directory.CreateDirectory("output/station-greybox");Shot("platform",new Vector3(-4.5f,2.5f,84),new Vector3(1,4,69));Shot("bridge",new Vector3(-10,7.2f,69.5f),new Vector3(8,6.8f,70));Shot("portal",new Vector3(12,4,153),new Vector3(0,4,136));}
 static void Shot(string n,Vector3 pos,Vector3 target){var go=new GameObject("Review camera");var camera=go.AddComponent<Camera>();var rt=new RenderTexture(1400,900,24);var tex=new Texture2D(1400,900,TextureFormat.RGB24,false);var old=RenderTexture.active;try{go.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos));camera.fieldOfView=80;camera.nearClipPlane=.05f;camera.farClipPlane=220;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.04f,.055f);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,900),0,0);tex.Apply();File.WriteAllBytes("output/station-greybox/"+n+".png",tex.EncodeToPNG());}finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}}
}
#endif



#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class RailUndergroundStation
{
 public const string RootName="Underground station greybox";
 static Transform root; static Mesh cube; static Material wall,floor,metal,edge;
 static GameObject Box(string n,Vector3 p,Vector3 s,Material m,bool collision=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.layer=6;g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localScale=s;g.GetComponent<MeshFilter>().sharedMesh=cube;var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=n.Contains("Mountain")?.03f:.4f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static void BoundsBox(string n,float x,float X,float y,float Y,float z,float Z,Material m){Box(n,new Vector3((x+X)/2,(y+Y)/2,(z+Z)/2),new Vector3(X-x,Y-y,Z-z),m);}
 static void Guard(float x,float z,float Z,float y){BoundsBox("Stair guard",x-.07f,x+.07f,y,y+1.2f,z,Z,metal);}
 static void Stair(float x){for(int flight=0;flight<2;flight++){float start=flight==0?82.32f:75.76f;float b=flight==0?1:3.55f;for(int i=0;i<14;i++){float z=start-(i+1)*.34f;float y=b+(i+1)*.17f;BoundsBox("Stair tread",x,x+2.4f,1,y,z,z+.34f,floor);Guard(x-.08f,z,z+.34f,y);Guard(x+2.48f,z,z+.34f,y);}}BoundsBox("Intermediate landing",x,x+2.4f,1,3.55f,75.76f,77.56f,floor);Guard(x-.08f,75.76f,77.56f,3.55f);Guard(x+2.48f,75.76f,77.56f,3.55f);}
 static void DoorWall(float x,float z,float Z,float door){BoundsBox("Room wall",x-.1f,x+.1f,1,4.2f,z,door,wall);BoundsBox("Room wall",x-.1f,x+.1f,1,4.2f,door+2.4f,Z,wall);BoundsBox("Door lintel",x-.1f,x+.1f,3.6f,4.2f,door,door+2.4f,wall);}
 static void Lamp(string name,Vector3 p,Vector3 target){Box(name+" fixture",p+Vector3.up*.12f,new Vector3(.5f,.15f,.4f),edge,false);var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.LookRotation(target-p);var l=g.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(1,.83f,.62f);l.intensity=65;l.range=16;l.spotAngle=115;l.innerSpotAngle=75;l.shadows=LightShadows.Soft;}
 [MenuItem("Duck Rails/Authoring/Station/Install underground greybox")]
 public static void Install(){if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Exit play/bake");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save scene first");
 cube=AssetDatabase.LoadAssetAtPath<Mesh>(RailStationLightingAuthoring.Folder+"/Cube Station UV.asset");wall=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/DepotGreybox/Walls.mat");floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/DepotGreybox/Paths.mat");metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/DepotGreybox/Roof.mat");edge=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Safety.mat");if(!cube||!wall||!floor||!metal||!edge)throw new Exception("Missing materials/UV");
 var scene=EditorSceneManager.OpenScene(RailStationLightingAuthoring.BakeScene);var block=Object.FindFirstObjectByType<RailBlock>();if(block.id!="starting-station-v1")throw new Exception("Wrong station");
 var keep=new[]{"Track Entry","Track Exit","Train Start","Station ground","Track rail","Sleeper","Future Store Area"};foreach(Transform t in block.transform.Cast<Transform>().ToArray())if(!keep.Contains(t.name))Object.DestroyImmediate(t.gameObject);
 root=new GameObject(RootName).transform;root.SetParent(block.transform,false);
 foreach(float side in new[]{-1f,1f}){Box("Platform 10 x 24",new Vector3(side*8,.5f,80),new Vector3(10,1,24),floor);Box("Safety edge",new Vector3(side*3.12f,1.012f,80),new Vector3(.18f,.024f,24),edge,false);Box("Station outer wall",new Vector3(side*13.2f,4.3f,80),new Vector3(.4f,8.6f,24.4f),wall);Box("Front end wall",new Vector3(side*8.6f,4.3f,92.2f),new Vector3(9.2f,8.6f,.4f),wall);Box("Tunnel wall",new Vector3(side*4.25f,3.5f,116),new Vector3(.5f,7,48),wall);Box("Mountain flank",new Vector3(side*26,9,105),new Vector3(43,18,70),metal);}
 // Mountain mass stays outside all station rooms; replace flank's inner face at X +/-13.4.
 foreach(Transform t in root)if(t.name=="Mountain flank"){t.localPosition=new Vector3(Mathf.Sign(t.localPosition.x)*30.7f,9,105);t.localScale=new Vector3(34.6f,18,70);}
 Box("Station ceiling",new Vector3(0,8.85f,80),new Vector3(26.8f,.5f,24.8f),metal);Box("Tunnel ceiling",new Vector3(0,7.25f,116),new Vector3(8,.5f,48),metal);Box("Mountain above tunnel",new Vector3(0,13,117),new Vector3(26.8f,11,46),metal);
 BoundsBox("Rear station wall",-13.4f,13.4f,0,8.6f,67.6f,68,wall); // scenic arrival alcove opens from inside station
 BoundsBox("Arrival recess left",-9,-8.8f,1,3.8f,68,70,wall);BoundsBox("Arrival recess right",-6.4f,-6.2f,1,3.8f,68,70,wall);
 BoundsBox("Footbridge slab",-12.5f,12.5f,5.8f,6.1f,68,71,floor);
 BoundsBox("Bridge rear guard",-12.5f,12.5f,6.1f,7.3f,68,68.14f,metal);BoundsBox("Bridge front guard",-10.1f,10.1f,6.1f,7.3f,70.86f,71,metal);
 Guard(-12.57f,68,71,6.1f);Guard(12.57f,68,71,6.1f);Stair(-12.5f);Stair(10.1f);
 BoundsBox("Supplies south wall",-13,-6,1,4.2f,85.9f,86.1f,wall);DoorWall(-6,86,92,86.7f);BoundsBox("Supplies ceiling",-13,-6,4.2f,4.4f,86,92,metal);
 BoundsBox("Store south wall",6,13,1,4.2f,85.9f,86.1f,wall);BoundsBox("Staff partition",6,13,1,4.2f,88.9f,89.1f,wall);DoorWall(6,86.1f,88.9f,86.3f);DoorWall(6,89.1f,92,89.3f);BoundsBox("East rooms ceiling",6,13,4.2f,4.4f,86,92,metal);
 Box("Waiting bench",new Vector3(8,1.4f,83.35f),new Vector3(3,.8f,.7f),metal);Box("Staff desk",new Vector3(11.5f,1.65f,90.2f),new Vector3(1.5f,1.3f,.8f),metal);Box("Store shelving",new Vector3(12.5f,2,87.4f),new Vector3(.6f,2,1.8f),metal);
 foreach(float x in new[]{-8f,8f}){Lamp("Room fixture",new Vector3(x,3.9f,90.5f),new Vector3(x,1,90));Lamp("Platform fixture",new Vector3(x,5.5f,81),new Vector3(x/2,1,79));Lamp("Stair lower fixture",new Vector3(x,5,80),new Vector3(Mathf.Sign(x)*11.3f,2,79));Lamp("Bridge fixture",new Vector3(x,8.2f,69.5f),new Vector3(x,6.1f,69.5f));Lamp("Stair middle fixture",new Vector3(Mathf.Sign(x)*11.3f,7,76.5f),new Vector3(Mathf.Sign(x)*11.3f,3.55f,76.5f));}
 Lamp("Store fixture",new Vector3(9,3.9f,87.4f),new Vector3(9,1,87.4f));foreach(float z in new[]{98f,116f,135f})Lamp("Tunnel fixture",new Vector3(0,6.6f,z),new Vector3(0,0,z));
 block.transform.Find("Future Store Area").localPosition=new Vector3(-9.5f,1,89);
 var data=block.GetComponent<RailBakedBlockLighting>();data.bindings=Array.Empty<RailBakedBlockLighting.Binding>();PrefabUtility.RecordPrefabInstancePropertyModifications(data);PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
 var probes=Object.FindFirstObjectByType<LightProbeGroup>();var points=new List<Vector3>();Physics.SyncTransforms();for(float z=69;z<140;z+=3)foreach(float x in new[]{-11f,-7f,-4f,0,4f,7f,11f})foreach(float y in new[]{1.4f,2.5f,4.5f,6.5f,7.5f}){if(z>92&&Mathf.Abs(x)>3)continue;var p=new Vector3(x,y,z);if(!Physics.CheckSphere(p,.18f,1<<6,QueryTriggerInteraction.Ignore))points.Add(p);}foreach(float z in new[]{1f,60f,145f,159f})foreach(float x in new[]{-4f,0,4f})points.Add(new Vector3(x,2,z));probes.probePositions=points.ToArray();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();RailStationLightingAuthoring.Bake();Debug.Log("Underground station installed; bake started.");}
}
#endif

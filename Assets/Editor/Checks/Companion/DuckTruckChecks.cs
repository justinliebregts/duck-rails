using System;using System.Reflection;using UnityEngine;using UnityEditor.SceneManagement;using Photon.Pun;
public static class DuckTruckChecks {
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
static object Call(DuckCompanion d,string n,params object[] a)=>typeof(DuckCompanion).GetMethod(n,F).Invoke(d,a);
static void Set(DuckCompanion d,string n,object v)=>typeof(DuckCompanion).GetField(n,F).SetValue(d,v);
static object Get(DuckCompanion d,string n)=>typeof(DuckCompanion).GetField(n,F).GetValue(d);
static void Check(bool b,string s){if(!b)throw new Exception(s);Debug.Log("PASS: "+s);}
public static void Run(){
EditorSceneManager.OpenScene("Assets/FenceLevel.unity");
var go=new GameObject("Truck exit walker");go.AddComponent<PhotonView>();var d=go.AddComponent<DuckCompanion>();Call(d,"Awake");Set(d,"environment",(LayerMask)(1<<6));
foreach(string name in new[]{"Blue_Box_Truck_A_Body_part_4","Blue_Box_Truck_B_Body_part_4"}) {
var roof=GameObject.Find(name).GetComponent<Collider>();var bounds=roof.bounds;int tested=0;
for(float z=bounds.max.z-.5f;z>bounds.min.z+.3f && tested<3;z-=.6f)
for(float x=bounds.min.x+.35f;x<bounds.max.x-.3f && tested<3;x+=.4f){
var start=new Vector3(x,2.246f,z);
if(!(bool)Call(d,"ClearBody",start))continue;
if(!Physics.Raycast(start+Vector3.up*.1f,Vector3.down,out var floor,.3f,1<<6))continue;
Call(d,"ResetNavigationAfterAuthorityChange");go.transform.position=start;Physics.SyncTransforms();
var goal=bounds.center+Vector3.right*8;goal.y=.87f;
Check(!(bool)Call(d,"BuildTruckExit",start),"player inside keeps ordinary following");
Check((bool)Call(d,"BuildTruckExit",goal),name+" finds cargo exit from "+start);
var route=(Vector3[])Get(d,"truckExitRoute");var end=route[route.Length-1];
for(int frame=0;frame<1200;frame++){
if(tested==0 && frame==15){Call(d,"ResetNavigationAfterAuthorityChange");Check((bool)Call(d,"BuildTruckExit",goal),"takeover replans truck exit");route=(Vector3[])Get(d,"truckExitRoute");end=route[route.Length-1];}
if(!(bool)Call(d,"WalkTruckExit",goal,.025f))break;
}
Debug.Log("TRUCK END "+go.transform.position+" expected="+end);
Check(Vector3.Distance(go.transform.position,end)<.4f,name+" actual motor exits loaded cargo and lands");tested++;
}
Check(tested==3,name+" three actual cargo starts tested");
}
// Block a known cargo start after planning: neither replanning nor yielding is
// allowed to restart the shared no-progress / respawn clock.
Call(d,"ResetNavigationAfterAuthorityChange");
go.transform.position=new Vector3(68.684f,2.246f,-33.24f);Physics.SyncTransforms();
var outside=new Vector3(80,.87f,-35);
Set(d,"lastProgressAt",123.0);
Check((bool)Call(d,"BuildTruckExit",outside),"route exists before dynamic obstruction");
var barriers=new System.Collections.Generic.List<GameObject>();
for(int side=0;side<4;side++){
var wall=new GameObject("Temporary cargo obstruction");wall.layer=6;
wall.transform.position=go.transform.position+Quaternion.Euler(0,90*side,0)*Vector3.forward*.29f+Vector3.up;
wall.transform.rotation=Quaternion.Euler(0,90*side,0);
wall.AddComponent<BoxCollider>().size=new Vector3(1,2,.1f);barriers.Add(wall);
}
Physics.SyncTransforms();bool yielded=false;
for(int frame=0;frame<160;frame++)if(!(bool)Call(d,"WalkTruckExit",outside,.025f)){yielded=true;break;}
Check(yielded,"blocked exit yields after bounded attempt");
Check((double)Get(d,"lastProgressAt")==123.0,"truck planning and blockage preserve failsafe clock");
foreach(var wall in barriers)UnityEngine.Object.DestroyImmediate(wall);
Physics.SyncTransforms();
Debug.Log("ALL TRUCK CHECKS PASSED");}
}


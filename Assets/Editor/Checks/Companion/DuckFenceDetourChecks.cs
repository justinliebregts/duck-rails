using System;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class DuckFenceDetourChecks
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Call(DuckCompanion d,string n,params object[] args)=>typeof(DuckCompanion).GetMethod(n,Flags).Invoke(d,args);
 static void Set(DuckCompanion d,string n,object v)=>typeof(DuckCompanion).GetField(n,Flags).SetValue(d,v);
 static object Get(DuckCompanion d,string n)=>typeof(DuckCompanion).GetField(n,Flags).GetValue(d);
 static void Check(bool v,string message){if(!v)throw new Exception(message);Debug.Log("PASS: "+message);}
 public static void Run(){
 Check(Application.isBatchMode,"isolated batch fixture");EditorSceneManager.OpenScene("Assets/FenceLevel.unity");
 const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
 typeof(PhotonNetwork).GetField("offlineMode",Static).SetValue(null,true);
 typeof(PhotonNetwork).GetField("offlineModeRoom",Static).SetValue(null,new Photon.Realtime.Room("Fence detour",new Photon.Realtime.RoomOptions(),true));
 var clock=typeof(PhotonNetwork).GetField("frametime",Static);clock.SetValue(null,100d);
 var go=new GameObject("Fence follower");go.AddComponent<PhotonView>();var d=go.AddComponent<DuckCompanion>();Call(d,"Awake");Set(d,"environment",(LayerMask)(1<<6));typeof(PhotonView).GetProperty("IsMine").SetValue(d.photonView,true);
 var starts=new[]{new Vector3(19.22f,.87f,-23.76f),new Vector3(32.84f,.87f,-24.68f),new Vector3(107.52f,1.01f,-59.78f)};
 var goals=new[]{new Vector3(21.62f,.87f,-24.75f),new Vector3(30.1f,.87f,-23.54f),new Vector3(106.37f,1.11f,-57f)};
 Physics.SyncTransforms();bool found=false;
 for(int i=0;i<starts.Length;i++){
 go.transform.position=starts[i];Set(d,"fenceDetourTarget",goals[i]);Set(d,"fenceDetourCommitted",true);
 if(!(bool)Call(d,"TryFenceDetourRoute",goals[i])){Debug.Log("NO COMPLETE DETOUR "+starts[i]);continue;}
 var route=(Vector3[])Get(d,"fenceDetourRoute");float length=0;Vector3 previous=starts[i];foreach(var p in route){Check((bool)Call(d,"ClearBodySweep",previous,p),"checked detour segment");length+=Vector3.Distance(previous,p);previous=p;}
 Debug.Log("ACTUAL INDUSTRIAL DETOUR start="+starts[i]+" goal="+goals[i]+" length="+length+" corners="+route.Length);
 Check(length>Vector3.Distance(starts[i],goals[i])+3,"longer route permitted around combined fence");
 Check(Physics.Raycast(starts[i]+Vector3.up*.6f,(goals[i]-starts[i]).normalized,out var hit,4,1<<6),"actual blocking industrial fence detected");
 Set(d,"fenceDetourCommitted",false);Call(d,"RegisterFenceAttempt",hit,goals[i]);double deadline=(double)Get(d,"fenceAttemptUntil");
 Check(deadline==105,"crossing budget is five seconds");
 go.transform.position+=Vector3.forward*.5f;Call(d,"RegisterFenceAttempt",hit,goals[i]);Check((double)Get(d,"fenceAttemptUntil")==deadline,"sideways movement does not restart attempt");
 clock.SetValue(null,105d);Set(d,"groundSupported",false);typeof(DuckCompanion).GetProperty("State").SetValue(d,DuckCompanion.Activity.Following);Call(d,"TickFenceAttempt");
 Check((bool)Get(d,"fenceDetourCommitted")&&(double)Get(d,"failedFenceUntil")==135d,"timeout commits detour with thirty-second local avoidance");
 typeof(DuckCompanion).GetProperty("Mode").SetValue(d,DuckCompanion.CompanionMode.Following);
 Set(d,"nextKillerCheck",104d);Set(d,"killerAttackChance",1f);
 Check(!(bool)Call(d,"TickKillerAttack")&&!d.KillerSequenceActive&&(double)Get(d,"nextKillerCheck")==104d,
     "due guaranteed killer roll stays pending during detour without taking over movement");
 Check(d.KillerStatus=="Killer mode inactive: finishing fence detour","debug distinguishes pending roll from active attack");
 Set(d,"fenceDetourCommitted",false);Set(d,"fenceAttemptUntil",105d);
 Check((string)Call(d,"KillerStartBlocker")=="finishing fence crossing attempt","expired crossing attempt remains gated until navigation resolves it");
 Set(d,"fenceAttemptUntil",0d);Set(d,"fenceReleaseUntil",107d);
 Check((string)Call(d,"KillerStartBlocker")=="finishing fence snag recovery","snag release blocks new attacks");
 Set(d,"fenceReleaseUntil",0d);
 Check((string)Call(d,"KillerStartBlocker")!="finishing fence detour","completed detour releases its killer gate");
 Set(d,"fenceDetourCommitted",true);
 Set(d,"fenceDetourRoute",Array.Empty<Vector3>());Set(d,"fenceDetourCorner",0);
 Set(d,"nextFenceDetourSearch",Time.time+3f);
 var waitingPosition=go.transform.position;
 Check(!(bool)Call(d,"TickFenceDetour",goals[i],.025f)&&go.transform.position==waitingPosition,
     "missing complete detour yields movement to existing alternate routes and recovery");
 Check((bool)Get(d,"fenceDetourCommitted")&&(double)Get(d,"failedFenceUntil")==135d,
     "failed route does not clear or extend local avoidance and killer gate");
 go.transform.position=starts[i];Check((bool)Call(d,"TryFenceDetourRoute",goals[i]),"route can be rebuilt after timeout");
 var peerGo=new GameObject("New master");peerGo.AddComponent<PhotonView>();var peer=peerGo.AddComponent<DuckCompanion>();Call(peer,"Awake");
 var stream=new PhotonStream(true,null);typeof(PhotonStream).GetMethod("SetWriteStream",Flags).Invoke(stream,new object[]{new System.Collections.Generic.List<object>(),0});Call(d,"SerializeHome",stream);Call(peer,"SerializeHome",new PhotonStream(false,stream.ToArray()));Call(peer,"ResetNavigationAfterAuthorityChange");
 Check((bool)Get(peer,"fenceDetourCommitted")&&(double)Get(peer,"failedFenceUntil")==135d&&(Vector3)Get(peer,"fenceDetourTarget")==goals[i],"takeover preserves detour intent and original avoidance deadline");UnityEngine.Object.DestroyImmediate(peerGo);
 var motor=go.GetComponent<CharacterController>();motor.enabled=true;
 // Simulate time passing beyond avoidance: the selected route must still be held.
 clock.SetValue(null,136d);
 for(int frame=0;frame<4000&&(bool)Get(d,"fenceDetourCommitted");frame++)Call(d,"TickFenceDetour",goals[i],.025f);
 Check(Vector3.Distance(go.transform.position,goals[i])<1f,"motor completes committed longer route even after avoidance expires");
 found=true;break;
 }
 Check(found,"actual industrial mesh has a tested longer detour");Debug.Log("ALL FENCE DETOUR CHECKS PASSED");
 }
}

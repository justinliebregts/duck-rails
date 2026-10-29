using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using Photon.Pun;
/// <summary>
/// Isolated batch regression: copy Quackville.unity as Assets/FenceLevel.unity
/// with its original city/industrial model GUIDs and collider transforms.
/// Lightmap UV generation may be disabled in the fixture; collider geometry must
/// remain unchanged. Compares low/full jumps and drives the real character motor.
/// </summary>
public static class DuckFenceChecks
{
 const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
 static object Call(DuckCompanion d,string n,params object[] a)=>typeof(DuckCompanion).GetMethod(n,Flags).Invoke(d,a);
 static void Set(DuckCompanion d,string n,object v)=>typeof(DuckCompanion).GetField(n,Flags).SetValue(d,v);
 public static void Run(){
 if(!Application.isBatchMode)throw new Exception("Isolated batch only");
 EditorSceneManager.OpenScene("Assets/FenceLevel.unity");
 var go=new GameObject("Fence probe");go.AddComponent<PhotonView>();var d=go.AddComponent<DuckCompanion>();Call(d,"Awake");Set(d,"environment",(LayerMask)(1<<6));
 const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
 typeof(PhotonNetwork).GetField("offlineMode",Static).SetValue(null,true);
 typeof(PhotonNetwork).GetField("offlineModeRoom",Static).SetValue(null,new Photon.Realtime.Room("Fence fixture",new Photon.Realtime.RoomOptions(),true));
 typeof(PhotonNetwork).GetField("frametime",Static).SetValue(null,100d);
 typeof(PhotonView).GetProperty("IsMine").SetValue(d.photonView,true);
 var playerObject=new GameObject("Followed player");playerObject.AddComponent<PhotonView>();var player=playerObject.AddComponent<PhotonPlayer>();player.photonView.OwnerActorNr=7;PhotonPlayer.Active.Add(player);d.Follow(7);
 Physics.SyncTransforms();int city=0,industrial=0,cityTakeoffs=0,industrialTakeoffs=0;
 foreach(var c in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)){
 if(c.sharedMesh==null)continue;
 string name=c.name+" "+c.sharedMesh.name;
 if(name.IndexOf("fence",StringComparison.OrdinalIgnoreCase)<0)continue;
 Debug.Log("FENCE "+name+" "+c.bounds+" layer="+c.gameObject.layer);
 bool ind=name.IndexOf("security",StringComparison.OrdinalIgnoreCase)>=0;
 var mesh=c.sharedMesh;var verts=mesh.vertices;var tris=mesh.triangles;int improved=0,valid=0;
 for(int t=0;t<tris.Length;t+=3){
 Vector3 a=c.transform.TransformPoint(verts[tris[t]]),b=c.transform.TransformPoint(verts[tris[t+1]]),e=c.transform.TransformPoint(verts[tris[t+2]]);
 Vector3 normal=Vector3.Cross(b-a,e-a).normalized;if(Mathf.Abs(normal.y)>.15f)continue;
 Vector3 mid=(a+b+e)/3;normal.y=0;normal.Normalize();
 for(int side=-1;side<=1;side+=2){
 Vector3 n=normal*side;
 if(!Physics.Raycast(mid+n*1.5f+Vector3.up*1,Vector3.down,out var from,10,1<<6)||from.normal.y<.7f)continue;
 if(!Physics.Raycast(mid-n*1.5f+Vector3.up*1,Vector3.down,out var to,10,1<<6)||to.normal.y<.7f)continue;
 Vector3 start=from.point+Vector3.up*.04f, end=to.point+Vector3.up*.04f;
 if(Mathf.Abs(end.y-start.y)>.15f||!(bool)Call(d,"ClearBody",start)||!(bool)Call(d,"ClearBody",end))continue;
 if((bool)Call(d,"ClearBodySweep",start,end))continue;
 valid++;
 Vector3 close=mid+n*.4f;close.y=start.y;
 if((ind?industrialTakeoffs:cityTakeoffs)<4 && (bool)Call(d,"ClearBody",close)){
 d.transform.position=close;Set(d,"traversalProbeOrigin",null);Set(d,"jumpSpeed",15.5f);
 if((bool)Call(d,"TryFenceTakeoffRoute",end)){
 var route=(Vector3[])typeof(DuckCompanion).GetField("obstacleRoute",Flags).GetValue(d);
 Vector3 launch=route[route.Length-1];
 Vector3 planned=(Vector3)typeof(DuckCompanion).GetField("fenceLanding",Flags).GetValue(d);
 var controller=d.GetComponent<CharacterController>();controller.enabled=true;
 for(int frame=1;frame<=90;frame++){
 Vector3 step=Vector3.Lerp(close,launch,frame/90f);controller.Move(step-d.transform.position);
 if(Vector3.Distance(step,d.transform.position)>.1f)throw new Exception("Blocked fence takeoff approach "+name);
 }
 Set(d,"traversalProbeOrigin",(Vector3?)d.transform.position);
 if(!(bool)Call(d,"BeginTraversal",planned,null,false))throw new Exception("Takeoff endpoint cannot jump "+name);
 if((ind?industrialTakeoffs:cityTakeoffs)==0){
 Set(d,"traversalProbeOrigin",null);Set(d,"progressGoal",end);Set(d,"nextPath",float.MaxValue);
 typeof(DuckCompanion).GetProperty("State").SetValue(d,DuckCompanion.Activity.Following);
 Call(d,"Move",end,true);
 if(d.State!=DuckCompanion.Activity.Jumping)throw new Exception("Arrived takeoff did not launch "+name);
 float flight=(float)typeof(DuckCompanion).GetField("traversalDuration",Flags).GetValue(d);
 float velocity=(float)typeof(DuckCompanion).GetField("traversalJumpSpeed",Flags).GetValue(d);
 Vector3 launchStart=d.transform.position;
 for(int frame=1;frame<=120;frame++){
 Vector3 step=(Vector3)Call(d,"JumpPoint",launchStart,planned,frame/120f,flight,velocity);
 Call(d,"AdvanceTraversal",flight/120f);
 if(Vector3.Distance(step,d.transform.position)>.1f)throw new Exception("Planned takeoff jump hit fence "+name);
 }
 if(d.TargetActor!=7)throw new Exception("Jump lost followed player");
 Debug.Log("TAKEOFF COMMIT PASS "+name+" launch="+launchStart+" landing="+planned);
 // Force the old too-close launch against this actual fence to exercise
 // unexpected collision recovery, rather than only testing accepted arcs.
 var phaseField=typeof(DuckCompanion).GetField("killerPhase",Flags);
 phaseField.SetValue(d,Enum.Parse(phaseField.FieldType,"Approaching"));Set(d,"killerDeadline",110d);
 d.transform.position=close;Physics.SyncTransforms();
 Set(d,"traversalStart",close);Set(d,"traversalEnd",planned);Set(d,"traversalTime",0f);
 DuckCompanion.TryJumpDuration(planned-close,15.5f,20f,5.88f,out flight);
 Set(d,"traversalDuration",flight);Set(d,"traversalJumpSpeed",15.5f);
 typeof(DuckCompanion).GetProperty("State").SetValue(d,DuckCompanion.Activity.Jumping);
 for(int frame=0;frame<180 && d.State==DuckCompanion.Activity.Jumping;frame++)Call(d,"AdvanceTraversal",flight/120f);
 if((double)typeof(DuckCompanion).GetField("fenceReleaseUntil",Flags).GetValue(d)<=PhotonNetwork.Time)throw new Exception("Fence collision did not start release "+name+" start="+close+" end="+planned+" actual="+d.transform.position+" state="+d.State+" contact="+typeof(DuckCompanion).GetField("lastFenceContact",Flags).GetValue(d));
 Vector3 snag=d.transform.position;
 var peerObject=new GameObject("Takeover peer");peerObject.AddComponent<PhotonView>();var peer=peerObject.AddComponent<DuckCompanion>();Call(peer,"Awake");
 var stateStream=new PhotonStream(true,null);typeof(PhotonStream).GetMethod("SetWriteStream",Flags).Invoke(stateStream,new object[]{new System.Collections.Generic.List<object>(),0});
 Call(d,"SerializeHome",stateStream);Call(peer,"SerializeHome",new PhotonStream(false,stateStream.ToArray()));Call(peer,"ResetNavigationAfterAuthorityChange");
 if((double)typeof(DuckCompanion).GetField("fenceReleaseUntil",Flags).GetValue(peer)!=PhotonNetwork.Time+2
 ||(double)typeof(DuckCompanion).GetField("failedFenceUntil",Flags).GetValue(peer)!=PhotonNetwork.Time+30
 ||(Vector3)typeof(DuckCompanion).GetField("fenceReleaseDirection",Flags).GetValue(peer)!=(Vector3)typeof(DuckCompanion).GetField("fenceReleaseDirection",Flags).GetValue(d))throw new Exception("Takeover lost fence release or avoidance");
 UnityEngine.Object.DestroyImmediate(peerObject);
 for(int frame=0;frame<90;frame++)Call(d,"TickFenceRelease",1f/60);
 if(Vector3.ProjectOnPlane(d.transform.position-snag,Vector3.up).magnitude<.5f)throw new Exception("Fence release did not back away "+name);
 if(d.TargetActor!=7)throw new Exception("Fence release lost followed player");
 if(!d.KillerSequenceActive||(double)typeof(DuckCompanion).GetField("killerDeadline",Flags).GetValue(d)!=110d)throw new Exception("Snag cancelled or extended armed chase");
 phaseField.SetValue(d,Enum.Parse(phaseField.FieldType,"None"));
 if((bool)Call(d,"BeginTraversal",planned,null,false))throw new Exception("Failed crossing immediately retried");
 Debug.Log("FENCE SNAG RELEASE PASS "+name+" from="+snag+" to="+d.transform.position);
 Set(d,"failedFenceUntil",0d);Set(d,"fenceReleaseUntil",0d);
 }
 controller.enabled=false;
 if(ind)industrialTakeoffs++;else cityTakeoffs++;
 }
 }
 d.transform.position=start;Set(d,"traversalProbeOrigin",(Vector3?)start);
 Set(d,"jumpSpeed",12.65f);bool low=(bool)Call(d,"BeginTraversal",end,to.transform,false);
 Set(d,"jumpSpeed",15.5f);bool high=(bool)Call(d,"BeginTraversal",end,to.transform,false);
 if(!low&&high){
 float duration;
 if(!DuckCompanion.TryJumpDuration(end-start,15.5f,20f,4.2f*1.4f,out duration))throw new Exception("Invalid duration");
 var motor=d.GetComponent<CharacterController>(); motor.enabled=true;
 for(int frame=1;frame<=120;frame++){
 Vector3 next=(Vector3)Call(d,"JumpPoint",start,end,frame/120f,duration,15.5f);
 motor.Move(next-d.transform.position);
 if(Vector3.Distance(next,d.transform.position)>.1f)throw new Exception("Fence motor collision "+name+" at "+d.transform.position);
 }
 if(Vector3.Distance(d.transform.position,end)>.1f)throw new Exception("Missed fence landing");
 motor.enabled=false;
 improved++;if(improved<=2)Debug.Log("FENCE PASS "+name+" from="+start+" to="+end);}
 }
 if(improved>=2)break;
 }
 Debug.Log("FENCE RESULT "+name+" valid="+valid+" improved="+improved);
 if(ind)industrial+=improved;else city+=improved;
 }
 if(city==0||industrial==0)throw new Exception("Missing improved crossings city="+city+" industrial="+industrial);
 Debug.Log("ALL FENCE CHECKS PASSED city="+city+" industrial="+industrial+" city takeoffs="+cityTakeoffs+" industrial takeoffs="+industrialTakeoffs);
 if(cityTakeoffs==0||industrialTakeoffs==0)throw new Exception("No backed-off takeoffs for both fence types");
 Call(d,"ResetNavigationAfterAuthorityChange");
 if((bool)typeof(DuckCompanion).GetField("fenceTakeoffPending",Flags).GetValue(d))throw new Exception("Stale takeoff survives takeover");
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 go=new GameObject("Ceiling probe");go.AddComponent<PhotonView>();d=go.AddComponent<DuckCompanion>();Call(d,"Awake");Set(d,"environment",(LayerMask)(1<<6));
 Set(d,"traversalProbeOrigin",(Vector3?)Vector3.zero);
 var barrier=new GameObject("Solid barrier");barrier.layer=6;barrier.transform.position=new Vector3(1.5f,1.25f,0);barrier.AddComponent<BoxCollider>().size=new Vector3(.1f,2.5f,5);
 var ceiling=new GameObject("Low ceiling");ceiling.layer=6;ceiling.transform.position=new Vector3(1.5f,3.3f,0);ceiling.AddComponent<BoxCollider>().size=new Vector3(8,.2f,8);
 Physics.SyncTransforms();
 if((bool)Call(d,"BeginTraversal",new Vector3(3,0,0),null,false))throw new Exception("Jump clipped ceiling");
 UnityEngine.Object.DestroyImmediate(barrier);UnityEngine.Object.DestroyImmediate(ceiling);Physics.SyncTransforms();
 if(!(bool)Call(d,"BeginTraversal",new Vector3(3,0,0),null,false))throw new Exception("Open jump regressed");
 var rail=new GameObject("Fence top clearance fixture");rail.layer=6;rail.transform.position=new Vector3(1.5f,2.9f,0);rail.AddComponent<BoxCollider>().size=new Vector3(.1f,5.8f,5);Physics.SyncTransforms();
 Vector3 nearTop=new Vector3(1.5f,6.01f,0);
 if(!(bool)Call(d,"ClearBody",nearTop)||(bool)Call(d,"ClearFenceMargin",nearTop,nearTop))throw new Exception("Top rail margin must reject a barely clear capsule");
 Debug.Log("FENCE SAFETY PASSED: overhead collision rejected, open jump retained, top rail margin enforced");
 }
}


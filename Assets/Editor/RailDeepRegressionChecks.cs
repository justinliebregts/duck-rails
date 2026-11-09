#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Real authenticated peers; fixture positioning, real gameplay requests and wallet transport.
/// Coordination files carry only test steps/observations. They never replicate game state.</summary>
[InitializeOnLoad]
public static class RailDeepRegressionChecks
{
    [Serializable] class Control { public string token, expiry, room, firstRun; public int step, revision, round, seed=12345; }
    [Serializable] class Pose { public int id, controller; public Vector3 position; }
    [Serializable] class Receipt { public Pose[] cargoPoses, enemyPoses; public string token, room, run, inventory, enemies, route, error; public int revision, coins; public float fuel; public Vector3 position; public double sampleTime; }
    static string Root { get { string p=Application.dataPath.Replace('\\','/'); int n=p.IndexOf("/Library/VP/",StringComparison.OrdinalIgnoreCase); return n<0?Path.GetDirectoryName(p):p.Substring(0,n); } }
    static string Folder => Root+"/docs/validation/overnight-regression/private";
    static string Arm => Root+"/Temp/rails-deep-control.json";
    static bool Main => CurrentPlayer.IsMainEditor;
    static int Slot => Main?0:CurrentPlayer.Tags.Contains("Player2")?1:CurrentPlayer.Tags.Contains("Player3")?2:3;
    static string Role => new[]{"main","player2","player3","player4"}[Slot];
    static string[] Roles = {"main","player2","player3","player4"};
    static Control c;
    static int revision=-1, itemId, walletBefore;
    static string token, itemToken;
    static bool done, action, ack, installed, released;
    static double entered, nextTick, readyAt;
    static float beforeFuel;
    static Func<bool> previousInvincible;
    static Func<string,RailRouteState> previousRoute;
    static RailDeepRegressionChecks() { EditorApplication.update += Tick; }
    public static void ArmTest(int seed=12345)
    {
        Directory.CreateDirectory(Folder);
        foreach(var role in Roles) { File.WriteAllText(Folder+"/"+role+".txt",""); File.WriteAllText(Folder+"/"+role+".json",""); }
        File.WriteAllText(Arm,JsonUtility.ToJson(new Control{seed=seed,token=Guid.NewGuid().ToString("N"),expiry=DateTime.UtcNow.AddHours(2).ToString("O")}));
    }
    public static void Disarm() { if(File.Exists(Arm)) File.WriteAllText(Arm,""); RailBridgeHoldFixture.SetHeld(false); }
    static void Log(string s) => File.AppendAllText(Folder+"/"+Role+".txt",DateTime.UtcNow.ToString("O")+" "+s+"\n");
    static void Require(bool ok,string s) { if(!ok) throw new Exception(s); }
    static bool Ready => PhotonSession.Instance!=null && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest && PhotonNetwork.InRoom;
    static bool Settled { get { if(!Ready){readyAt=0;return false;} if(readyAt==0)readyAt=EditorApplication.timeSinceStartup; return EditorApplication.timeSinceStartup-readyAt>4; } }
    static void Warp(Vector3 p) => NewPlayer.instance.Warp(p,Quaternion.identity);
    static void Board() => Warp(RailTrain.Instance.transform.TransformPoint(new Vector3(Slot%2==0?-.8f:.8f,.35f,-1-Slot/2*1.5f)));
    static PhotonView[] Views() => UnityEngine.Object.FindObjectsByType<PhotonView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(v=>v.ViewID!=0).ToArray();
    static string Inventory() => string.Join(",",Views().Where(v=>v.GetComponent<RailCargo>()!=null).OrderBy(v=>v.ViewID).Select(v=>v.ViewID+":"+v.SpawnToken));
    static string Enemies() => string.Join(",",Game.AI.Enemy.Active.Select(e=>e.GetComponent<PhotonView>()).OrderBy(v=>v.ViewID).Select(v=>v.ViewID+":"+v.SpawnToken));
    static Receipt Read(string role) { var s=File.ReadAllText(Folder+"/"+role+".json"); return string.IsNullOrEmpty(s)?null:JsonUtility.FromJson<Receipt>(s); }
    static bool All => Roles.All(r=>Read(r)?.token==token && Read(r)?.revision==revision);
    static void Ack(string s)
    {
        bool first = !ack;
        if(PhotonNetwork.PlayerList.Any(p=>p.IsInactive || PhotonPlayer.ForActor(p.ActorNumber)==null || p.CustomProperties[RailSessionRules.Ready] as string != PhotonSession.RunId+":"+PhotonSession.Phase))return;
        RailAvatarChecks.AssertCurrentRoom();
        var vs=Views(); if(RailExpedition.Instance!=null && vs.Any(v=>RailExpedition.Instance.State.WasConsumed(v.ViewID,v.SpawnToken)))return; Require(vs.GroupBy(v=>v.ViewID).All(g=>g.Count()==1),"Duplicate live Photon view");
        if(PhotonNetwork.LocalPlayer.CustomProperties["test.deep.slot"] == null) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable{["test.deep.slot"]=Slot});
        RailExpedition.Position(PhotonNetwork.LocalPlayer.ActorNumber,out var observedPosition);
        var row=new Receipt{cargoPoses=vs.Where(v=>v.GetComponent<RailCargo>()!=null).Select(v=>new Pose{id=v.ViewID,controller=v.ControllerActorNr,position=v.transform.position}).OrderBy(p=>p.id).ToArray(),enemyPoses=Game.AI.Enemy.Active.Select(e=>new Pose{id=e.GetComponent<PhotonView>().ViewID,controller=e.GetComponent<PhotonView>().ControllerActorNr,position=e.Root.position}).OrderBy(p=>p.id).ToArray(),position=observedPosition,sampleTime=PhotonNetwork.Time,token=token,revision=revision,room=PhotonNetwork.CurrentRoom.Name,run=PhotonSession.RunId,route=PhotonSession.Get(RailRoute.StateKey,""),inventory=Inventory(),enemies=Enemies(),coins=DuckzWallet.Instance.Coins,fuel=PhotonSession.CurrentFuel};
        Write(Folder+"/"+Role+".json",JsonUtility.ToJson(row)); if(first)Log("PASS step="+c.step+" round="+c.round+" "+s+" coins="+row.coins+" fuel="+row.fuel);ack=true;
    }
    // Readers must see a complete observation, especially the larger pose arrays.
    static void Write(string path,string value) { string temp=path+".tmp";File.WriteAllText(temp,value);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }
    static void Next(int n) { c.step=n;c.revision++;Write(Arm,JsonUtility.ToJson(c)); }
    static void Advance(int n,bool compare=false)
    {
        if(!Main||!All)return;
        if(compare) {var rows=Roles.Select(Read).ToArray();if(rows.Select(r=>r.inventory).Distinct().Count()!=1 || rows.Select(r=>r.enemies).Distinct().Count()!=1 || rows.Select(r=>r.route).Distinct().Count()!=1)return;}
        if(compare && (c.step==5 || c.step==17 || c.step==19)) { var rows=Roles.Select(Read).ToArray(); foreach(var peer in rows.Skip(1)) { for(int i=0;i<rows[0].cargoPoses.Length;i++) if(rows[0].cargoPoses[i].controller!=peer.cargoPoses[i].controller || Vector3.Distance(rows[0].cargoPoses[i].position,peer.cargoPoses[i].position)>.3f)return; for(int i=0;i<rows[0].enemyPoses.Length;i++) if(rows[0].enemyPoses[i].controller!=peer.enemyPoses[i].controller || Vector3.Distance(rows[0].enemyPoses[i].position,peer.enemyPoses[i].position)>.75f)return; } }
        Next(n);
    }
    static void MoveTrain(float distance,float fuel=50) { PhotonSession.Instance.Commit(new Hashtable{[RailSessionRules.Motion]=new object[]{PhotonNetwork.Time,distance,0f,0f},[RailSessionRules.Fuel]=fuel}); }
    static void SeedCheck(int crew)
    {
        var run=RailExpedition.Instance;var route=RailRoute.Instance;
        Require(route.Ready && route.Blocks.Count==4,"Four-block route ready");
        Require(route.Selection.resourceCrew==crew,"Retained resource crew");
        Require(UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).Length==run.coalPoints.Length,"Exact authored coal count");
        Require(UnityEngine.Object.FindObjectsByType<ItemGoldBar>(FindObjectsSortMode.None).Length==run.goldPoints.Length,"Exact authored gold count");
        Require(Game.AI.Enemy.Active.Count==run.baseEnemyCount,"Exact base enemy count");
        Require(Flashlights()==crew,"One flashlight per departing player");
        if(RailEnemySpawns.ForcedEnemyType<0)Require(RailEnemySpawns.Names.All(name=>Game.AI.Enemy.Active.Any(e=>e.name.StartsWith(name))),"All four normal enemy types spawned");
    }
    static int Flashlights() => Views().Count(v=>v.InstantiationData is object[] d && d.Length==3 && d[2] is string s && s.StartsWith("flashlight:"));
    static void Gold(int receiver)
    {
        var r=UnityEngine.Object.FindObjectsByType<RailGoldReceiver>(FindObjectsSortMode.None).First(x=>x.receiverId==receiver);
        if(!action)
        {
            var gold=UnityEngine.Object.FindObjectsByType<ItemGoldBar>(FindObjectsSortMode.None).OrderBy(x=>x.GetComponent<PhotonView>().ViewID).First();
            itemId=gold.GetComponent<PhotonView>().ViewID;itemToken=gold.GetComponent<PhotonView>().SpawnToken;action=true;
        }
        var v=PhotonView.Find(itemId); if(v==null)return;
        Warp(r.intake.transform.TransformPoint(r.intake.center)+Vector3.back*1.5f);
        if(!v.IsMine){v.RequestOwnership();return;}
        var item=v.GetComponent<PhotonGrabbable>(); if(item.IsHeld)GR.Player.instance.rightPlayerHand.Release();
        var p=r.intake.transform.TransformPoint(r.intake.center);v.transform.position=p;
        if(v.TryGetComponent<Rigidbody>(out var body)){body.position=p;body.linearVelocity=Vector3.zero;}
        // Physical intake OnTriggerStay generates the reliable production request.
    }
    static void Rescue()
    {
        var target=RailExpedition.Instance.State.players.FirstOrDefault(p=>p.down);if(target==null)return;
        if(!RailExpedition.Position(target.actor,out var head))return;
        var hand=GR.Player.instance.rightPlayerHand.HoldPoint.position;
        Warp(NewPlayer.instance.transform.position+head-hand);
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<nextTick)return;nextTick=EditorApplication.timeSinceStartup+.1;
        if(!EditorApplication.isPlaying)
        { if(installed){RailTestTools.EnemyInvincibilityRequested=previousInvincible;RailTestTools.OrderedRoute=previousRoute;installed=false;}return; }
        try
        {
            if(!File.Exists(Arm))return;var json=File.ReadAllText(Arm);if(string.IsNullOrEmpty(json))return;
            c=JsonUtility.FromJson<Control>(json); if(DateTime.UtcNow>DateTime.Parse(c.expiry).ToUniversalTime())return;
            if(token!=c.token){token=c.token;revision=-1;done=false;Log("START four authenticated clients; physical intake; real wallet transport; fixture teleports; seed="+c.seed+" forcedEnemyType="+RailEnemySpawns.ForcedEnemyType);}
            if(done)return;
            if(!installed){previousRoute=RailTestTools.OrderedRoute;RailTestTools.OrderedRoute=r=>RailRoute.ChooseNormal(r,RailRoute.Instance.catalog,c.seed);previousInvincible=RailTestTools.EnemyInvincibilityRequested;RailTestTools.EnemyInvincibilityRequested=()=>!(c?.step==10 && Slot==2);installed=true;}
            foreach(var role in Roles){var r=Read(role);if(r?.token==token && !string.IsNullOrEmpty(r.error))throw new Exception("Peer failure "+role+": "+r.error);}
            if(revision!=c.revision){revision=c.revision;entered=EditorApplication.timeSinceStartup;action=ack=released=false;readyAt=0;itemId=0;Log("ENTER step="+c.step+" round="+c.round);}
            double age=EditorApplication.timeSinceStartup-entered;
            Require(age<180,"Timeout step="+c.step+" phase="+PhotonSession.Phase+" status="+PhotonSession.Instance?.Status);
            if(!Ready)return;
            var session=PhotonSession.Instance;var run=RailExpedition.Instance;var train=RailTrain.Instance;
            switch(c.step)
            {
                case 0:
                    if(!DuckzWallet.Instance || !DuckzWallet.Instance.Loaded)break;
                    walletBefore=DuckzWallet.Instance.Coins;
                    if(Main){if(!action){session.RequestCreatePrivate(UnityEngine.Random.Range(20000,90000).ToString());action=true;break;}if(!PhotonSession.IsPrivate)break;c.room=PhotonNetwork.CurrentRoom.Name;Next(1);}break;
                case 1:
                    if(Slot==3){if(DuckzWallet.Instance.Loaded){walletBefore=DuckzWallet.Instance.Coins;Ack("Fourth player waits in compound");}break;}
                    if(PhotonNetwork.CurrentRoom.Name!=c.room){session.RequestJoinCode(c.room);break;}
                    if(Settled && PhotonNetwork.PlayerList.Length==3 && DuckzWallet.Instance.Loaded){Require(PhotonNetwork.PlayerList.Select(p=>p.UserId).Distinct().Count()==3,"Distinct account identities");walletBefore=DuckzWallet.Instance.Coins;Ack("Three distinct accounts in private staging");}
                    Advance(2);break;
                case 2:
                    if(Slot==3){Ack("Late entrant remains outside expedition");break;}
                    if(PhotonSession.Phase==RailPhase.Staging && PhotonNetwork.IsMasterClient)session.StartPrivateRun();
                    if(PhotonSession.Phase==RailPhase.Running && Settled){SeedCheck(3);Board();Ack("Fresh current route, coal/gold/enemies and three flashlights");}
                    if(Main&&All){c.firstRun=PhotonSession.RunId;Next(3);}break;
                case 3:
                    if(Slot==3){Ack("No sale while outside expedition");break;}
                    if(Main)Gold(1);
                    if(run.State.gold.Count==1){Ack("Departure-shop physical bar consumed once");}
                    Advance(4);break;
                case 4:
                    if(Slot==3){Require(DuckzWallet.Instance.Coins==walletBefore,"Absent player credited sale");Ack("Absent account received no sale");break;}
                    int credit=RailResourceBalance.GoldCredit(RailResourceBalance.GoldCount(3),1);
                    if(DuckzWallet.Instance.Coins==Math.Min(DuckzWallet.MaxCoins,walletBefore+credit)){Require(run.State.gold[0].recipients.Length==3,"Shop recipient count");Ack("Server wallet credited exact scaled amount");}
                    Advance(5);break;
                case 5:
                    if(Slot==3 && PhotonNetwork.CurrentRoom.Name!=c.room){session.RequestJoinCode(c.room);break;}
                    if(Settled && PhotonNetwork.PlayerList.Length==4 && run!=null && run.State.players.Count==4){Require(Flashlights()==3,"Late join added flashlight");Require(run.State.gold.Count==1,"Late join lost sale");Board();Ack("Late join aboard with retained route, sale and no extra flashlight");}
                    Advance(6,true);break;
                case 6:
                    if(Slot==3)Require(DuckzWallet.Instance.Coins==walletBefore,"Retroactive shop credit on late join");
                    if(age>4){beforeFuel=PhotonSession.CurrentFuel;walletBefore=DuckzWallet.Instance.Coins;Ack("Stable wallets after late join");}Advance(34,true);break;
                case 34:
                    if(Slot==3)Gold(2);
                    if(run.State.gold.Count==2){Require(run.State.gold[1].recipients.Length==4,"Second shop recipient count");Ack("Late entrant sold to second physical shop; four recipients");}Advance(35,true);break;
                case 35:
                    int shopExpected=Math.Min(DuckzWallet.MaxCoins,walletBefore+RailResourceBalance.GoldCredit(RailResourceBalance.GoldCount(3),Slot==3?1:2));
                    if(DuckzWallet.Instance.Coins==shopExpected && age>4){Board();Ack("Second shop credited all four actual wallets, per-player remainder correct");}Advance(7,true);break;
                case 7:
                    if(Slot==1)
                    {
                        if(!action){var coal=UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None).First();itemId=coal.GetComponent<PhotonView>().ViewID;itemToken=coal.GetComponent<PhotonView>().SpawnToken;Warp(coal.transform.position+Vector3.left);GR.Player.instance.rightPlayerHand.ForceGrab(coal.GetComponent<PhotonGrabbable>());action=true;}
                        var v=PhotonView.Find(itemId);
                        if(v!=null && v.IsMine && v.GetComponent<PhotonGrabbable>().IsHeld && age>2){GR.Player.instance.rightPlayerHand.Release();released=true;}
                        if(v!=null && released){Warp(run.furnace.position+Vector3.forward*1.5f);v.transform.position=run.furnace.position;if(v.TryGetComponent<Rigidbody>(out var body)){body.position=run.furnace.position;body.linearVelocity=Vector3.zero;}run.RequestCoal(v,v.GetComponent<PhotonGrabbable>());}
                    }
                    if(run.State.players.Sum(p=>p.coal)==1){Require(Mathf.Abs(PhotonSession.CurrentFuel-beforeFuel-RailResourceBalance.CoalValue(3))<.02f,"Exact coal fuel increment");Board();Ack("Non-master grab/release/furnace consumption and fuel replicated");}Advance(8,true);break;
                case 8:
                    if(age>4){Require(run.State.players.Sum(p=>p.coal)==1,"Duplicate coal credit");walletBefore=DuckzWallet.Instance.Coins;Ack("Coal duplicate rejected and consumed object absent");}Advance(36,true);break;
                case 36:
                    if(Main && !action){var go=PhotonNetwork.InstantiateRoomObject(RailWorld.Instance.bagPrefab.name,train.transform.TransformPoint(new Vector3(0,.5f,-2)),train.transform.rotation,0,BagObject.SpawnData(1,new[]{"","","",""},0,BagObject.Target.Dropped,0));var bag=go.GetComponent<BagObject>();bag.Put(bag.FirstEmptySlot(),"RailCoal",go.transform.position,.3f);PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{["test.deep.bag"]=bag.photonView.ViewID});action=true;}
                    var stored=PhotonView.Find(PhotonSession.Get("test.deep.bag",0))?.GetComponent<BagObject>();
                    if(stored!=null && stored.Keys().Count(k=>k=="RailCoal")==1 && age>2){Board();Ack("Bag Put replicated one coal slot to all four");}Advance(37,true);break;
                case 37:
                    var claimed=PhotonView.Find(PhotonSession.Get("test.deep.bag",0))?.GetComponent<BagObject>();
                    if(claimed==null)break;
                    if(Slot==3 && !action && claimed.RequestControl()){claimed.HoldLocal(GR.Player.instance.rightPlayerHand);action=true;}
                    if(Slot==3 && action && age>3 && claimed.State==BagState.Held)claimed.DropLocal(Vector3.zero);
                    if(age>5 && claimed.State==BagState.Dropped && claimed.photonView.Owner.CustomProperties["test.deep.slot"] is int bagOwner && bagOwner==3){Require(claimed.Keys().Count(k=>k=="RailCoal")==1,"Bag takeover changed contents");Ack("Non-master bag ownership, hold, release and contents agree");}Advance(38,true);break;
                case 38:
                    var emptied=PhotonView.Find(PhotonSession.Get("test.deep.bag",0))?.GetComponent<BagObject>();
                    if(emptied==null)break;
                    if(Slot==3 && !action)
                    {
                        var slot=emptied.Slots.First(s=>!s.IsEmpty);var hand=GR.Player.instance.rightPlayerHand;var palm=hand.HoldPoint.localPosition;
                        try { hand.HoldPoint.position=slot.DisplayContent.position;typeof(BagInventory).GetMethod("TakeOut",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(BagInventory.Local,new object[]{emptied,slot,hand}); }
                        finally {hand.HoldPoint.localPosition=palm;}
                        Require(hand.Held!=null && hand.Held.GetComponent<RailCoal>()!=null,"Production bag extraction did not grab coal");
                        var view=hand.Held.GetComponent<PhotonView>();Require(view.PreserveOnCreatorLeave,"Extracted coal must have room lifetime");
                        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{["test.deep.extracted"]=view.ViewID});hand.Release();action=true;
                    }
                    var extracted=PhotonView.Find(PhotonSession.Get("test.deep.extracted",0));
                    if(age>2 && emptied.Slots.All(s=>s.IsEmpty) && extracted!=null && extracted.GetComponent<RailCargo>().StateReady)Ack("Bag Take emptied slot and recreated exactly one shared loose coal");Advance(39,true);break;
                case 39:
                    var pickup=PhotonView.Find(PhotonSession.Get("test.deep.extracted",0));if(pickup==null)break;
                    if(Slot==2 && !action){GR.Player.instance.rightPlayerHand.ForceGrab(pickup.GetComponent<PhotonGrabbable>());action=true;}
                    if(age>3 && pickup.GetComponent<PhotonGrabbable>().IsHeldByAnyPlayer && pickup.Owner.CustomProperties["test.deep.slot"] is int coalOwner && coalOwner==2)Ack("Different player picked up extracted coal; held state and owner agree");Advance(40,true);break;
                case 40:
                    if(Slot==2 && !action){GR.Player.instance.rightPlayerHand.Release();action=true;}
                    var loose=PhotonView.Find(PhotonSession.Get("test.deep.extracted",0));
                    if(age>4 && loose!=null && !loose.GetComponent<PhotonGrabbable>().IsHeldByAnyPlayer){Require(train.Contains(loose.transform.position),"Released extracted coal left train");Ack("Transferred item released loose aboard on every client");}Advance(9,true);break;
                case 9:
                    if(Main)Gold(0);
                    if(run.State.SafeBars==1){Require(DuckzWallet.Instance.Coins==walletBefore,"Safe paid before escape");Board();Ack("Safe intake count shared; no immediate credit");}Advance(10,true);break;
                case 10:
                    if(Slot==2 && !RailExpedition.LocalDown){var attacker=Game.AI.Enemy.Active.OrderBy(e=>e.GetComponent<PhotonView>().ViewID).First(e=>e.GetComponentsInChildren<KillTrigger>().Any(t=>t.isActiveAndEnabled));Warp(attacker.Root.position+Vector3.right*.5f);if(age>2)attacker.GetComponentsInChildren<KillTrigger>().First(t=>t.isActiveAndEnabled).TryContact(NewPlayer.instance.bodyCollider);}
                    if(run.State.players.Count(p=>p.down)==1){Require(run.State.players.Single(p=>p.down).flareAt>0,"Down flare timestamp missing");Ack("Real enemy contact callback down replicated with flare");}Advance(11);break;
                case 11:
                    if(Slot==1)Rescue();
                    if(run.State.players.Any(p=>p.reviveAt>0)){Ack("Physical hand proximity starts revive");}Advance(12);break;
                case 12:
                    if(Slot==1)Board();
                    if(age>2){Require(run.State.players.Any(p=>p.down)&&run.State.players.All(p=>p.reviveAt==0),"Revive interruption did not reset");Ack("Interrupted revive reset");}Advance(13);break;
                case 13:
                    if(Slot==1)Rescue();
                    if(age>4 && run.State.players.All(p=>!p.down)){Require(run.State.players.Sum(p=>p.revives)==1,"Exactly one revive credit");Board();Ack("Continuous physical hand rescue revived once");}Advance(14);break;
                case 14:
                    if(PhotonNetwork.IsMasterClient&&!action){var enemy=Game.AI.Enemy.Active.First(e=>run.State.carriers.Any(x=>x.marker==e.GetComponent<RailCoalCarrier>().Marker&&x.carrying));enemy.ReceiveTrustedHit(new Vector3(4,0,2),enemy.Root.position);action=true;}
                    if(age>3 && run.State.carriers.Any(x=>x.drops.Count>0)){Ack("Accepted enemy hit emitted retained replicated coal drop");}Advance(15,true);break;
                case 15:
                    if(Slot==1&&!action){PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);action=true;}
                    if(age>5 && PhotonNetwork.MasterClient.CustomProperties["test.deep.slot"] is int masterSlot && masterSlot==1){Ack("Authority handoff preserves cargo, enemies and payouts");}Advance(16,true);break;
                case 16:
                    if(Slot==3){if(!action){session.LeaveCurrentRoom();action=true;break;}if(PhotonNetwork.CurrentRoom.Name!=c.room)Ack("Fourth client left run");}
                    else if(PhotonNetwork.PlayerList.Length==3)Ack("Three remain with unchanged expedition");Advance(17);break;
                case 17:
                    if(Slot==3 && PhotonNetwork.CurrentRoom.Name!=c.room){session.RequestJoinCode(c.room);break;}
                    if(Settled && PhotonNetwork.PlayerList.Length==4){Require(DuckzWallet.Instance.Coins==walletBefore,"Rejoin replayed wallet credit");Require(PhotonSession.RunId==c.firstRun && run.State.SafeBars==1 && Flashlights()==3,"Rejoin changed retained state");Require(PhotonView.Find(PhotonSession.Get("test.deep.bag",0)).GetComponent<BagObject>().Slots.All(s=>s.IsEmpty),"Former bag owner resurrected taken slot");Require(PhotonView.Find(PhotonSession.Get("test.deep.extracted",0))!=null,"Extracted coal lost during handoff/rejoin");Board();Ack("Rejoin restores safe/cargo/enemies and empty bag without new grants");}Advance(18,true);break;
                case 18:
                    if(PhotonNetwork.IsMasterClient&&!action){MoveTrain(RailRoute.Instance.FinaleStart+55);action=true;}
                    if(age>3){Board();Ack("Positioned at finale for physical bridge hold");}Advance(19);break;
                case 19:
                    if(Slot==1)RailBridgeHoldFixture.SetHeld(true);
                    if(run.State.BridgeProgress(PhotonNetwork.Time)>.1f && Game.AI.Enemy.Active.Count==run.enemyPoints.Length)
                    {
                        Require(run.enemyPoints.Length>run.baseEnemyCount,"Additional bridge defenders configured");
                        var markers=Game.AI.Enemy.Active.Select(e=>e.GetComponent<RailCoalCarrier>()?.Marker).ToArray();
                        Require(markers.Distinct().Count()==markers.Length && Enumerable.Range(0,run.enemyPoints.Length).All(i=>markers.Contains("enemy-"+i)),"Every base/defender spawn marker exactly once");
                        Ack("Physical bridge hold; exact enemy count="+markers.Length+" including defenders="+(markers.Length-run.baseEnemyCount)+"; unique markers and shared identities/controllers/poses");
                    }
                    Advance(20,true);break;
                case 20:
                    RailBridgeHoldFixture.SetHeld(false);if(Slot==1)Board();
                    if(age>2 && !run.State.BridgeMoving(PhotonNetwork.Time)){Require(run.State.BridgeProgress(PhotonNetwork.Time)>.05f,"Bridge progress lost");Ack("Release pauses bridge with retained work");}Advance(21);break;
                case 21:
                    if(Slot==1)RailBridgeHoldFixture.SetHeld(true);
                    if(run.State.BridgeProgress(PhotonNetwork.Time)>=1){RailBridgeHoldFixture.SetHeld(false);Board();Ack("Bridge fully lowered by actual button hold");}Advance(22);break;
                case 22:
                    if(Slot==3)Warp(train.transform.position+Vector3.left*14);
                    else if(Slot==2 && !RailExpedition.LocalDown){Warp(train.transform.position+Vector3.right*8);RailExpedition.RequestDown();break;}
                    else Board();
                    if(age>2){RailExpedition.Position(PhotonNetwork.LocalPlayer.ActorNumber,out var finishPosition);Require(train.Contains(finishPosition)==(Slot!=3),"Finish setup occupancy mismatch");walletBefore=DuckzWallet.Instance.Coins;Ack("Three finishers including a downed passenger; fourth left behind");}Advance(23);break;
                case 23:
                    if(!run.State.finished && Slot!=3 && age>1){RailExpedition.Position(PhotonNetwork.LocalPlayer.ActorNumber,out var ridePosition);Require(train.Contains(ridePosition),"Passenger left train before finish: "+train.transform.InverseTransformPoint(ridePosition));}
                    if(PhotonNetwork.IsMasterClient && PhotonSession.Phase==RailPhase.Running)session.SetTrainMoving(true);
                    if(run.State.finished){Require(run.State.success && run.State.players.Count(p=>p.escaped)==3,"Expected three finishers including downed passenger");Require(run.State.players.Count(p=>p.down && p.escaped)==1,"Downed passenger did not retain escape credit");Require(run.results.text.Contains("SUCCESS"),"Success UI missing");Ack("Actual bridge crossing commits success and immutable finishers");}Advance(24);break;
                case 24:
                    int expected=Slot==3?walletBefore:Math.Min(DuckzWallet.MaxCoins,walletBefore+RailResourceBalance.GoldCredit(RailResourceBalance.GoldCount(3),3));
                    if(DuckzWallet.Instance.Coins==expected && age>5){Require(run.State.players.Count(p=>p.escaped)==3,"Catch-up changed finish credit");Ack("Safe pays only escaped players, exact scaled credit");}Advance(25);break;
                case 25:
                    if(PhotonSession.Phase==RailPhase.Staging && Settled){Require(PhotonNetwork.CurrentRoom.Name==c.room && !RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count==0,"Private return leaked objects or changed code");Require(!RailExpedition.LocalDown,"Down state survived return");Ack("Success return: same private code, clean compound");}Advance(26,true);break;
                case 26:
                    if(PhotonNetwork.IsMasterClient && PhotonSession.Phase==RailPhase.Staging)session.StartPrivateRun();
                    if(PhotonSession.Phase==RailPhase.Running && Settled){Require(PhotonSession.RunId!=c.firstRun,"Replay reused run ID");SeedCheck(4);walletBefore=DuckzWallet.Instance.Coins;Board();Ack("Replay: new four-player crew and fresh exact supplies");}Advance(27,true);break;
                case 27:
                    if(Main)Gold(0);if(run.State.SafeBars==1){Require(DuckzWallet.Instance.Coins==walletBefore,"Safe credited early");Ack("Replay safe deposit unpaid");}Advance(28,true);break;
                case 28:
                    Warp(train.transform.position+new Vector3(8+Slot*4,.3f,0));if(age>2 && !RailExpedition.LocalDown)RailExpedition.RequestDown();
                    if(run.State.finished){Require(!run.State.success && run.State.reason=="Crew down","All-down failure missing");Require(run.results.text.Contains("FAIL"),"Failure UI missing");Require(DuckzWallet.Instance.Coins==walletBefore,"Failed safe rewarded coins");Ack("All four down: shared failure and no safe payout");}Advance(29);break;
                case 29:
                    if(PhotonSession.Phase==RailPhase.Staging && Settled){Require(!RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count==0 && !RailExpedition.LocalDown,"Failure cleanup");Require(DuckzWallet.Instance.Coins==walletBefore,"Failure changed earned wallet");Ack("Failure return clean; wallet retained");}Advance(30,true);break;
                case 30:
                    if(PhotonNetwork.IsMasterClient && PhotonSession.Phase==RailPhase.Staging)session.StartPrivateRun();
                    if(PhotonSession.Phase==RailPhase.Running && Settled){SeedCheck(4);Board();Ack("Third fresh run ready");}Advance(31,true);break;
                case 31:
                    if(PhotonNetwork.IsMasterClient && PhotonSession.Phase==RailPhase.Running)MoveTrain(RailRoute.Instance.FinaleStart+80);
                    if(run.State.finished){Require(!run.State.success && run.State.reason=="Bridge collision","Raised-bridge failure missing");Ack("Raised bridge fails shared expedition");}Advance(32);break;
                case 32:
                    if(PhotonSession.Phase==RailPhase.Staging && Settled){Require(!RailWorld.HasLiveCargo && Game.AI.Enemy.Active.Count==0,"Crash cleanup");Ack("Crash return clean");}Advance(33,true);break;
                case 33: Log("PASS COMPLETE private lifecycle, late join, coal, shop/safe wallet, down/revive, enemies, handoff/rejoin, success, two failures and replays");done=true;break;
            }
        }
        catch(IOException) { }
        catch(Exception e){done=true;RailBridgeHoldFixture.SetHeld(false);Log("FAIL "+e.Message);Write(Folder+"/"+Role+".json",JsonUtility.ToJson(new Receipt{token=token,revision=revision,error=e.Message}));}
    }
}
#endif

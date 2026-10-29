using System;
using System.Collections;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GrabbablePlayChecks
{
    const string Key = "GrabbablePlayChecks.Run";
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator checks;
    static double started;
    static GrabbablePlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup; checks = RunChecks(); EditorApplication.update += Step;
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        SessionState.SetBool(Key, true); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Step()
    {
        try { if (EditorApplication.timeSinceStartup - started > 30) throw new Exception("Timeout");
            if (checks.MoveNext()) return; Finish(0);
        } catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Step; EditorApplication.Exit(code); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static IEnumerator RunChecks()
    {
        GR.Player.instance = null;
        var enemyGo = new GameObject("Sweep target"); var enemy = enemyGo.AddComponent<Enemy>();
        typeof(Enemy).GetField("root", P).SetValue(enemy, enemyGo.transform);
        // Verify contact recognition without invoking unrelated enemy knockback simulation.
        typeof(Enemy).GetField("_nextHitTime", P).SetValue(enemy, float.PositiveInfinity);
        var item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>(); typeof(PhotonView).GetProperty("IsMine").SetValue(view, true);
        view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        var handGo = new GameObject("Moving hand"); var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
        item.transform.position = new Vector3(-1, .5f, 0); item.Grab(hand);
        Check(item.IsHeld, "Actual grab"); item.enabled = false; // Drive only the method under test.
        var swing = (Action)Delegate.CreateDelegate(typeof(Action), item, typeof(PhotonGrabbable).GetMethod("TickSwing", P));
        swing(); PhotonGrabbable.ResetProfiling();
        for (int i = 0; i < 10; i++) { yield return null; swing(); }
        Check(PhotonGrabbable.IdleSkips > 0 && !enemy.SwingLocked, "Idle hand does not hit");
        yield return null;
        handGo.transform.position += Vector3.right * 2; Physics.SyncTransforms(); swing();
        Check(enemy.SwingLocked, "First moving frame after idle must hit");
        // Throw still has a valid sweep after sleeping/idle. No IsSleeping gate is used.
        item.Release(Vector3.right * 3, Vector3.zero);
        item.GetComponent<Rigidbody>().isKinematic = true;
        item.transform.position = new Vector3(-1, .5f, 0);
        typeof(PhotonGrabbable).GetField("_hasSamplePrev", P).SetValue(item, false);
        typeof(Enemy).GetField("_localSwingLock", P).SetValue(enemy, 0f); swing();
        yield return null; swing();
        item.transform.position += Vector3.right * 2; Physics.SyncTransforms(); swing();
        Check(enemy.SwingLocked, "Thrown item first movement still hits");
        Debug.Log("GrabbablePlayChecks: ALL PASSED; idle held items do not hit; first held and thrown movement after idle crosses and hits the enemy sweep target");
    }
}

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Edit Mode simulation of the Huggy flare gun's shot path and charge. Run from the menu or with
// -executeMethod HuggyFlareChecks.Run. Live firing, enemy reactions and peers are playtest items.
public static class HuggyFlareChecks
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Duck Rails/Bundles/Check Huggy flare gun")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>("Assets/Prefabs/Effects/HuggyFlare.prefab");
        Require(prefab != null, "HuggyFlare prefab");
        Require(Mathf.Approximately(prefab.BurnSeconds, 15f), "15 s burn");
        Vector3 origin = new Vector3(5000f, 500f, 5000f); // Far from any authored geometry.
        var advance = typeof(DeathFlare).GetMethod("AdvanceShot", Any);
        GameObject wall = null, floor = null;
        DeathFlare open = null, blocked = null;
        try
        {
            // Open air: fast straight flight, stop near the range, then drift down slowly.
            open = DeathFlare.SpawnShot(prefab, origin, Vector3.forward);
            Require(open != null && open.IsShot && open.IsBurning, "Shot spawns burning");
            advance.Invoke(open, new object[] { 0.5f });
            float atHalf = open.transform.position.z - origin.z;
            Require(atHalf > 14f && atHalf < 18f, $"Fast flight, {atHalf:F1} m after 0.5 s");
            advance.Invoke(open, new object[] { 3f });
            Vector3 drift = open.transform.position - origin;
            Require(drift.z > 24f && drift.z < 26.5f, $"Stops near 25 m, at {drift.z:F1} m");
            Require(drift.y < -1f && drift.y > -4f, $"Floats down slowly, {drift.y:F2} m");

            // Wall 6 m ahead and a floor: spark, deflect back, then fall and rest on the floor.
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 6;
            wall.transform.SetPositionAndRotation(origin + new Vector3(0, 0, 6.5f), Quaternion.identity);
            wall.transform.localScale = new Vector3(10, 10, 1);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 6;
            floor.transform.position = origin + new Vector3(0, -2.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
            Physics.SyncTransforms();
            blocked = DeathFlare.SpawnShot(prefab, origin, Vector3.forward);
            advance.Invoke(blocked, new object[] { 6f });
            Vector3 rest = blocked.transform.position - origin;
            Require(rest.z < 6f && rest.z > -3f, $"Deflected by the wall, z {rest.z:F2}");
            Require(rest.y < -1.7f && rest.y > -2.1f, $"Fell to the floor, y {rest.y:F2}");
            Require(blocked.IsBurning, "Still burning on the floor");
        }
        finally
        {
            var pool = DeathEffectPool.Get();
            if (open != null) pool.Release(open.gameObject);
            if (blocked != null) pool.Release(blocked.gameObject);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(pool.gameObject);
        }

        // Charge belongs to the gun: empty right after a shot, full after the cooldown, monotonic.
        var weapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Economy/Bundles/Weapon_Huggy.prefab");
        var gunGo = UnityEngine.Object.Instantiate(weapon);
        try
        {
            var gun = gunGo.GetComponent<HuggyFlareGun>();
            Require(gun.IsCharged, "New gun starts charged");
            int now = Photon.Pun.PhotonNetwork.ServerTimestamp;
            typeof(HuggyFlareGun).GetField("hasShot", Any).SetValue(gun, true);
            typeof(HuggyFlareGun).GetField("lastShotAt", Any).SetValue(gun, now);
            Require(!gun.IsCharged && gun.Charge01 < 0.05f, "Empty after firing");
            typeof(HuggyFlareGun).GetField("lastShotAt", Any).SetValue(gun, now - 5000);
            Require(Mathf.Abs(gun.Charge01 - 0.5f) < 0.05f, $"Half charged after 5 s ({gun.Charge01:F2})");
            typeof(HuggyFlareGun).GetField("lastShotAt", Any).SetValue(gun, now - 10050);
            Require(gun.IsCharged, "Charged after 10 s");
        }
        finally { UnityEngine.Object.DestroyImmediate(gunGo); }
        Debug.Log("HuggyFlareChecks PASS: fast flight, ~25 m stop and slow float, wall deflection to rest, 15 s burn, 10 s gun charge.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("HuggyFlareChecks: " + message);
    }
}

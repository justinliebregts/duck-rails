using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    [SerializeField] bool observePhotonRoomsInEditor;
    public static bool observePhotonRooms;
    public Transform respawnPoint;
    [Tooltip("Duck companion spawn, respawn and stuck-recovery point. Falls back to the player spawn if unassigned.")]
    public Transform companionSpawnPoint;
    [Tooltip("Stops EnemyManager from creating new enemies. Enemies already in the scene keep running.")]
    public bool disableEnemySpawning;
    [Tooltip("Master Client: the only Duck enable switch in every build. Checked removes Duck from the room; unchecked allows automatic spawning and retries.")]
    public bool disableDuckCompanion;
    [HideInInspector] public bool usingDesktopPlayer = false;

    List<PhotonPlayer> photonPlayers = new List<PhotonPlayer>();

    private void Awake() {
        instance = this;
        usingDesktopPlayer = DesktopPlay.UseDesktop;
#if UNITY_EDITOR
        observePhotonRooms = observePhotonRoomsInEditor;
#endif
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        if (usingDesktopPlayer) yield break;
        // Wait until the HMD is actually presenting so the frequency is real
        while (OVRManager.instance == null || !OVRManager.isHmdPresent)
            yield return null;

        float hz = OVRPlugin.systemDisplayFrequency;   // or OVRManager.display.frequency, depending on SDK version
        if(hz != 72) {
            float[] availableFrameRates = OVRManager.display.displayFrequenciesAvailable;
            for(int i = 0; i < availableFrameRates.Length; i++) {
                if(availableFrameRates[i] == 72f) {
                    hz = 72f;
                    OVRPlugin.systemDisplayFrequency = hz;
                }
            }
        }
        if(hz != 0) {
            Time.fixedDeltaTime     = 1f / hz;
            Time.maximumDeltaTime   = Time.fixedDeltaTime * 2f;   // cap catch-up at 2 steps on a dropped frame
        } else {
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write("couldn't get OVRPlugin.systemDisplayFrequency");
            }
        }
    }

    public bool TryGetPlayerSpawnPose(out Vector3 position, out Quaternion rotation)
        => TryGetSpawnPose(respawnPoint, out position, out rotation);

    public bool TryGetCompanionSpawnPose(out Vector3 position, out Quaternion rotation)
        => TryGetSpawnPose(companionSpawnPoint != null ? companionSpawnPoint : respawnPoint, out position, out rotation);

    static bool TryGetSpawnPose(Transform point, out Vector3 position, out Quaternion rotation) {
        position = default;
        rotation = Quaternion.identity;
        if (point == null) return false;
        Vector3 forward = Vector3.ProjectOnPlane(point.forward, Vector3.up);
        rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward, Vector3.up)
            : Quaternion.Euler(0f, point.eulerAngles.y, 0f);
        position = point.position;
        return true;
    }

   public void RespawnPlayer() {
        if (!TryGetPlayerSpawnPose(out Vector3 position, out Quaternion rotation)) return;
        if (usingDesktopPlayer) {
            DesktopPlayer d = GR.Player.instance.GetComponent<DesktopPlayer>();
            d.Teleport(position, rotation, resetLook: true);
        } else {
            NewPlayer.instance.Warp(position, rotation);
        }
        DuckzLog.Log("Respawned");
        GR.Player.instance.IsDead = false;
        GR.Player.instance.playerHUD.DisableDeathEffect();
        GR.Player.instance.ragdoll.gameObject.SetActive(false);
        DuckzAnalytics.MarkAlive();
    }

    public PhotonPlayer GetPhotonPlayer(int actorNumber) {
        for (int i = 0; i < photonPlayers.Count; i++) {
            if (!photonPlayers[i].IsCompanion && photonPlayers[i].photonView.OwnerActorNr == actorNumber) {
                return photonPlayers[i];
            }
        }
        return null;
    }

    public void AddPhotonPlayer(PhotonPlayer p) {
        if (!photonPlayers.Contains(p)) {
            photonPlayers.Add(p);
        }
    }

    public void RemovePhotonPlayer(PhotonPlayer p) {
        if (photonPlayers.Contains(p)) {
            photonPlayers.Remove(p);
        }
    }
}

using ExitGames.Client.Photon;
using Game.Audio;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Room-wide calls that need no PhotonView. Each runs here and is sent to everyone else in the room.
public static class RoomRpc
{
    const byte PlaySoundCode = 101;
    const byte GoldDepositedCode = 102;
    const byte SpawnBagCode = 103;
    const byte SpawnEquipmentCode = 104;

    public static void SpawnEquipment(string key, Vector3 position, Quaternion rotation)
    {
        if (!PhotonNetwork.InRoom) return;
        object[] request = { key, position, rotation };
        if (PhotonNetwork.IsMasterClient)
            SpawnEquipmentAsMaster(request, PhotonNetwork.LocalPlayer.ActorNumber);
        else
            PhotonNetwork.RaiseEvent(SpawnEquipmentCode, request,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    static void SpawnEquipmentAsMaster(object[] request, int actor)
    {
        if (!PhotonNetwork.IsMasterClient || request.Length != 3 ||
            !(request[0] is string key) || !(request[1] is Vector3 position) ||
            !(request[2] is Quaternion rotation)) return;
        Player owner = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        GameObject prefab = BagObject.ResolvePrefab(key);
        if (owner == null || owner.IsInactive || prefab == null || prefab.GetComponent<HeadEquipment>() == null) return;
        PhotonPreparePool.Register(prefab);
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation, 0,
            new object[] { "equipment-restore-v1", actor });
        if (go != null && owner != PhotonNetwork.LocalPlayer)
            go.GetComponent<PhotonView>().TransferOwnership(owner);
    }

    static Game.Audio.AudioSettings settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        settings = null;
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }

    /// <summary>Plays a sound at a position here and for everyone else in the room. Sounds are matched by asset name, so names must be unique.</summary>
    public static void PlaySound(SoundEvent sound, Vector3 position)
    {
        if (sound == null) return;
        Play(sound, position);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(PlaySoundCode, new object[] { sound.name, position },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>Plays the "+N" text and coin shower on the crate nearest the position, here and for everyone else in the room.</summary>
    public static void GoldDeposited(Vector3 cratePosition, int coins)
    {
        int depositor = PhotonNetwork.LocalPlayer.ActorNumber;
        CrateGoldBar.PlayDepositFx(cratePosition, coins, depositor);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(GoldDepositedCode, new object[] { cratePosition, coins, depositor },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>
    /// Asks the master client to spawn a bag as a room object, so it outlives whoever asked for it. Ownership goes to the
    /// local player, whose BagInventory puts it where target says once it arrives. Offline it spawns here directly.
    /// </summary>
    public static void SpawnBag(byte bagId, string[] keys, Vector3 position, Quaternion rotation, BagObject.Target target)
    {
        GameObject prefab = BagInventory.BagPrefab;
        if (prefab == null)
        {
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write("RoomRpc: no bag prefab registered (BagInventory.bagPrefab is unset), can't spawn a bag.");
            }
            return;
        }

        int requester = BagObject.LocalActor;
        int life = BagInventory.Life;
        if (!PhotonNetwork.InRoom)
        {
            GameObject go = Object.Instantiate(prefab, position, rotation);
            go.GetComponent<BagObject>().Init(BagObject.SpawnData(bagId, keys, requester, target, life));
            return;
        }

        object[] request = { bagId, keys ?? new string[0], position, rotation, requester, (byte)target, life, BagInventory.HandClaimEpoch };
        if (PhotonNetwork.IsMasterClient)
            SpawnBagAsMaster(request);
        else
            PhotonNetwork.RaiseEvent(SpawnBagCode, request,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    static void SpawnBagAsMaster(object[] request)
    {
        GameObject prefab = BagInventory.BagPrefab;
        if (prefab == null || !PhotonNetwork.IsMasterClient || request.Length < 6) return;
        if (!(request[0] is byte bagId) || !(request[2] is Vector3 position) || !(request[3] is Quaternion rotation) ||
            !(request[4] is int requester) || !(request[5] is byte target))
            return;

        PhotonPreparePool.Register(prefab);
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation, 0,
            BagObject.SpawnData(bagId, request[1] as string[], requester, (BagObject.Target)target,
                request.Length > 6 && request[6] is int life ? life : -1,
                request.Length > 7 && request[7] is int epoch ? epoch : 0));
        if (go == null) return;

        Player owner = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(requester) : null;
        if (owner != null && owner != PhotonNetwork.LocalPlayer)
            go.GetComponent<PhotonView>().TransferOwnership(owner);
    }

    static void OnEvent(EventData e)
    {
        if (e.Code == SpawnEquipmentCode)
        {
            if (e.CustomData is object[] equipmentRequest)
                SpawnEquipmentAsMaster(equipmentRequest, e.Sender);
            return;
        }
        if (e.Code == SpawnBagCode)
        {
            if (e.CustomData is object[] request)
                SpawnBagAsMaster(request);
            return;
        }
        if (!(e.CustomData is object[] data) || data.Length < 2) return;
        if (e.Code == PlaySoundCode)
        {
            if (data[0] is string name && data[1] is Vector3 position && TryFind(name, out SoundEvent sound))
                Play(sound, position);
        }
        else if (e.Code == GoldDepositedCode)
        {
            if (data[0] is Vector3 cratePosition && data[1] is int coins)
                CrateGoldBar.PlayDepositFx(cratePosition, coins, data.Length > 2 && data[2] is int depositor ? depositor : e.Sender);
        }
    }

    // GameAudioSettings lists every SoundEvent; the editor keeps it current (Assets/Editor/Infrastructure/SoundRegistry.cs).
    static bool TryFind(string name, out SoundEvent sound)
    {
        if (settings == null)
            settings = AudioManager.instance != null && AudioManager.instance.Settings != null
                ? AudioManager.instance.Settings
                : Resources.Load<Game.Audio.AudioSettings>("GameAudioSettings");
        sound = settings != null ? settings.FindSound(name) : null;
        if (sound != null) return true;
        {
            if (DuckzLog.AllowWarning(out var logNotice2))
                logNotice2.Write($"RoomRpc: GameAudioSettings has no SoundEvent named '{name}'.");
        }
        return false;
    }

    static void Play(SoundEvent sound, Vector3 position)
    {
        if (AudioManager.instance != null)
            AudioManager.instance.Play(sound, position);
    }
}

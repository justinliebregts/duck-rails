using System.Collections.Generic;
using Photon.Pun;
using Photon.Voice;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Copies each player's Photon Voice mic into a buffer the walkie speakers read.
/// Remote audio is the stream already sent for proximity chat. Your own mic is copied
/// straight through, so a nearby radio plays you without an added delay.
/// </summary>
[DisallowMultipleComponent]
public class WalkieRadio : MonoBehaviour
{
    public const int SampleRate = 24000;
    public const int LocalDelaySamples = 0;
    const int RemoteDelaySamples = SampleRate * 80 / 1000;
    const int BufferSamples = SampleRate * 2;

    static readonly object stateLock = new object();
    static readonly object publishLock = new object();
    static readonly RadioBuffer localBuffer = new RadioBuffer(LocalDelaySamples, BufferSamples);
    static readonly Dictionary<int, RadioBuffer> remoteByActor = new Dictionary<int, RadioBuffer>();
    static readonly Dictionary<int, int> transmitting = new Dictionary<int, int>();
    static readonly List<RemoteTap> incoming = new List<RemoteTap>();

    static int localActor;
    static TransmitSnapshot published = new TransmitSnapshot();

    PunVoiceClient client;

    public static int LocalActor
    {
        get
        {
            lock (publishLock)
                return published.localActor;
        }
    }

    public struct ReadHead
    {
        public int actor;
        public bool live;
        public bool seen;
        public long cursor;
    }

    void Awake()
    {
        client = GetComponent<PunVoiceClient>();
        if (client != null)
            client.RemoteVoiceAdded += OnRemoteVoiceAdded;
    }

    void OnDestroy()
    {
        if (client != null)
            client.RemoteVoiceAdded -= OnRemoteVoiceAdded;
    }

    void Update()
    {
        bool changed = false;
        int actorNow = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null
            ? PhotonNetwork.LocalPlayer.ActorNumber
            : 0;

        lock (stateLock)
        {
            if (actorNow != localActor)
            {
                localActor = actorNow;
                changed = true;
            }

            for (int i = incoming.Count - 1; i >= 0; i--)
            {
                RemoteTap tap = incoming[i];
                if (tap.Removed)
                {
                    incoming.RemoveAt(i);
                    if (tap.Actor != 0 &&
                        remoteByActor.TryGetValue(tap.Actor, out RadioBuffer buffer) &&
                        buffer == tap.Buffer)
                        remoteByActor.Remove(tap.Actor);
                    changed = true;
                    continue;
                }

                if (tap.Actor != 0 || !(tap.UserData is int viewId))
                    continue;

                PhotonView view = PhotonView.Find(viewId);
                if (view == null)
                    continue;

                tap.Actor = view.OwnerActorNr;
                if (tap.Actor != 0)
                {
                    remoteByActor[tap.Actor] = tap.Buffer;
                    changed = true;
                }
            }
        }

        if (changed)
            Publish();
    }

    // Sent by Recorder to every component on its GameObject.
    void PhotonVoiceCreated(PhotonVoiceCreatedParams parameters)
    {
        if (parameters.Voice is LocalVoiceAudioFloat voice)
            voice.AddPostProcessor(new LocalTap(Mathf.Max(1, voice.Info.Channels)));
    }

    public static void SetTransmitting(int actor, int viewId, bool talking)
    {
        if (viewId == 0)
            return;
        if (talking && actor == 0)
            return;

        lock (stateLock)
        {
            if (talking)
            {
                if (transmitting.TryGetValue(viewId, out int current) && current == actor)
                    return;
                transmitting[viewId] = actor;
            }
            else if (!transmitting.Remove(viewId))
            {
                return;
            }
        }
        Publish();
    }

    public static void ClearView(int viewId)
    {
        if (viewId == 0)
            return;

        bool removed;
        lock (stateLock)
            removed = transmitting.Remove(viewId);
        if (removed)
            Publish();
    }

    public static bool TryTransmitter(int viewId, out int actor)
    {
        lock (publishLock)
        {
            TransmitSnapshot shot = published;
            for (int i = 0; i < shot.viewCount; i++)
            {
                if (shot.viewIds[i] != viewId)
                    continue;
                actor = shot.viewActors[i];
                return true;
            }
        }

        actor = 0;
        return false;
    }

    /// <summary>Mixes every player who is holding a walkie button, except excludeActor. 0 excludes nobody.</summary>
    public static void Mix(float[] dst, int excludeActor, ReadHead[] heads, float[] scratch)
    {
        for (int i = 0; i < dst.Length; i++)
            dst[i] = 0f;
        for (int i = 0; i < heads.Length; i++)
            heads[i].seen = false;

        TransmitSnapshot shot;
        lock (publishLock)
            shot = published;

        for (int i = 0; i < shot.count; i++)
        {
            int actor = shot.actors[i];
            RadioBuffer buffer = shot.buffers[i];
            if (buffer == null || (excludeActor != 0 && actor == excludeActor))
                continue;

            int slot = Slot(heads, actor);
            if (slot < 0)
                continue;

            ReadHead head = heads[slot];
            head.seen = true;
            if (!head.live)
            {
                head.cursor = buffer.Written;
                head.live = true;
            }
            buffer.Read(ref head.cursor, scratch, dst.Length);
            heads[slot] = head;

            for (int s = 0; s < dst.Length; s++)
                dst[s] += scratch[s];
        }

        for (int i = 0; i < heads.Length; i++)
        {
            if (!heads[i].seen)
                heads[i].live = false;
        }
    }

    public static void BindSlot(BagSlot slot)
    {
        if (slot == null)
            return;

        Transform existing = slot.transform.Find("WalkieSpeaker");
        if (slot.ItemKey != WalkieTalkie.PrefabName && slot.ItemKey != "RailWalkie")
        {
            if (existing != null)
                UnityEngine.Object.Destroy(existing.gameObject);
            return;
        }

        if (existing != null)
            return;

        var go = new GameObject("WalkieSpeaker");
        go.transform.SetParent(slot.transform, false);
        WalkieSpeaker speaker = go.AddComponent<WalkieSpeaker>();
        speaker.Configure(0, true);
    }

    public static void UnbindSlot(BagSlot slot)
    {
        if (slot == null)
            return;

        Transform existing = slot.transform.Find("WalkieSpeaker");
        if (existing != null)
            UnityEngine.Object.Destroy(existing.gameObject);
    }

    static int Slot(ReadHead[] heads, int actor)
    {
        int free = -1;
        for (int i = 0; i < heads.Length; i++)
        {
            if (heads[i].actor == actor)
                return i;
            if (free < 0 && !heads[i].live)
                free = i;
        }

        if (free < 0)
            return -1;

        heads[free].actor = actor;
        heads[free].live = false;
        heads[free].cursor = 0;
        return free;
    }

    void OnRemoteVoiceAdded(RemoteVoiceLink link)
    {
        if (link == null)
            return;

        var tap = new RemoteTap
        {
            Buffer = new RadioBuffer(RemoteDelaySamples, BufferSamples),
            Channels = Mathf.Max(1, link.VoiceInfo.Channels),
            UserData = link.VoiceInfo.UserData
        };
        link.FloatFrameDecoded += frame => OnRemoteFrame(tap, frame);
        link.RemoteVoiceRemoved += () => tap.Removed = true;

        lock (stateLock)
            incoming.Add(tap);
    }

    static void OnRemoteFrame(RemoteTap tap, FrameOut<float> frame)
    {
        if (tap.Removed || frame == null || frame.Buf == null || frame.Buf.Length == 0)
            return;
        tap.Buffer.Write(frame.Buf, tap.Channels);
    }

    static void Publish()
    {
        int[] viewIds;
        int[] viewActors;
        int[] actors;
        RadioBuffer[] buffers;
        int actorNow;

        lock (stateLock)
        {
            actorNow = localActor;
            viewIds = new int[transmitting.Count];
            viewActors = new int[transmitting.Count];
            var unique = new List<int>(transmitting.Count);
            int n = 0;
            foreach (KeyValuePair<int, int> pair in transmitting)
            {
                viewIds[n] = pair.Key;
                viewActors[n] = pair.Value;
                n++;
                if (!unique.Contains(pair.Value))
                    unique.Add(pair.Value);
            }

            actors = unique.ToArray();
            buffers = new RadioBuffer[actors.Length];
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i] == actorNow)
                    buffers[i] = localBuffer;
                else
                    remoteByActor.TryGetValue(actors[i], out buffers[i]);
            }
        }

        lock (publishLock)
        {
            published = new TransmitSnapshot
            {
                viewIds = viewIds,
                viewActors = viewActors,
                viewCount = viewIds.Length,
                actors = actors,
                buffers = buffers,
                count = actors.Length,
                localActor = actorNow
            };
        }
    }

    sealed class TransmitSnapshot
    {
        public int[] viewIds = System.Array.Empty<int>();
        public int[] viewActors = System.Array.Empty<int>();
        public int viewCount;
        public int[] actors = System.Array.Empty<int>();
        public RadioBuffer[] buffers = System.Array.Empty<RadioBuffer>();
        public int count;
        public int localActor;
    }

    sealed class RemoteTap
    {
        public RadioBuffer Buffer;
        public int Channels;
        public object UserData;
        public int Actor;
        public volatile bool Removed;
    }

    sealed class LocalTap : IProcessor<float>
    {
        readonly int channels;

        public LocalTap(int channels) => this.channels = channels;

        public float[] Process(float[] buf)
        {
            if (PhotonVoiceChat.IsLocallyMuted)
                return buf;
            if (buf != null && buf.Length > 0)
                localBuffer.Write(buf, channels);
            return buf;
        }

        public void Dispose() { }
    }

    sealed class RadioBuffer
    {
        readonly float[] data;
        readonly int delay;
        readonly object gate = new object();
        long written;

        public RadioBuffer(int delaySamples, int capacity)
        {
            delay = Mathf.Max(0, delaySamples);
            data = new float[Mathf.Max(1, capacity)];
        }

        public long Written
        {
            get
            {
                lock (gate)
                    return written;
            }
        }

        public void Write(float[] src, int channels)
        {
            if (src == null || src.Length == 0)
                return;
            if (channels < 1)
                channels = 1;

            lock (gate)
            {
                if (channels == 1)
                {
                    for (int i = 0; i < src.Length; i++)
                        Push(src[i]);
                    return;
                }

                for (int i = 0; i + channels <= src.Length; i += channels)
                {
                    float sample = 0f;
                    for (int c = 0; c < channels; c++)
                        sample += src[i + c];
                    Push(sample / channels);
                }
            }
        }

        public void Read(ref long cursor, float[] dst, int count)
        {
            lock (gate)
            {
                long limit = written - delay;
                if (limit < 0)
                    limit = 0;
                if (cursor < written - data.Length)
                    cursor = limit;

                int n = 0;
                while (n < count && cursor < limit)
                {
                    dst[n++] = data[(int)(cursor % data.Length)];
                    cursor++;
                }
                for (; n < count; n++)
                    dst[n] = 0f;
            }
        }

        void Push(float sample)
        {
            data[(int)(written % data.Length)] = sample;
            written++;
        }
    }
}

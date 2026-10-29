using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Photon.Voice;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>One local microphone tap, shared with Photon Voice. Vosk inference stays off the Unity thread.</summary>
public sealed class DuckOfflineRecognition : MonoBehaviour
{
    [Serializable] class Result { public string text = ""; }
    static DuckOfflineRecognition instance;
    readonly struct AudioFrame
    {
        public readonly float[] Samples;
        public readonly int Count;
        public AudioFrame(float[] samples, int count) { Samples = samples; Count = count; }
    }
    readonly ConcurrentQueue<AudioFrame> frames = new ConcurrentQueue<AudioFrame>();
    readonly object frameGate = new object();
    DuckCompanion cachedDuck;
    Recorder cachedRecorder;
    float nextReferenceSearch;
    static readonly string OutOfRangeStatus = $"Move within {DuckCompanionDialogue.ConversationRange:0} metres of Duck";
    string inputFormat = "Not attached";
    int displayedRate, displayedChannels;
    readonly ConcurrentQueue<string> results = new ConcurrentQueue<string>();
    volatile bool quit, listen;
    volatile string status = "Loading offline recognition";
    Task worker;
    float conversationUntil;
    [SerializeField, Range(1f, 8f)] float recognitionGain = 1f;
    volatile float tapGain = 1f;
    int receivedFrames;
    int lastReceivedFrames;
    float lastAudioAt;
    volatile int inputRate, inputChannels;
    public string Status => status;
    public string ListeningStatus { get; private set; } = "Waiting for microphone";
    public int ReceivedFrames => Volatile.Read(ref receivedFrames);
    public string InputFormat => inputFormat;
    public float RecognitionGain { get => recognitionGain; set => recognitionGain = Mathf.Clamp(value, 1f, 8f); }
    public static DuckOfflineRecognition Instance => instance;
    public float MicLevel { get; private set; }
    public string LastHeard { get; private set; } = "";

    public static void Ensure()
    {
        if (instance != null) return;
        PhotonVoiceChat chat = FindAnyObjectByType<PhotonVoiceChat>();
        if (chat == null) return;
        instance = chat.gameObject.AddComponent<DuckOfflineRecognition>();
        // The existing local voice may predate this tap; republish its processor chain once.
        Recorder recorder = chat.GetComponent<Recorder>();
        if (recorder != null && recorder.RecordingEnabled) recorder.RestartRecording();
    }

    void Awake() => cachedRecorder = GetComponent<Recorder>();

    IEnumerator Start()
    {
        string folder = Path.Combine(Application.persistentDataPath, "DuckRecognition-en-us-015");
        string model = Path.Combine(folder, "vosk-model-small-en-us-0.15");
        string marker = Path.Combine(folder, "complete");
        if (!File.Exists(marker))
        {
            Directory.CreateDirectory(folder);
            string zip = Path.Combine(folder, "model.zip");
            string assetPath = Application.streamingAssetsPath + "/DuckSpeech/recognition.zip";
#if !UNITY_ANDROID || UNITY_EDITOR
            assetPath = new Uri(assetPath).AbsoluteUri;
#endif
            using (var request = UnityWebRequest.Get(assetPath))
            {
                // Only reads an APK asset or local file; this URL never points at a server.
                request.downloadHandler = new DownloadHandlerFile(zip);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { status = request.error; yield break; }
            }
            Task extract = Task.Run(() =>
            {
                using (ZipArchive archive = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destination = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                        if (!destination.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            throw new InvalidDataException("Invalid recognition archive path.");
                        if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        entry.ExtractToFile(destination, true);
                    }
                }
                File.WriteAllText(marker, "vosk-model-small-en-us-0.15");
            });
            while (!extract.IsCompleted) yield return null;
            if (extract.IsFaulted) { status = extract.Exception.GetBaseException().Message; yield break; }
        }
        worker = Task.Run(() => Recognize(model));
    }

    void Update()
    {
        // Search only on spawn/respawn, not every render frame. Keep listening
        // gates frame-accurate so mute and Duck speech immediately stop the tap.
        if ((cachedDuck == null || cachedRecorder == null) && Time.unscaledTime >= nextReferenceSearch)
        {
            nextReferenceSearch = Time.unscaledTime + 0.5f;
            if (cachedDuck == null) cachedDuck = FindAnyObjectByType<DuckCompanion>();
            if (cachedRecorder == null) cachedRecorder = GetComponent<Recorder>();
        }
        DuckCompanion duck = cachedDuck;
        PhotonPlayer player = PhotonPlayer.Local;
        Recorder recorder = cachedRecorder;
        int rate = inputRate, channels = inputChannels;
        if (rate != displayedRate || channels != displayedChannels)
        {
            displayedRate = rate; displayedChannels = channels;
            inputFormat = rate > 0 ? $"{rate} Hz / {channels} ch" : "Not attached";
        }
        MicLevel = recorder?.LevelMeter != null ? recorder.LevelMeter.CurrentAvgAmp : 0;
        tapGain = recognitionGain;
        int count = ReceivedFrames;
        if (count != lastReceivedFrames) { lastReceivedFrames = count; lastAudioAt = Time.unscaledTime; }
        bool canListen = false;
        // Half-duplex on speakers: avoid recognizing Duck's own synthetic reply as a new player turn.
        if (recorder == null || !recorder.RecordingEnabled) ListeningStatus = "Microphone recording is off";
        else if (PhotonVoiceChat.IsLocallyMuted) ListeningStatus = "Microphone is muted";
        else if (duck == null) ListeningStatus = "Waiting for Duck";
        else if (duck.IsDead) ListeningStatus = "Waiting for Duck to respawn";
        else if (player == null || player.IsDead) ListeningStatus = "Waiting for your living player";
        else if (!player.TryGetHeadPosition(out Vector3 head)
            || (head - duck.Head.position).sqrMagnitude > DuckCompanionDialogue.ConversationRange * DuckCompanionDialogue.ConversationRange)
            ListeningStatus = OutOfRangeStatus;
        else if (duck.Dialogue.Busy) ListeningStatus = "Paused while Duck prepares or speaks his reply";
        else
        {
            canListen = true;
            ListeningStatus = duck.Dialogue.ChatterPaused
                ? "Listening: say follow me, stay with me, or address Duck to chat"
                : duck.Dialogue.IsNearbyBuddy(player) ? "Listening: talk to your buddy, then pause"
                : "Listening: say Duck, then speak and pause";
        }
        listen = canListen;
        if (listen && (count == 0 || Time.unscaledTime - lastAudioAt > 2f))
            ListeningStatus = "Waiting for microphone audio (check input device/permission)";
        // Bound result dispatch during a backlog; do not parse discarded speech.
        for (int processed = 0; processed < 4 && results.TryDequeue(out string json); processed++)
        {
            if (!listen || duck == null || duck.Dialogue.Busy) continue;
            Result result;
            try { result = JsonUtility.FromJson<Result>(json); } catch { continue; }
            if (!listen || string.IsNullOrWhiteSpace(result?.text)) continue;
            LastHeard = result.text;
            if (!duck.CanHearVisitor && !DuckCompanionDialogue.AcceptsSpeech(result.text, duck.Dialogue.IsNearbyBuddy(player),
                conversationUntil > 0 && Time.time <= conversationUntil)) continue;
            conversationUntil = Time.time + 60f;
            duck.Dialogue.SubmitLocal(result.text);
        }
    }

    void PhotonVoiceCreated(PhotonVoiceCreatedParams parameters)
    {
        if (parameters?.AudioDesc == null) return;
        // Pre-processors receive the microphone's original format before Photon resamples
        // and removes quiet frames. Vosk needs those pauses to finalize an utterance.
        int rate = parameters.AudioDesc.SamplingRate;
        int channels = parameters.AudioDesc.Channels;
        inputRate = rate;
        inputChannels = channels;
        if (parameters.Voice is LocalVoiceAudioFloat voice)
            voice.AddPreProcessor(new Tap(this, rate, channels));
        else if (parameters.Voice is LocalVoiceAudioShort shortVoice)
            shortVoice.AddPreProcessor(new ShortTap(this, rate, channels));
    }

    sealed class Tap : IProcessor<float>
    {
        readonly DuckOfflineRecognition owner;
        readonly int rate, channels;
        public Tap(DuckOfflineRecognition owner, int rate, int channels) { this.owner = owner; this.rate = rate; this.channels = Math.Max(1, channels); }
        public float[] Process(float[] input)
        {
            if (owner.quit || input == null || rate <= 0) return input;
            Interlocked.Increment(ref owner.receivedFrames);
            if (!owner.listen || owner.frames.Count >= 100) return input;
            int sourceSamples = input.Length / channels;
            int count = sourceSamples * 16000 / rate;
            if (count == 0) return input;
            var mono = System.Buffers.ArrayPool<float>.Shared.Rent(count);
            for (int i = 0; i < count; i++)
            {
                float at = i * rate / 16000f;
                int a = Math.Min((int)at, sourceSamples - 1), b = Math.Min(a + 1, sourceSamples - 1);
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += input[a * channels + c] + (input[b * channels + c] - input[a * channels + c]) * (at - a);
                // Gain applies only to Duck's private recognition copy, never multiplayer voice.
                mono[i] = Math.Max(-1f, Math.Min(1f, sum / channels * owner.tapGain)) * 32767f;
            }
            lock (owner.frameGate)
            {
                if (owner.quit || !owner.listen) System.Buffers.ArrayPool<float>.Shared.Return(mono);
                else owner.frames.Enqueue(new AudioFrame(mono, count));
            }
            return input;
        }
        public void Dispose() { }
    }

    sealed class ShortTap : IProcessor<short>
    {
        readonly Tap tap;
        readonly DuckOfflineRecognition owner;
        float[] normalized;
        public ShortTap(DuckOfflineRecognition owner, int rate, int channels) { this.owner = owner; tap = new Tap(owner, rate, channels); }
        public short[] Process(short[] input)
        {
            if (input == null) return input;
            if (owner.quit) return input;
            if (!owner.listen || owner.frames.Count >= 100)
            {
                Interlocked.Increment(ref owner.receivedFrames);
                return input;
            }
            // Photon serializes calls per processor. Reuse its format-sized
            // conversion buffer; the float tap copies before returning.
            if (normalized == null || normalized.Length != input.Length) normalized = new float[input.Length];
            for (int i = 0; i < input.Length; i++) normalized[i] = input[i] / 32768f;
            tap.Process(normalized);
            return input;
        }
        public void Dispose() => tap.Dispose();
    }

    void Recognize(string path)
    {
        IntPtr model = IntPtr.Zero, recognizer = IntPtr.Zero;
        bool wasListening = false;
        try
        {
            Native.vosk_set_log_level(-1);
            model = Native.vosk_model_new(path);
            if (model == IntPtr.Zero) throw new IOException("Offline speech model could not load.");
            recognizer = Native.vosk_recognizer_new(model, 16000);
            if (recognizer == IntPtr.Zero) throw new IOException("Offline recognizer could not start.");
            status = "Ready: say Duck, then speak";
            while (!quit)
            {
                if (!listen)
                {
                    while (frames.TryDequeue(out AudioFrame discarded)) System.Buffers.ArrayPool<float>.Shared.Return(discarded.Samples);
                    if (wasListening) Native.vosk_recognizer_reset(recognizer);
                    wasListening = false;
                    Thread.Sleep(20);
                    continue;
                }
                wasListening = true;
                if (!frames.TryDequeue(out AudioFrame frame)) { Thread.Sleep(10); continue; }
                int accepted;
                try { accepted = Native.vosk_recognizer_accept_waveform_f(recognizer, frame.Samples, frame.Count); }
                finally { System.Buffers.ArrayPool<float>.Shared.Return(frame.Samples); }
                if (accepted != 0)
                {
                    IntPtr ptr = Native.vosk_recognizer_result(recognizer);
                    if (ptr == IntPtr.Zero) continue;
                    int length = 0;
                    while (length < 65536 && Marshal.ReadByte(ptr, length) != 0) length++;
                    byte[] bytes = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        Marshal.Copy(ptr, bytes, 0, length);
                        results.Enqueue(Encoding.UTF8.GetString(bytes, 0, length));
                    }
                    finally { System.Buffers.ArrayPool<byte>.Shared.Return(bytes); }
                }
            }
        }
        catch (Exception ex) { status = "Recognition unavailable: " + ex.Message; }
        finally
        {
            StopFrames();
            if (recognizer != IntPtr.Zero) Native.vosk_recognizer_free(recognizer);
            if (model != IntPtr.Zero) Native.vosk_model_free(model);
        }
    }

    void StopFrames()
    {
        lock (frameGate)
        {
            quit = true;
            listen = false;
            while (frames.TryDequeue(out AudioFrame frame)) System.Buffers.ArrayPool<float>.Shared.Return(frame.Samples);
        }
    }

    void OnDestroy() { StopFrames(); if (instance == this) instance = null; }

    static class Native
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string Library = "vosk";
#else
        const string Library = "libvosk";
#endif
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void vosk_set_log_level(int level);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr vosk_model_new([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void vosk_model_free(IntPtr model);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_new(IntPtr model, float rate);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void vosk_recognizer_free(IntPtr recognizer);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void vosk_recognizer_reset(IntPtr recognizer);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int vosk_recognizer_accept_waveform_f(IntPtr recognizer, float[] data, int count);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
    }
}

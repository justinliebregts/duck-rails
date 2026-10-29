using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class DuckVoiceSettingsChecks
{
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Debug.Log("VOICE SETTINGS PASS: " + label);
    }
    public static void Run()
    {
        var baseline = (AnimationCurve)typeof(PhotonVoiceChat).GetMethod("CloseBoostedRolloff", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var settings = new DuckCompanionVoiceSettings();
        for (int i = 0; i <= 100; i++)
            Check(Mathf.Abs(settings.customRolloff.Evaluate(i / 100f) - baseline.Evaluate(i / 100f)) < .00001f, "default curve matches at " + i);
        var a = new GameObject("Companion peer A");
        var b = new GameObject("Companion peer B");
        var human = new GameObject("Human player");
        try
        {
            var duckA = a.AddComponent<DuckCompanion>();
            var duckB = b.AddComponent<DuckCompanion>();
            // Identical serialized prefab settings on independent peers; no master required.
            duckA.voiceSettings.volume = .42f;
            duckA.voiceSettings.pitch = .85f;
            duckA.voiceSettings.spatialBlend = .8f;
            duckA.voiceSettings.minDistance = 3f;
            duckA.voiceSettings.maxDistance = 22f;
            duckA.voiceSettings.rolloffMode = AudioRolloffMode.Linear;
            duckB.voiceSettings = JsonUtility.FromJson<DuckCompanionVoiceSettings>(JsonUtility.ToJson(duckA.voiceSettings));
            var sourceA = PhotonVoiceChat.Attach(a, null, true);
            var sourceB = PhotonVoiceChat.Attach(b, null, true);
            Check(Mathf.Approximately(sourceA.volume, .42f) && Mathf.Approximately(sourceA.spatialBlend, .8f)
                && sourceA.minDistance == 3f && sourceA.maxDistance == 22f && sourceA.rolloffMode == AudioRolloffMode.Linear, "all companion settings applied at creation");
            Check(sourceA.volume == sourceB.volume && sourceA.maxDistance == sourceB.maxDistance
                && sourceA.rolloffMode == sourceB.rolloffMode, "independent peers apply same serialized settings");
            Check(Mathf.Approximately(sourceA.pitch, .85f) && sourceA.pitch == sourceB.pitch,
                "custom pitch applied on independent peers");
            var dialogueA = a.AddComponent<DuckCompanionDialogue>();
            var dialogueB = b.AddComponent<DuckCompanionDialogue>();
            var duckField = typeof(DuckCompanionDialogue).GetField("duck", BindingFlags.Instance | BindingFlags.NonPublic);
            duckField.SetValue(dialogueA, duckA); duckField.SetValue(dialogueB, duckB);
            Check(dialogueA.VoicePitch == sourceA.pitch && dialogueB.VoicePitch == sourceB.pitch,
                "network timestamp seek uses the same pitch as playback on both peers");
            dialogueA.CancelSpeech();
            Check(dialogueA.VoicePitch == .85f, "cancel preserves configured pitch");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var startLine = typeof(DuckCompanionDialogue).GetMethod("StartLine", flags);
            var sequence = typeof(DuckCompanionDialogue).GetField("sequence", flags);
            var scheduledPitch = typeof(DuckCompanionDialogue).GetField("scheduledPitch", flags);
            var scheduledStart = typeof(DuckCompanionDialogue).GetField("scheduledStart", flags);
            sequence.SetValue(dialogueA, 77); sequence.SetValue(dialogueB, 77);
            foreach (var dialogue in new[] { dialogueA, dialogueB })
                startLine.Invoke(dialogue, new object[] {77, 100.0, default(Photon.Pun.PhotonMessageInfo)});
            Check((float)scheduledPitch.GetValue(dialogueA) == .85f && (float)scheduledPitch.GetValue(dialogueB) == .85f,
                "matching prefab settings produce identical local pitch snapshots without an RPC pitch parameter");
            duckA.voiceSettings.pitch = 1.5f;
            Check((float)scheduledPitch.GetValue(dialogueA) == .85f, "scheduled pitch remains stable after local edits");
            startLine.Invoke(dialogueB, new object[] {76, 200.0, default(Photon.Pun.PhotonMessageInfo)});
            Check((double)scheduledStart.GetValue(dialogueB) == 100.0 && (float)scheduledPitch.GetValue(dialogueB) == .85f,
                "stale messages do not replace accepted schedule");
            sequence.SetValue(dialogueA, 78);
            startLine.Invoke(dialogueA, new object[] {78, 200.0, default(Photon.Pun.PhotonMessageInfo)});
            Check((float)scheduledPitch.GetValue(dialogueA) == 1.5f, "next line captures updated local pitch");
            dialogueB.CancelSpeech();
            Check((double)scheduledStart.GetValue(dialogueB) < 0, "cancellation invalidates old schedule for takeover/respawn");
            // Even an accidentally attached config must not affect the human branch.
            var accidental = human.AddComponent<DuckCompanion>();
            accidental.voiceSettings.volume = .01f;
            var player = PhotonVoiceChat.Attach(human, null, false);
            Check(player.pitch == 1f && player.volume == 1f && player.spatialBlend == 1f && player.minDistance == 1.5f
                && player.maxDistance == 40f && player.rolloffMode == AudioRolloffMode.Custom, "human branch preserves existing voice defaults");
            var humanCurve = player.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            for (int i = 0; i <= 20; i++) Check(Mathf.Abs(humanCurve.Evaluate(i / 20f) - baseline.Evaluate(i / 20f)) < .00001f, "human curve unchanged " + i);
            var output = sourceA.outputAudioMixerGroup;
            sourceA.pitch = 1.1f;
            settings.Apply(sourceA);
            Check(sourceA.pitch == 1.1f && sourceA.outputAudioMixerGroup == output, "default pitch and mixer routing preserved");
            settings.pitch = 0f; settings.Apply(sourceA);
            Check(sourceA.pitch > 0f, "zero pitch cannot stall dialogue forever");
            settings.pitch = float.NaN; settings.Apply(sourceA);
            Check(sourceA.pitch == 1.1f, "non-finite pitch falls back to default");
            settings.minDistance = -1f; settings.maxDistance = -2f; settings.Apply(sourceA);
            Check(sourceA.minDistance > 0f && sourceA.maxDistance > sourceA.minDistance, "invalid distance values constrained");
        }
        finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(human); }
        Debug.Log("ALL VOICE SETTINGS CHECKS PASSED");
    }
}

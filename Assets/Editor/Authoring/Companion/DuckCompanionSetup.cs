using System;
using System.IO;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class DuckCompanionSetup
{
    const string PrefabPath = "Assets/Resources/DuckCompanion.prefab";

    [MenuItem("Tools/DuckZ/Authoring/Companion/Create or Validate Prefab")]
    public static void Prepare()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
            if (original == null) throw new InvalidOperationException("PhotonPlayer prefab missing.");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            try
            {
                instance.name = "DuckCompanion";
                instance.AddComponent<DuckCompanion>();
                // Share the player emitter's authored settings and sound assets.
                var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DesktopPlayer.prefab");
                var playerSteps = player != null ? player.GetComponent<FootstepEmitter>() : null;
                if (playerSteps == null) throw new InvalidOperationException("Player FootstepEmitter missing.");
                EditorUtility.CopySerialized(playerSteps, instance.GetComponent<FootstepEmitter>());
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab.GetComponent<DuckCompanion>() == null || prefab.GetComponent<PhotonPlayer>() == null)
            throw new InvalidOperationException("Companion components missing.");
        if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant)
            throw new InvalidOperationException("Companion must inherit the shared PhotonPlayer rig.");
        var footsteps = prefab.GetComponent<FootstepEmitter>();
        if (footsteps == null || new SerializedObject(footsteps).FindProperty("sounds").objectReferenceValue
            != AssetDatabase.LoadAssetAtPath<SurfaceSoundSet>("Assets/Audio/SurfaceSoundSets/Player_Footsteps.asset"))
            throw new InvalidOperationException("Companion must use the shared player FootstepEmitter and sound set.");
        Debug.Log("[Duck checks] Companion prefab variant is valid.");
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/DuckZ/Validation/Companion/Validate Offline Speech and Rules")]
    public static void Validate()
    {
        Prepare();
        ValidateDialogueBank();
        Check(DuckCompanionDialogue.Intent("Duck, stop following me and wait here") == "dismiss", "dismiss command precedence");
        Check(DuckCompanionDialogue.Intent("Duck, how do I unlock a weapon?") == "weapons", "weapon intent");
        Check(DuckCompanionDialogue.Intent("Duck, tell me a joke") == "jokes", "joke intent");
        Check(DuckCompanionDialogue.Intent("Duck, this is scary") == "unknown", "partial words do not match hi or car");
        Check(DuckCompanionDialogue.Intent("weapons") == "weapons" && DuckCompanionDialogue.Intent("hi") == "greeting", "whole-word topics and greetings");
        Check(DuckCompanionDialogue.AcceptsSpeech("purple pancakes", true, false), "nearby buddy can speak without a wake word");
        Check(!DuckCompanionDialogue.AcceptsSpeech("purple pancakes", false, false), "unaddressed bystander speech is ignored");
        Check(DuckCompanionDialogue.AcceptsSpeech("purple pancakes", false, true), "open conversation accepts follow-up speech");
        Check(DuckCompanion.TryJumpDuration(new Vector3(3, 0, 0), 8.66f, 20, 6, out float duration) && duration > 0, "reachable jump");
        Check(!DuckCompanion.TryJumpDuration(new Vector3(0, 5, 0), 8.66f, 20, 6, out _), "unreachable height rejected");
        Check(!DuckCompanion.TryJumpDuration(new Vector3(20, 0, 0), 8.66f, 20, 6, out _), "unreachable gap rejected");
        DuckCompanionDialogue.Bank bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        var airborne = Array.Find(bank.groups, group => group.intent == "airborne");
        Check(airborne != null && airborne.lines.Length == 20
            && new System.Collections.Generic.HashSet<string>(airborne.lines).Count == 20, "twenty unique airborne reactions");
        int total = 0;
        foreach (var group in bank.groups)
        {
            Check(group.intent == "hurt" ? group.lines.Length > 0 : group.lines.Length >= 4, "variation for " + group.intent);
            foreach (string line in group.lines) { Check(line.Length <= 240, "line length"); total++; }
        }
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using (var engine = new DuckTtsCore(Path.Combine(Application.streamingAssetsPath, DuckTtsCore.AssetFolder)))
        {
            Debug.Log("[Duck checks] Voice loaded in " + clock.Elapsed.TotalSeconds.ToString("0.00") + " seconds.");
            string phraseTable = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, DuckTtsCore.AssetFolder, "phrases.tsv"));
            foreach (var group in bank.groups)
                foreach (string line in group.lines)
                {
                    Check(phraseTable.Contains(line.Replace('’', '\'') + "\t"), "prepared Kokoro pronunciation: " + line);
                    string phonemes = engine.Phonemize(line);
                    Check(phonemes.Length > 0 && phonemes.Length <= 510, "Kokoro context length");
                }
            clock.Restart();
            float[] samples = engine.Generate("Hello! I'm Duck. Let's find some gold and keep each other safe.");
            Check(samples.Length > 24000, "speech has meaningful duration");
            double energy = 0;
            foreach (float sample in samples) { Check(!float.IsNaN(sample), "finite audio"); energy += sample * sample; }
            Check(energy / samples.Length > 0.00001, "audible speech energy");
            Directory.CreateDirectory(".utmp/duck-speech");
            SaveWav(".utmp/duck-speech/duck-sample.wav", samples);
            Debug.Log($"[Duck checks] {total} dialogue lines; generated {samples.Length / 24000f:0.00}s of offline audio in {clock.Elapsed.TotalSeconds:0.00}s. All checks passed.");
        }
    }

    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Duck validation failed: " + description);
    }

    [MenuItem("Tools/DuckZ/Validation/Companion/Validate Dialogue Bank")]
    public static void ValidateDialogueBank()
    {
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        var intents = new System.Collections.Generic.HashSet<string>();
        var unique = new System.Collections.Generic.HashSet<string>();
        int total = 0;
        foreach (var group in bank.groups)
        {
            Check(intents.Add(group.intent), "unique intent " + group.intent);
            foreach (string line in group.lines)
            {
                Check(!string.IsNullOrWhiteSpace(line) && line.Length <= 240 && unique.Add(line), "unique, bounded line in " + group.intent);
                total++;
            }
        }
        Check(total >= 717, "expanded bank contains at least 717 lines");
        Check(Array.Find(bank.groups, g => g.intent == "wander").lines.Length >= 30, "thirty scouting departure lines");
        Check(Array.Find(bank.groups, g => g.intent == "enemy_warning").lines.Length >= 30, "thirty nearby enemy warnings");
        Check(Array.Find(bank.groups, g => g.intent == "hurt").lines.Length >= 31, "thirty added hit reactions plus ouch");
        Check(Array.Find(bank.groups, g => g.intent == "follow_return").lines.Length >= 30, "thirty happy return replies");
        Check(Array.Find(bank.groups, g => g.intent == "helicopter_arrival").lines.Length >= 50, "fifty helicopter arrival lines");
        Check(Array.Find(bank.groups, g => g.intent == "dismiss").lines.Length >= 30, "thirty dismissal farewells");
        string[,] cases = {
            { "are you an imposter", "creepy" }, { "forever buddy", "creepy" }, { "watching people sleep", "creepy" },
            { "tell me about the underground cave", "caves" }, { "cave bats", "caves" },
            { "cinema", "cinema" }, { "apartments", "apartments" }, { "factories", "factory" },
            { "warehouse", "warehouse" }, { "subway", "subway" }, { "local store", "store" },
            { "knowing the sheriff", "sheriff" }, { "your childhood hero Rambro", "rambro_memories" },
            { "Rambro knife", "rambro_knife" }, { "aunt by the creek and bridge", "river" },
            { "my friend's uncle runs the gas station", "gas_station" }, { "church bell", "church" },
            { "barn animals", "barn" }, { "helicopter supplies to the remote town", "helicopter_memories" },
            { "bags of marsh mellows at the cabins", "woods" }, { "Quackville", "town" },
            { "can you hear me?", "acknowledge" }, { "mic check", "acknowledge" }, { "Duck, go away", "dismiss" }, { "stop following me duck", "dismiss" },
            { "shut up duck", "quiet" }, { "be quiet duck", "quiet" }, { "Duck, wait here", "wait" },
            { "follow me again", "follow" }, { "stay with me", "follow" }, { "stay with me duck", "follow" },
            { "resume following me", "follow" }, { "stick with me", "follow" }, { "come along", "follow" },
            { "don't follow me", "dismiss" }, { "do not follow me", "dismiss" },
            { "Duck, follow me to the helicopter", "follow" }, { "tell me a chopper joke", "helicopter" },
            { "tell me about the nail bat weapon", "nail_bat" }, { "fry pan against zombies", "fry_pan" },
            { "cooking delicious duck food", "creepy_food" }, { "night-vision goggles tips", "night_vision" },
            { "how do I use my flashlight", "flashlight" }, { "buy a helmet", "helmet" },
            { "walkie-talkie", "walkie_talkie" }, { "Rambro knife", "rambro_knife" },
            { "ninja knife weapon", "ninja_knife" }, { "bags that hold stuff", "bags" },
            { "getting rich with gold bars", "gold" }, { "staying alive", "survival" },
            { "where did you grow up", "town" }, { "miss your old school", "school" },
            { "used to work at the factory", "factory" }, { "the store you went shopping at", "store" },
            { "playing in the river", "river" }, { "hiking in the woods", "woods" },
            { "the city was noisy", "city" }, { "you enjoy the quiet town", "town" },
            { "Duck, how do I unlock a weapon?", "weapons" }, { "weapons and gold", "gold" },
            { "Duck, this is scary", "unknown" }, { "hi", "greeting" }, { "tell me more", "repeat" },
            { "tell me about your school again", "school" }, { "zombies killed me", "death" },
            { "pizza", "creepy_food" }, { "burgers", "creepy_food" }, { "rice", "creepy_food" },
            { "fries", "creepy_food" }, { "Duck, I am hungry", "creepy_food" },
            { "buy a sandwich", "creepy_food" }, { "a pizza joke", "creepy_food" },
            { "frying pan against zombies", "fry_pan" }, { "price", "weapons" },
            { "follow me for pizza", "follow" }, { "ha", "laughter" }, { "ha ha", "laughter" }, { "HA, HA!", "laughter" },
            { "hahaha Duck", "laughter" }, { "hehe", "laughter" }, { "heh", "laughter" },
            { "perhaps", "unknown" }, { "what", "unknown" }, { "haunted", "unknown" },
            { "ha ha stop following me", "dismiss" }, { "", "unknown" }, { null, "unknown" }
        };
        for (int i = 0; i < cases.GetLength(0); i++)
            Check(DuckCompanionDialogue.Intent(cases[i, 0]) == cases[i, 1], "topic routing: " + cases[i, 0]);
        foreach (string command in new[] { "follow me", "follow me again", "stay with me", "resume following me", "come back" })
            Check(DuckCompanionDialogue.AcceptsSpeech(command, false, false), "follow command passes both recognition and quiet-mode gates: " + command);
        Check(!DuckCompanionDialogue.AcceptsSpeech("tell me about gold", false, false), "ordinary chatter cannot wake quiet Duck");
        Check(DuckCompanionDialogue.AcceptsSpeech("don't follow me", false, false), "dismissal command is heard and acknowledged");
        var testObject = new GameObject("Dialogue shuffle validation") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var dialogue = testObject.AddComponent<DuckCompanionDialogue>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(DuckCompanionDialogue).GetMethod("Awake", flags).Invoke(dialogue, null);
            var choose = typeof(DuckCompanionDialogue).GetMethod("Choose", flags);
            foreach (var group in bank.groups)
            {
                string previous = null;
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    var seen = new System.Collections.Generic.HashSet<string>();
                    for (int i = 0; i < group.lines.Length; i++)
                    {
                        string line = (string)choose.Invoke(dialogue, new object[] { group.intent });
                        Check(Array.IndexOf(group.lines, line) >= 0 && seen.Add(line), "shuffle covers only current topic without repeats: " + group.intent);
                        Check(group.lines.Length == 1 || previous != line, "no boundary repeat: " + group.intent);
                        previous = line;
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(testObject); }
        Debug.Log($"[Duck checks] {total} unique dialogue lines in {bank.groups.Length} topics; routing and three shuffle cycles passed.");
    }

    static void SaveWav(string path, float[] samples)
    {
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float sample in samples) writer.Write((short)(sample * 32767f));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

public static class DuckIntentOptimizationChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
        var cases = new HashSet<string> {
            "", " ", "\t\n", "hello duck", "the weather is strange today", "what is your favorite food",
            "Duck, stop following me and wait here", "follow me to the helicopter", "take me to the helicopter",
            "night-vision goggles", "walkie_talkie", "don't follow me", "do not follow me",
            "hi", "this is scary", "perhaps", "what", "haunted", "gearbox", "ha", "hehe", "HAHA, HA!",
            "his skin is better than yours", "you are not cuter than duck", "his skin isn't prettier than you",
            "he is more handsome than you", "you look better than him", "better weapon than you",
            "Duck, be quiet I like this song", "what music do you like", "have you seen any old films",
            "what's your favourite drink", "who are you", "ha ha stop following me", "purple pancakes"
        };
        // Every literal keyword/phrase from the frozen matcher, plus boundaries,
        // case/whitespace normalization and combinations that exercise precedence.
        string baselineSource = File.ReadAllText("Assets/Editor/Checks/Baselines/DuckIntentLegacy.cs");
        var words = new List<string>();
        foreach (Match group in Regex.Matches(baselineSource, @"Has\(t,\s*((?:""[^""]*""\s*,\s*)*""[^""]*"")\)"))
            foreach (Match literal in Regex.Matches(group.Groups[1].Value, @"""([^""]*)"""))
            {
                string word = literal.Groups[1].Value;
                words.Add(word);
                cases.Add(word);
                cases.Add("Duck, " + word.ToUpperInvariant() + "!");
                cases.Add("x" + word + "x");
                cases.Add(word.Replace(" ", "_-\t"));
                cases.Add("follow me " + word);
            }
        var random = new System.Random(1928);
        for (int i = 0; i < 200; i++)
            cases.Add(words[random.Next(words.Count)] + " " + words[random.Next(words.Count)]);
        string[] boundaries = { "é", "中", "1", "_", "\u0301", "\u203f", "\u200c", "\u200d", "\u0903", "\u00a0", "😀", "'", "-", "." };
        foreach (string boundary in boundaries)
        {
            cases.Add(boundary + "hello"); cases.Add("hello" + boundary);
            cases.Add(boundary + "hi" + boundary + " hello");
        }
        Check(DuckCompanionDialogue.Intent(null) == DuckIntentLegacy.Intent(null), "Null intent changed");
        int checkedCount = 0;
        foreach (string text in cases)
        {
            string expected = DuckIntentLegacy.Intent(text);
            string actual = DuckCompanionDialogue.Intent(text);
            Check(actual == expected, "Intent differs for [" + text + "]: " + expected + " -> " + actual);
            checkedCount++;
        }
        Debug.Log("Intent parity PASS: " + checkedCount + " cases against frozen pre-change matcher");

        string[] acceptance = { null, "", " ", "purple pancakes", "hello", "follow me", "be quiet", "don't follow me",
            "wait here", "can you hear me", "ducky", "duck", "DUCK!", "duck_hello", "éduck", "duck\u0301", "duck\u200c",
            "what music do you like", "what is your favorite food", "his skin is better than yours", "tell me about gold" };
        foreach (string text in acceptance)
            foreach (bool buddy in new[] { false, true })
                foreach (bool open in new[] { false, true })
                    Check(DuckCompanionDialogue.AcceptsSpeech(text, buddy, open) == DuckIntentLegacy.AcceptsSpeech(text, buddy, open),
                        "Speech acceptance changed: " + text);
        var sharedGate = typeof(DuckCompanionDialogue).GetMethod("AcceptsSpeech", BindingFlags.Static | BindingFlags.NonPublic);
        var gateArgs = new object[] { "follow me", false, false, null };
        Check((bool)sharedGate.Invoke(null, gateArgs) && (string)gateArgs[3] == "follow", "Gate did not retain classification");
        Check((bool)sharedGate.Invoke(null, gateArgs) && (string)gateArgs[3] == "follow", "Second gate lost classification");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                foreach (string text in new[] { "HI DUCK", "İhi", "helloé", "DUCK, FOLLOW ME" })
                    Check(DuckCompanionDialogue.Intent(text) == DuckIntentLegacy.Intent(text), "Culture parity: " + name);
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Debug.Log("Acceptance PASS: buddy/conversation/quiet gates, wake word, cached classification and culture cases");
        DuckCompanionSetup.ValidateDialogueBank();
        foreach (string text in new[] { "follow me", "hello duck", "the weather is strange today", "what is your favorite food" })
        {
            Measure(DuckIntentLegacy.Intent, text, out double oldMs, out long oldEvents);
            Measure(DuckCompanionDialogue.Intent, text, out double newMs, out long newEvents);
            Debug.Log($"Intent benchmark [{text}]: old {oldMs:F3} ms / {oldEvents} allocation events; new {newMs:F3} ms / {newEvents} allocation events per call (Unity Editor)");
            Check(newEvents < oldEvents / 20, "Expected at least 95% allocation reduction: " + text);
        }
        Debug.Log("DuckIntentOptimizationChecks: ALL PASSED");
        EditorApplication.Exit(0);
    }

    static void Measure(Func<string, string> matcher, string text, out double ms, out long events)
    {
        for (int i = 0; i < 3; i++) matcher(text);
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < 30; i++) matcher(text);
        ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency / 30;
        // Unity Mono can return zero from GC.GetAllocatedBytesForCurrentThread.
        // Record each allocation directly, without waiting for a frame boundary.
        using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1000000,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(allocations.Valid, "GC.Alloc recorder is unavailable");
        const int iterations = 5;
        for (int i = 0; i < iterations; i++) matcher(text);
        allocations.Stop();
        Check(allocations.Count < allocations.Capacity, "Allocation recorder capacity exceeded");
        // GC.Alloc.Value is time, not bytes. Count allocation events.
        events = allocations.Count / iterations;
    }
}

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RailWalletCapChecks
{
    public static string Run()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Interface/Watch.prefab");
        try
        {
            var watch = root.GetComponentInChildren<Watch>(true);
            var displayed = typeof(Watch).GetField("displayed", BindingFlags.Instance | BindingFlags.NonPublic);
            var show = typeof(Watch).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic);
            var awake = typeof(Watch).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake.Invoke(watch, null);
            var normal = watch.moneyText.color;
            foreach (bool status in new[] { false, true })
            foreach (int balance in new[] { 0, 1999, 2000, 1950 })
            {
                watch.SetStatus(status ? "Muted\n0:30" : null);
                displayed.SetValue(watch, balance);
                show.Invoke(watch, null);
                watch.moneyText.ForceMeshUpdate(true, true);
                if (watch.moneyText.color != (balance == 2000 ? Color.yellow : normal))
                    throw new Exception("Watch balance color mismatch");
                if (!watch.moneyText.text.Contains("/2000") || watch.moneyText.textInfo.lineCount != 2 || watch.moneyText.isTextOverflowing)
                    throw new Exception("Watch cap layout overflow or missing line");
                var info = watch.moneyText.textInfo;
                var first = info.characterInfo[0];
                var last = info.characterInfo[info.characterCount - 1];
                if (last.pointSize >= first.pointSize || last.color.r != 144 || last.color.g != 144 || last.color.b != 144)
                    throw new Exception("Cap must be smaller and grey");
            }
            return "PASS: eight watch balance/status layouts, yellow at 2000, normal after spending, smaller grey cap text, no overflow.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

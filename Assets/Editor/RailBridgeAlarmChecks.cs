#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
public static class RailBridgeAlarmChecks
{
    public static void Run()
    {
        int count=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; }
            double nextAlarm=double.NegativeInfinity;
            Check(!RailBridgeAlarm.TakeAlarm(false,0,ref nextAlarm),"Idle does not beep");
            Check(RailBridgeAlarm.TakeAlarm(true,10,ref nextAlarm),"First lowering beeps immediately");
            Check(!RailBridgeAlarm.TakeAlarm(false,11,ref nextAlarm) && !RailBridgeAlarm.TakeAlarm(true,11.1,ref nextAlarm),"Stop/restart retains cooldown");
            Check(!RailBridgeAlarm.TakeAlarm(true,13.999,ref nextAlarm) && RailBridgeAlarm.TakeAlarm(true,14,ref nextAlarm),"Four seconds minimum between beep starts");
            Check(!RailBridgeAlarm.TakeAlarm(false,30,ref nextAlarm) && RailBridgeAlarm.TakeAlarm(true,31,ref nextAlarm) && !RailBridgeAlarm.TakeAlarm(true,31,ref nextAlarm),"Idle expiry waits for lowering; no catch-up burst");

        var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab");
        var alarm=root.GetComponentInChildren<RailBridgeAlarm>(true);
        Check(alarm && alarm.alarm && alarm.alarm.clip && !alarm.alarm.loop && !alarm.alarm.playOnAwake,"Saved one-shot binding");
        Check(AssetDatabase.GetAssetPath(alarm.alarm.clip)=="Assets/Audio/Bridge/BridgeAlarmBeep_Reverb.wav","Processed clip");
        Debug.Log("BRIDGE ALARM: " + count + " checks passed");
    }
}
#endif

using System;
using System.Linq;
using System.Reflection;
using Game.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run with -executeMethod PauseMenuAudioChecks.Run in batch mode.
public static class PauseMenuAudioChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/DuckZ/Validation/Audio/Validate Pause Menu")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run this check outside Play mode.");
        Require(SoundRegistry.Refresh(out string error), error);
        var settings = AssetDatabase.LoadAssetAtPath<Game.Audio.AudioSettings>("Assets/Resources/GameAudioSettings.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PauseMenu/PauseMenu.prefab");
        Require(prefab != null, "Pause menu prefab missing.");
        var menu = prefab.GetComponent<PauseMenu>();
        var sounds = prefab.GetComponent<ItemSounds>();
        Require(sounds != null, "Pause menu needs the shared item impact handler.");
        CheckEvent(menu, "buttonPressSound", "ButtonPush", settings);
        CheckEvent(menu, "openSound", "On", settings);
        CheckEvent(menu, "closeSound", "Off", settings);
        CheckEvent(sounds, "grab", "Grab", settings);
        CheckEvent(sounds, "drop", "Drop", settings);
        Require(!System.IO.File.Exists("Assets/Audio/Menu/ButtonPush.ogg"), "Old clip still exists.");

        GameObject instance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        var cameraObject = new GameObject("Pause menu check camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, instance.scene);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(3, 2, 4);
        try
        {
            menu = instance.GetComponent<PauseMenu>();
            var body = instance.GetComponent<Rigidbody>();
            typeof(PauseMenu).GetField("body", Private).SetValue(menu, body);
            var collider = (BoxCollider)new SerializedObject(menu).FindProperty("grabCollider").objectReferenceValue;
            Require(collider != null && !collider.isTrigger, "Drop needs a solid collider.");
            body.isKinematic = false;
            body.useGravity = true;
            Vector3 velocity = new Vector3(0, -2, 0);
            body.linearVelocity = velocity;
            collider.enabled = false;
            Call(menu, "OnMenuReleased", new object[] { null });
            Require(!body.isKinematic && body.useGravity && collider.enabled,
                "Release must allow a physical drop and restore its collider.");
            Require(body.linearVelocity == velocity, "Release must preserve drop velocity.");
            Call(menu, "StopMenuBody");
            Require(body.isKinematic && !body.useGravity, "Closing must stop physics.");
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = velocity;
            var head = Camera.main;
            Require(head != null, "Pose check requires a main camera.");
            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
            Vector3 expected = head.transform.position + forward.normalized * menu.distance + Vector3.up * menu.height;
            Call(menu, "PlaceInFrontOfPlayer");
            Require(body.isKinematic && !body.useGravity && Vector3.Distance(body.position, expected) < 0.001f,
                "Reopening a dropped menu must restore its floating pose in front of the camera.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            PrefabUtility.UnloadPrefabContents(instance);
        }
        Debug.Log("PauseMenuAudioChecks PASS: five clip/event bindings, sound registry, solid drop collider, release physics, close freeze and reopen pose.");
    }

    static void CheckEvent(UnityEngine.Object owner, string field, string suffix, Game.Audio.AudioSettings settings)
    {
        var sound = new SerializedObject(owner).FindProperty(field).objectReferenceValue as SoundEvent;
        string name = "PauseMenu_" + suffix;
        Require(sound != null && sound.name == name, "Incorrect sound binding: " + field);
        Require(sound.TrySelect(null, out SoundSelection selection) && selection.Clip.name == name,
            "Incorrect or missing clip: " + name);
        Require(settings.sounds.Contains(sound), "Unregistered event: " + name);
    }

    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}

using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ZombieHitShapeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Types = { "Smasher", "Minion", "Hook", "Bomber", "Spider" };
    delegate Vector3 Contact(Vector3 from, Vector3 to, float radius, out Vector3 normal, out float separation);

    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static Vector3 Endpoint(SerializedProperty shape, string key)
    {
        var bone = (Transform)shape.FindPropertyRelative(key).objectReferenceValue;
        return bone.TransformPoint(shape.FindPropertyRelative(key + "Offset").vector3Value);
    }

    public static void Run()
    {
        foreach (var type in Types)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Zombie{type}.prefab");
            var obj = Object.Instantiate(prefab);
            try
            {
                Enemy enemy = obj.GetComponent<Enemy>();
                var config = new SerializedObject(enemy);
                var shapes = config.FindProperty("hitShapes");
                Check(shapes.arraySize == (type == "Spider" ? 10 : 6), type + " shape count");
                var near = (Func<Bounds, bool>)Delegate.CreateDelegate(typeof(Func<Bounds, bool>), enemy,
                    typeof(Enemy).GetMethod("WithinSwingReach", Private));
                CheckGait(enemy, shapes, near);
                foreach (var scale in new[] { 0.7f, 1f, 1.8f })
                {
                    obj.transform.localScale = prefab.transform.localScale * scale;
                    obj.transform.SetPositionAndRotation(new Vector3(8, 4, -11) * scale, Quaternion.Euler(11, 83, 25));
                    for (int i = 0; i < shapes.arraySize; i++)
                    {
                        var s = shapes.GetArrayElementAtIndex(i);
                        var p = Endpoint(s, "start");
                        var q = s.FindPropertyRelative("end").objectReferenceValue ? Endpoint(s, "end") : p;
                        float radius = s.FindPropertyRelative("radius").floatValue * obj.transform.lossyScale.x;
                        Check(radius > 0f, type + " collider thickness");
                        Vector3 outside = ((p + q) * 0.5f - enemy.Root.position).normalized;
                        foreach (var center in new[] { p, q, (p + q) * 0.5f })
                        {
                            var edge = center + outside * (radius * .99f);
                            Check(enemy.OverlapsSwing(edge, edge, 0f), type + " capsule edge " + i);
                            Check(near(new Bounds(edge, Vector3.zero)), type + " reach contains surface " + i);
                        }
                        Vector3 from = p - Vector3.forward * 5, to = p + Vector3.forward * 5;
                        Check(!enemy.OverlapsSwing(from, from, .02f) && !enemy.OverlapsSwing(to, to, .02f)
                            && enemy.OverlapsSwing(from, to, .02f), type + " continuous crossing " + i);
                        var approach = enemy.HitBloodPosition(from, to, .02f);
                        Check(!enemy.OverlapsSwing(approach, approach, .02f), type + " contact outside " + i);
                    }
                    var distant = new Bounds(enemy.Root.position + Vector3.right * 30, Vector3.one);
                    Check(!near(distant), type + " reject distant item");
                }

                // Bend one actual rig into an L away from the other limbs. The
                // old shoulder-to-hand chord misses the elbow and fills this gap.
                if (type == "Smasher")
                {
                    var upper = shapes.GetArrayElementAtIndex(2);
                    var lower = shapes.GetArrayElementAtIndex(3);
                    var shoulder = (Transform)upper.FindPropertyRelative("start").objectReferenceValue;
                    var elbow = (Transform)upper.FindPropertyRelative("end").objectReferenceValue;
                    var hand = (Transform)lower.FindPropertyRelative("end").objectReferenceValue;
                    shoulder.position = enemy.Root.position + Vector3.right * 2;
                    elbow.position = shoulder.position + Vector3.up;
                    hand.position = elbow.position + Vector3.right;
                    Check(enemy.OverlapsSwing(elbow.position - Vector3.forward, elbow.position + Vector3.forward, .02f),
                        "Smasher bent elbow hit");
                    Vector3 gap = (shoulder.position + hand.position) * .5f;
                    Check(!enemy.OverlapsSwing(gap - Vector3.forward, gap + Vector3.forward, .02f),
                        "Smasher open elbow gap");
                }
                if (type == "Hook")
                {
                    var hook = new SerializedObject(obj.GetComponent<ZombieHookAttack>());
                    var hookHead = (Transform)hook.FindProperty("hookHead").objectReferenceValue;
                    Vector3 wrist = hookHead.parent.position;
                    hookHead.position += Vector3.forward * 12f;
                    Check(enemy.OverlapsSwing(wrist, wrist, .02f), "Hook wrist remains hittable");
                    Check(!enemy.OverlapsSwing(hookHead.position, hookHead.position, .02f)
                        && !enemy.OverlapsSwing((wrist + hookHead.position) * .5f, (wrist + hookHead.position) * .5f, .02f),
                        "Flying hook and paid-out chain are excluded from zombie hits");
                }
                if (type == "Bomber")
                {
                    // Isolate the animated skull to distinguish a contact normal
                    // from the old horizontal push away from the torso.
                    var skull = shapes.GetArrayElementAtIndex(1);
                    var bone = (Transform)skull.FindPropertyRelative("start").objectReferenceValue;
                    bone.position += Vector3.up * 2;
                    Vector3 center = Endpoint(skull, "start");
                    Check(enemy.OverlapsSwing(center, center, 0), "Bomber head follows its bone");
                    var contact = (Contact)Delegate.CreateDelegate(typeof(Contact), enemy,
                        typeof(Enemy).GetMethod("SwingContact", Private));
                    contact(center + Vector3.up, center - Vector3.up * .1f, .02f, out var normal, out float depth);
                    Check(Vector3.Dot(normal, Vector3.up) > .99f && depth > 0, "Throw rebounds off top of skull");
                    contact(center, center + Vector3.up, .02f, out normal, out depth);
                    Check(Vector3.Dot(normal, Vector3.up) > .99f && depth == 0, "Throw exiting skull stays separated");
                }
                Debug.Log("ZombieHitShapeChecks PASS " + type);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }

    static void CheckGait(Enemy enemy, SerializedProperty shapes, Func<Bounds, bool> near)
    {
        const float dt = 1f / 72f;
        var floor = new GameObject("Hit geometry test floor") { layer = 6 };
        Vector3 origin = new Vector3(400f, 5f, 400f);
        try
        {
            floor.transform.position = origin - Vector3.up * .5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(40, 1, 40);
            enemy.Root.SetPositionAndRotation(origin, Quaternion.identity);
            Physics.SyncTransforms();
            foreach (var limb in enemy.GetComponentsInChildren<Game.Animation.TwoBoneLimb>()) limb.CaptureRest();
            foreach (var leg in enemy.GetComponentsInChildren<Game.Animation.SpiderLeg>())
                typeof(Game.Animation.SpiderLeg).GetMethod("Awake", Private).Invoke(leg, null);
            typeof(Enemy).GetMethod("Awake", Private).Invoke(enemy, null);
            var gait = enemy.Gait;
            gait.GetType().GetMethod("Awake", Private)?.Invoke(gait, null);
            for (int frame = 0; frame < 144; frame++)
            {
                float distance = frame * dt * 3f;
                enemy.SetRootPose(origin + Vector3.forward * distance, Quaternion.Euler(0, distance * 12, 0));
                gait.Tick(dt);
                for (int i = 0; i < shapes.arraySize; i++)
                {
                    var shape = shapes.GetArrayElementAtIndex(i);
                    string joint = shape.FindPropertyRelative("end").objectReferenceValue ? "end" : "start";
                    Vector3 p = Endpoint(shape, joint);
                    float r = shape.FindPropertyRelative("radius").floatValue * enemy.Root.lossyScale.x;
                    Vector3 outer = p + (p - enemy.Root.position).normalized * (r * .98f);
                    Check(near(new Bounds(outer, Vector3.zero)) && enemy.OverlapsSwing(outer, outer, 0),
                        enemy.name + " moving extremity outside hit geometry at frame " + frame);
                }
            }
        }
        finally { Object.DestroyImmediate(floor); }
    }
}

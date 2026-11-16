using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RailBridgeCollapseChecks
{
    public static string Run()
    {
        int checks=0;
        void Check(bool value,string label) { if(!value)throw new Exception(label); checks++; }
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var bridge=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab"));
            SceneManager.MoveGameObjectToScene(bridge,scene);
            var hinge=bridge.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Bridge hinge");
            var renderers=hinge.GetComponentsInChildren<Renderer>(true);
            var colliders=hinge.GetComponentsInChildren<Collider>(true);
            var rendererStates=renderers.Select(r=>r.enabled).ToArray();
            var colliderStates=colliders.Select(c=>c.enabled).ToArray();
            foreach(float progress in new[]{0f,.5f,.99f})
            {
                hinge.localRotation=Quaternion.Euler(-80*(1-progress),0,0);
                var go=Object.Instantiate(Resources.Load<GameObject>("Rails/BridgeCollapse"));
                SceneManager.MoveGameObjectToScene(go,scene);
                var fx=go.GetComponent<RailBridgeCollapse>();
                fx.Begin(hinge,progress,Vector3.zero,1.2f);
                Check(fx.sections.Length==5 && fx.fires.Length==3,"Five pieces and three fire pockets");
                Check(renderers.All(r=>!r.enabled) && colliders.All(c=>!c.enabled),"Intact span fully suppressed");
                Check(go.GetComponentsInChildren<Collider>(true).Length==0,"No new collision on scenery");
                Check(go.GetComponentsInChildren<Light>(true).Length==0,"No fire realtime lights");
                fx.Sample(0);
                for(int i=0;i<5;i++)
                {
                    Check(Vector3.Distance(fx.sections[i].position,hinge.TransformPoint(new Vector3(0,0,i*6+3)))<.001f,"Initial pivot matches actual bridge angle: "+i+" actual "+fx.sections[i].position+" expected "+hinge.TransformPoint(new Vector3(0,0,i*6+3))+" root "+fx.transform.position);
                    Check(Quaternion.Angle(fx.sections[i].rotation,hinge.rotation)<.01f,"Initial rotation continuity");
                }
                fx.Sample(3.2f);
                for(int i=0;i<5;i++)
                {
                    var section=fx.sections[i];
                    float bottom=section.GetComponentsInChildren<MeshFilter>().Min(m=>m.sharedMesh.vertices.Min(v=>bridge.transform.InverseTransformPoint(m.transform.TransformPoint(v)).y));
                    Check(Mathf.Abs(bottom+11.48f)<.005f,"Resting section sits on river bed");
                    var end=section.position;
                    fx.Sample(30);
                    Check(Vector3.Distance(section.position,end)<.001f,"Final wreck remains still");
                }
                Check(fx.fires.All(f=>f.activeSelf),"Persistent fires active");
                fx.Restore();
                Check(renderers.Select(r=>r.enabled).SequenceEqual(rendererStates),"Renderer enabled states restored");
                Check(colliders.Select(c=>c.enabled).SequenceEqual(colliderStates),"Collider enabled states restored");
                Object.DestroyImmediate(go);
            }
            return checks+" collapse EditMode assertions passed (raised, half lowered, almost lowered; landing, late age, cleanup).";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}

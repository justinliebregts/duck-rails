#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class RailQuarryMine
{
    static void TuneGate(Transform root){
        foreach(var light in root.GetComponentsInChildren<Light>().Where(l=>l.name=="Gate lantern")){
            light.intensity=55;light.range=16;light.bounceIntensity=0;light.shadowRadius=.42f;
            var pos=light.transform.localPosition;pos.y=6.38f;light.transform.localPosition=pos;
            foreach(var r in light.transform.parent.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Warm lantern globe")){
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);r.receiveGI=ReceiveGI.LightProbes;
            }
        }
    }

    static void QuarryAcoustics(RailBlock block){
        var old=block.transform.Find("Reverb Zones");
        if(old)Object.DestroyImmediate(old.gameObject);
        var zones=Group(block.transform,"Reverb Zones");
        var underground=Group(zones,"Mine workings");var exposed=Group(zones,"Open quarry");
        void Add(string name,Vector3 center,Vector3 size,bool mine,float yaw=0){
            var box=Group(mine?underground:exposed,name);
            box.localPosition=center;box.localRotation=Quaternion.Euler(0,yaw,0);
            var audio=Group(box,"Reverb settings").gameObject.AddComponent<AudioReverbZone>();
            audio.reverbPreset=mine?AudioReverbPreset.Cave:AudioReverbPreset.Mountains;
            audio.reverbPreset=AudioReverbPreset.User;audio.room=mine?-650:-1650;
            audio.roomHF=mine?-850:-1450;audio.decayTime=mine?3.25f:1.55f;audio.decayHFRatio=mine?.74f:.62f;
            audio.reflections=mine?-870:-1310;audio.reflectionsDelay=mine?.026f:.085f;
            audio.reverb=mine?-140:-980;audio.reverbDelay=mine?.042f:.074f;
            float fade=mine?.55f:1.2f;audio.minDistance=.01f;audio.maxDistance=fade;
            var so=new SerializedObject(box.gameObject.AddComponent<Game.Audio.BoxReverbZone>());
            so.FindProperty("size").vector3Value=size;so.FindProperty("fadeDistance").floatValue=fade;
            so.FindProperty("zone").objectReferenceValue=audio;so.ApplyModifiedPropertiesWithoutUndo();
        }
        void Gallery(string name,Vector2 a,Vector2 b,float width){
            var delta=b-a;int count=Mathf.CeilToInt(delta.magnitude/.5f);int first=-1;
            for(int i=0;i<=count;i++){
                var mid=a+delta*((i+.5f)/count);
                bool covered=i<count && Height(mid.x,mid.y)>MineRoof(mid.x,mid.y)-.12f;
                if(covered && first<0)first=i;
                if(covered || first<0)continue;
                var from=a+delta*((float)first/count);var to=a+delta*((float)i/count);var c=(from+to)*.5f;
                Add(name,new(c.x,-4.65f,c.y),new(width,3.8f,(to-from).magnitude+.42f),true,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
                first=-1;
            }
        }
        Gallery("West adit",new(-25,48),new(-25,110),7.5f);
        for(int route=0;route<Routes.Length;route++)for(int i=1;i<Routes[route].Length;i++)
            Gallery(new[]{"North drift","Middle drift","South drift","Crosscut"}[route]+" "+i,Routes[route][i-1],Routes[route][i],6.6f);
        Gallery("Hoist landing connector",new(-42,106),new(-25,99),5.5f);
        Add("West shaft landing",new(-42,-4.65f,106),new(8.8f,3.8f,8.8f),true);
        Add("Office entry passage",new(11,-4.65f,74.4f),new(5,3.8f,10.8f),true);
        Add("South portal recess",new(13.35f,-4.65f,54.6f),new(2.8f,3.8f,3.5f),true);
        Add("North drift bend",new(-15,-4.65f,108.4f),new(4.4f,3.8f,5.4f),true);
        Add("Boring chamber",new(-13,-4.5f,128),new(12,4.2f,20),true);
        Add("Office",new(10.8f,-4.55f,80.3f),new(11.8f,3.9f,11.5f),true);
        Add("Vertical winding shaft",new(-42,3.55f,106),new(6.1f,19.7f,6.1f),true);
        Add("Southeast gravel approach",new(27,1.9f,11.5f),new(39,5,18),false);
        Add("Southwest weighbridge yard",new(-27,1.9f,18),new(39,5,31),false);
        Add("Northwest pump yard",new(-27,2.7f,144.5f),new(39,6,25),false);
        Add("Northeast ore yard",new(27,2.4f,141),new(39,6,32),false);
        Add("East haul road and rim",new(26,.95f,73),new(37,6.0f,105),false);
        Add("Lower haul and south pit",new(26,-4.65f,45),new(29,5.2f,30),false);
        Add("Crane pit and water",new(32,-4.65f,81),new(29,5.2f,42),false);
        Add("North pit benches",new(27,-4.65f,110),new(29,5.2f,16),false);
        Add("West terraced face",new(-23,7.5f,83),new(29,17,97),false);
        Add("Upper south ridge",new(-42.4f,9,70),new(9.8f,19,63),false);
        Add("Upper north ridge",new(-42.4f,9,123),new(9.8f,19,25),false);
    }

    [MenuItem("Duck Rails/Authoring/Quarry Mine/Update lamps, reverb and spawn placements")]
    public static void UpdateQuarryPlacementsAndSound(){
        Guard();
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=BakeScene||PrefabStageUtility.GetCurrentPrefabStage()!=null)
            throw new InvalidOperationException("Open the saved Quarry bake scene in the main stage");
        var live=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<RailBlock>()).Single(b=>b.id==Id);
        Physics.SyncTransforms();
        var plan=PlanQuarrySpawns(live);
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/"+Id+"/Cube Block UV.asset");
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try{
            var b=prefab.GetComponent<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RootName);
            var supports=root.Find("Tunnel timber supports");RemoveNamed(supports,"Camping lantern");CaveLanterns(root,supports);
            TuneGate(root);QuarryAcoustics(b);SaveSpawnPlan(b,plan);
            b.GetComponent<RailBakedBlockLighting>().CaptureBakedFixtures();
            PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath,out bool saved);
            if(!saved)throw new InvalidOperationException("Quarry prefab save failed");
        }finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(BakeScene);
    }
}
#endif
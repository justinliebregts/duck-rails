#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public static partial class RailFarmPolish
{
    public static void InstallBasement(RailBlock block)
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new Exception("Duck Rails Edit Mode required");
        var house = block.transform.Find("Env/Farm authored v3/Farmhouse");
        var cellar = house.Find("Basement");
        if (cellar.Find("Holding cages")) throw new Exception("Basement already furnished");
        Init(); meshId = 2100;
        var iron = NewMat("Basement oxidized iron", steel, new Color(.23f,.28f,.25f));
        var enamel = NewMat("Basement locker enamel", steel, new Color(.40f,.46f,.40f));
        var fluorescent = NewMat("Basement fluorescent glass", white, new Color(.83f,.91f,.76f));
        fluorescent.EnableKeyword("_EMISSION");
        fluorescent.SetColor("_EmissionColor",new Color(.74f,.88f,.64f)*1.5f);
        fluorescent.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        foreach (var l in cellar.GetComponentsInChildren<Light>()) Object.DestroyImmediate(l.gameObject);
        foreach (var t in cellar.Cast<Transform>().Where(t=>t.name=="Lamp housing").ToArray()) Object.DestroyImmediate(t.gameObject);
        Start(cellar,"Holding cages");
        // Bars have physical gaps too; no invisible solid wall across an open cell.
        void Bars(Vector3 a,Vector3 b)
        {
            for(int i=0,n=Mathf.CeilToInt(Vector3.Distance(a,b)/.28f);i<=n;i++)
                Box("Iron bar",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*1.15f,new Vector3(.065f,2.3f,.065f),iron);
            foreach(float y in new[]{.12f,2.3f}) Beam("Cage rail",a+Vector3.up*y,b+Vector3.up*y,.09f,iron);
        }
        void Cage(float x,float z,float w,float d)
        {
            var a=new Vector3(x-w/2,-3.2f,z-d/2);var b=a+Vector3.right*w;
            var c=b+Vector3.forward*d;var e=a+Vector3.forward*d;
            Bars(a,e);Bars(b,c);Bars(e,c);
            // Open south entrance, at least 1.2m clear. Door rests against the side.
            Bars(a,a+Vector3.right*(w/2-.65f));Bars(b-Vector3.right*(w/2-.65f),b);
            Beam("Door lintel",a+Vector3.up*2.3f,b+Vector3.up*2.3f,.1f,iron);
            Bars(a+new Vector3(.08f,0,.1f),a+new Vector3(.08f,0,1.25f));
            for(float xx=x-w/2;xx<=x+w/2;xx+=.55f)
                Beam("Cage roof bar",new Vector3(xx,-.9f,z-d/2),new Vector3(xx,-.9f,z+d/2),.065f,iron);
        }
        Cage(18.5f,135.7f,3.4f,3.4f);
        Cage(19.3f,140.8f,2.5f,2.3f);Cage(19.3f,144.35f,2.5f,2.3f);
        Cage(26.5f,130.1f,3,2.8f);Cage(28,144.1f,2.5f,2.5f);
        Finish();
        Start(cellar,"Watching chairs");
        foreach(var p in new[]{new Vector3(16.4f,-3.2f,132.2f),new Vector3(18.3f,-3.2f,131.7f),new Vector3(20.5f,-3.2f,132.3f),new Vector3(21.8f,-3.2f,134.3f)})
        {var forward=new Vector3(18.5f,p.y,135.7f)-p;Chair(p,Quaternion.LookRotation(-forward).eulerAngles.y);}
        Finish();
        Start(cellar,"Open lockers");
        foreach(float z in new[]{139.7f,141.2f,142.7f})
        {
            float x=33.1f;
            Box("Locker back",new Vector3(x+.45f,-2.15f,z),new Vector3(.08f,2.1f,1.25f),enamel);
            foreach(float dz in new[]{-.625f,.625f})Box("Locker side",new Vector3(x,-2.15f,z+dz),new Vector3(.9f,2.1f,.07f),enamel);
            foreach(float y in new[]{-3.12f,-1.95f,-1.12f})Box("Locker shelf",new Vector3(x,y,z),new Vector3(.9f,.08f,1.25f),enamel);
            // Door swung out toward the room, leaving the shelf exposed.
            Box("Open locker door",new Vector3(x-.95f,-2.15f,z+.62f),new Vector3(1.1f,2.1f,.07f),enamel);
            Box("Locker handle",new Vector3(x-1.3f,-2.1f,z+.56f),new Vector3(.06f,.22f,.06f),iron,false);
        }
        Finish();
        Start(cellar,"Unsettling fluorescent fixtures");
        void Fixture(Vector3 p,float intensity,float range)
        {
            Box("Fluorescent housing",p,new Vector3(1.05f,.12f,.28f),iron,false);
            Box("Green-white diffuser",p-Vector3.up*.075f,new Vector3(.9f,.05f,.2f),fluorescent,false);
            var t=Group(group,"Baked green-white light");t.localPosition=p-Vector3.up*.17f;
            var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;
            l.color=new Color(.80f,.9f,.73f);l.intensity=intensity;l.range=range;l.shadows=LightShadows.Soft;
        }
        Fixture(new Vector3(18.5f,-.45f,135.7f),14,8);
        Fixture(new Vector3(20.2f,-.45f,143.2f),5,6);
        Fixture(new Vector3(27,-.45f,131.3f),5,6);
        Fixture(new Vector3(30.8f,-.45f,141.6f),6,7);Finish();
        void LootAt(RailSpawnKind kind,Vector3 local,string label)
        {
            var g=RailSpawnerPrefabs.Instantiate(kind,block.gameObject.scene);
            g.transform.SetParent(block.transform.Find(kind+" Spawns"),false);
            g.name=kind+" - Basement "+label;
            g.transform.position=house.TransformPoint(local+Vector3.up*(.05f-RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold).min.y));
            RailSpawnPool.SetPoints(block.spawnCandidates,kind,RailSpawnPool.Points(block.spawnCandidates,kind).Append(g.transform).ToArray());
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        }
        LootAt(RailSpawnKind.Coal,new Vector3(18.4f,-3.2f,135.6f),"central cage");
        LootAt(RailSpawnKind.Coal,new Vector3(26.5f,-3.2f,130.2f),"utility cell");
        LootAt(RailSpawnKind.Gold,new Vector3(19.3f,-3.2f,144.4f),"rear cell");
        LootAt(RailSpawnKind.Gold,new Vector3(28,-3.2f,144.1f),"utility rear cell");
        LootAt(RailSpawnKind.Gold,new Vector3(33.05f,-1.91f,139.7f),"open locker");
        LootAt(RailSpawnKind.Gold,new Vector3(33.05f,-1.91f,142.7f),"open locker");
        EditorUtility.SetDirty(block);PrefabUtility.ApplyPrefabInstance(block.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(block.gameObject.scene);EditorSceneManager.SaveScene(block.gameObject.scene);AssetDatabase.SaveAssets();
    }
}
#endif


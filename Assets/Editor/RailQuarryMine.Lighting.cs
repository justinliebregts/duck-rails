#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class RailQuarryMine
{
    // The ground, tunnel floor and soffit are height fields. A single world-space
    // projection per layer keeps adjoining triangles in the same lightmap chart.
    // Vertical walls get separate charts for each connected run with the same facing.
    static void SetTerrainLightmapUV(Mesh mesh){
        var positions=mesh.vertices;
        var triangles=mesh.triangles;
        int count=triangles.Length/3;
        var parent=Enumerable.Range(0,count).ToArray();
        var axes=new int[count];
        var columns=new Dictionary<(int,Vector2Int),int>();
        var layers=new Dictionary<int,int>();
        int Root(int i){
            while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}
            return i;
        }
        Vector2 Project(int axis,Vector3 p){
            if(axis<2)return new(p.x,p.z);
            return axis<4?new(p.z,p.y):new(p.x,p.y);
        }
        for(int f=0;f<count;f++){
            int at=f*3;var a=positions[triangles[at]];
            var n=Vector3.Cross(positions[triangles[at+1]]-a,positions[triangles[at+2]]-a).normalized;
            int axis=Mathf.Abs(n.y)>.00001f?(n.y>=0?0:1):
                Mathf.Abs(n.x)>Mathf.Abs(n.z)?(n.x>=0?2:3):(n.z>=0?4:5);
            axes[f]=axis;
            if(axis<2){
                if(layers.TryGetValue(axis,out int first))parent[f]=first;
                else layers.Add(axis,f);
                continue;
            }
            for(int j=0;j<3;j++){
                var v=positions[triangles[at+j]];
                var key=(axis,new Vector2Int(Mathf.RoundToInt(v.x*2048),Mathf.RoundToInt(v.z*2048)));
                if(columns.TryGetValue(key,out int other))parent[Root(f)]=Root(other);
                else columns.Add(key,f);
            }
        }
        var groups=Enumerable.Range(0,count).GroupBy(Root).ToArray();
        var uv=new Vector2[positions.Length];
        const float gutter=.75f;
        var charts=new List<(int axis,int[] vertices,Vector2 lo,Vector2 size)>();
        foreach(var group in groups){
            int axis=axes[group.First()];
            var vertices=group.SelectMany(f=>new[]{triangles[f*3],triangles[f*3+1],triangles[f*3+2]}).Distinct().ToArray();
            var lo=Project(axis,positions[vertices[0]]);var hi=lo;
            foreach(int v in vertices){
                var q=Project(axis,positions[v]);lo=Vector2.Min(lo,q);hi=Vector2.Max(hi,q);
            }
            charts.Add((axis,vertices,lo,hi-lo+Vector2.one*(2*gutter)));
        }
        float rowLimit=Mathf.Max(charts.Max(c=>c.size.x),Mathf.Sqrt(charts.Sum(c=>c.size.x*c.size.y)));
        float x=0,y=0,rowHeight=0,width=0;
        foreach(var chart in charts.OrderByDescending(c=>c.size.y)){
            if(x>0 && x+chart.size.x>rowLimit){x=0;y+=rowHeight;rowHeight=0;}
            var origin=new Vector2(x+gutter,y+gutter)-chart.lo;
            foreach(int v in chart.vertices)uv[v]=Project(chart.axis,positions[v])+origin;
            x+=chart.size.x;width=Mathf.Max(x,width);rowHeight=Mathf.Max(rowHeight,chart.size.y);
        }
        for(int i=0;i<uv.Length;i++)uv[i]=new(uv[i].x/width,uv[i].y/(y+rowHeight));
        mesh.uv2=uv;
    }

    // Preview scenes do not populate the global atlas table when the main scene is unlit.
    // Append only missing maps, and return the original slots as soon as the stage closes.
    static LightmapData[] priorPreviewMaps,registeredPreviewMaps;
    static void ReleaseQuarryPreview(){
        if(registeredPreviewMaps!=null && LightmapSettings.lightmaps.Select(m=>m.lightmapColor)
            .SequenceEqual(registeredPreviewMaps.Select(m=>m.lightmapColor)))
            LightmapSettings.lightmaps=priorPreviewMaps;
        priorPreviewMaps=null;registeredPreviewMaps=null;
    }

    [InitializeOnLoadMethod]
    static void RegisterQuarryPrefabLighting(){
        PrefabStage.prefabStageOpened+=PreviewQuarryLighting;
        PrefabStage.prefabStageClosing+=stage=>{if(stage.assetPath==PrefabPath)ReleaseQuarryPreview();};
        AssemblyReloadEvents.beforeAssemblyReload+=ReleaseQuarryPreview;
        EditorApplication.delayCall+=()=>PreviewQuarryLighting(PrefabStageUtility.GetCurrentPrefabStage());
    }

    static void PreviewQuarryLighting(PrefabStage stage){
        if(stage==null || stage.assetPath!=PrefabPath || !stage.prefabContentsRoot)return;
        var owner=stage.prefabContentsRoot.GetComponent<RailBakedBlockLighting>();
        if(!owner || owner.bindings==null || owner.bindings.Length==0)return;
        var data=AssetDatabase.LoadAssetAtPath<LightingDataAsset>(
            System.IO.Path.ChangeExtension(BakeScene,null)+"/LightingData.asset");
        if(!data)return;
        Lightmapping.SetLightingDataAssetForScene(stage.scene,data);
        var maps=LightmapSettings.lightmaps;
        var missing=owner.bindings.Where(b=>b.renderer && b.color).Select(b=>b.color).Distinct()
            .Except(maps.Select(m=>m.lightmapColor)).ToArray();
        if(missing.Length>0){
            priorPreviewMaps=maps;
            maps=maps.Concat(missing.Select(t=>new LightmapData{lightmapColor=t})).ToArray();
            registeredPreviewMaps=maps;LightmapSettings.lightmaps=maps;
        }
        var indices=new Dictionary<Texture2D,int>();
        for(int i=0;i<maps.Length;i++)if(maps[i].lightmapColor)indices[maps[i].lightmapColor]=i;
        foreach(var binding in owner.bindings){
            if(!binding.renderer || !binding.color || !indices.TryGetValue(binding.color,out int index))continue;
            binding.renderer.lightmapIndex=index;
            binding.renderer.lightmapScaleOffset=binding.scaleOffset;
        }
        if(!owner.bindings.Where(b=>b.renderer && b.color).All(b=>indices.ContainsKey(b.color)))return;
        foreach(var lamp in owner.GetComponentsInChildren<Light>()){
            if(lamp.lightmapBakeType!=LightmapBakeType.Baked)continue;
            var output=lamp.bakingOutput;output.isBaked=true;output.lightmapBakeType=LightmapBakeType.Baked;
            lamp.bakingOutput=output;
        }
    }
}
#endif

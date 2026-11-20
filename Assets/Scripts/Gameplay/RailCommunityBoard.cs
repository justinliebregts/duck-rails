using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RailCommunityBoard : MonoBehaviour
{
    [Serializable] public class Row { public string label; public long amount; }
    [Serializable] public class Snapshot
    {
        public long attempts, escapes, coal, gold, meters;
        public Row Stop, Deadliest, Bonked, Item;
        public Row[] Weekly, Lifetime;
    }
    readonly List<TMP_Text> values = new List<TMP_Text>();
    readonly Color yellow = new Color32(255, 212, 86, 255);
    readonly Color card = new Color32(48,65,78,255);
    TMP_Text status;
    RectTransform panel;
    static Snapshot cached;
    static float refreshAt;
    public static void Install(GameObject compound)
    {
        var go = new GameObject("Tales from the Rails", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(compound.transform, false);
        go.transform.SetPositionAndRotation(new Vector3(-8.69f, 1.85f, 0), Quaternion.Euler(0,-90,0));
        go.transform.localScale = Vector3.one * .00195f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.AddComponent<RailCommunityBoard>();
    }
    RectTransform Rect(string label, int x, int y, int w, int h)
    {
        var r = (RectTransform)new GameObject(label, typeof(RectTransform)).transform;
        r.SetParent(panel,false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h);
        return r;
    }
    void Plate(int x,int y,int w,int h,Color color)
    {
        var image=Rect("Backing",x,y,w,h).gameObject.AddComponent<Image>();
        image.color=color; image.raycastTarget=false;
    }
    TMP_Text Text(string label,int x,int y,int w,int h,int size,bool value=false,bool right=false)
    {
        var t=Rect(label,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=TMP_Settings.defaultFontAsset; t.raycastTarget=false; t.richText=false;
        t.enableAutoSizing=true; t.fontSizeMin=size*.65f; t.fontSizeMax=size; t.fontSize=size;
        t.textWrappingMode=TextWrappingModes.NoWrap; t.overflowMode=TextOverflowModes.Ellipsis;
        t.alignment=right?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft;
        t.color=value?yellow:Color.white; t.text=value?"—":label;
        if(value) values.Add(t);
        return t;
    }
    void Awake()
    {
        panel=(RectTransform)transform; panel.sizeDelta=new Vector2(1500,900);
        Plate(0,0,1500,900,new Color32(32,45,56,255)); Plate(0,0,1500,8,yellow);
        Text("TALES FROM THE RAILS",30,18,1000,64,58);
        status=Text("",1000,25,470,45,26,false,true);
        string[] totals={"Escape attempts","Successful escapes","Coal gathered","Gold gathered","Kilometers ridden together"};
        for(int i=0;i<5;i++){
            int x=30+i*290; Plate(x,92,280,91,card); Text(totals[i],x+10,96,260,36,28); Text("Total",x+10,134,260,44,38,true);
        }
        Text("THIS WEEK ON THE RAILS",30,193,1400,42,34);
        string[] titles={"Most-looted stop","Deadliest zombie","Most-bonked zombie"};
        for(int i=0;i<3;i++){
            int x=30+i*485; Plate(x,242,470,122,card); Text(titles[i],x+12,245,446,36,30);
            Text("Winner",x+12,282,446,42,39,true); Text("Details",x+12,325,446,32,26,true);
        }
        Text("TOP 10 ESCAPE ARTISTS",30,375,1440,52,44);
        for(int j=0;j<2;j++){
            int x=30+j*730; Plate(x,438,710,39,card); Text(j==0?"THIS WEEK":"ALL TIME",x+12,439,680,36,31);
            for(int i=0;i<10;i++){
                int y=481+i*40; if(i%2==0) Plate(x,y,710,39,new Color32(40,57,70,255));
                Text((i+1).ToString("00"),x+12,y,55,38,26);
                var name=Text("Duck",x+76,y,432,38,31,true); name.color=Color.white;
                Text("Escapes",x+514,y,182,38,31,true,true);
            }
        }
    }
    void OnEnable() { StartCoroutine(Refresh()); }
    void OnDisable() { StopAllCoroutines(); }
    IEnumerator Refresh()
    {
        if(cached!=null) Show(cached);
        else status.text="Waiting for station records";
        while(true){
            if(DuckzApiClient.IsReady && Time.unscaledTime>=refreshAt){
                bool ok=false;
                yield return DuckzApiClient.Get<Snapshot>("rail-board",r=>{
                    ok=r.Success && r.Data!=null;
                    if(ok){cached=r.Data;Show(cached);}
                });
                refreshAt=Time.unscaledTime+(ok?300:30);
                status.text=ok?"":cached==null?"Records unavailable":"Showing last update";
            }
            yield return new WaitForSecondsRealtime(2);
        }
    }
    static string Friendly(string s) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((s??"").Replace("_"," ").Replace("-"," "));
    void Show(Snapshot s)
    {
        int n=0;
        void Put(string text){values[n++].text=text;}
        foreach(var v in new[]{s.attempts,s.escapes,s.coal,s.gold}) Put(v.ToString("N0"));
        Put((s.meters/1000d).ToString("N1")+" km");
        void Award(Row row,string unit,string extra=""){
            Put(row==null || row.amount==0?"—":Friendly(row.label));
            Put(row==null || row.amount==0?"":row.amount.ToString("N0")+" "+unit+extra);
        }
        Award(s.Stop,"coal gathered"); Award(s.Deadliest,"ducks downed");
        Award(s.Bonked,"hits",s.Item?.amount>0?"  /  Top item: "+Friendly(s.Item.label):"");
        foreach(var rows in new[]{s.Weekly,s.Lifetime}) for(int i=0;i<10;i++){
            var row=rows!=null && i<rows.Length?rows[i]:null;
            Put(row?.label??"—"); Put(row?.amount.ToString("N0")??"");
        }
    }
}

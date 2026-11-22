using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;


public sealed class RailCommunityBoard : MonoBehaviour
{
    [Serializable] public class Row { public string label; public long amount; }
    [Serializable] public class Snapshot
    {
        public long attempts, escapes, coal, gold, meters;
        public Row Stop, Deadliest, Bonked, Item;
        public Row[] Weekly, Lifetime;
    }
    [SerializeField] List<TMP_Text> values = new List<TMP_Text>();
    [SerializeField] TMP_Text status;
    static Snapshot cached;
    static float refreshAt;
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
            Put(row==null || row.amount==0?"-":Friendly(row.label));
            Put(row==null || row.amount==0?"":row.amount.ToString("N0")+" "+unit+extra);
        }
        Award(s.Stop,"coal gathered"); Award(s.Deadliest,"ducks downed");
        Award(s.Bonked,"hits",s.Item?.amount>0?"  /  Top item: "+Friendly(s.Item.label):"");
        foreach(var rows in new[]{s.Weekly,s.Lifetime}) for(int i=0;i<10;i++){
            var row=rows!=null && i<rows.Length?rows[i]:null;
            Put(row?.label??"-"); Put(row?.amount.ToString("N0")??"");
        }
    }
}

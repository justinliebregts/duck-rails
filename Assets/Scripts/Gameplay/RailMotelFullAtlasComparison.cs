using UnityEngine;

/// <summary>Only attached to the explicit full-map development comparison fixture.</summary>
[DefaultExecutionOrder(-1100)]
public sealed class RailMotelFullAtlasComparison : MonoBehaviour
{
    public Renderer[] controlOnly;
    public GameObject atlas;
    void OnEnable()
    {
        if(!Application.isPlaying || !atlas || controlOnly==null || controlOnly.Length==0)return;
        bool useAtlas=false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        useAtlas=RailQuestPerformance.MotelFullMapVariant=="atlas";
        RailQuestPerformance.AppliedMotelFullMapVariant=useAtlas?"atlas":"control";
#endif
        foreach(var renderer in controlOnly)if(renderer)renderer.enabled=!useAtlas;
        atlas.SetActive(useAtlas);
    }
}

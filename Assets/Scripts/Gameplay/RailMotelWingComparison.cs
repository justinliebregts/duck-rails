using UnityEngine;

/// <summary>Only attached to the isolated benchmark prefab, injected by an explicit development build.</summary>
[DefaultExecutionOrder(-1100)]
public sealed class RailMotelWingComparison : MonoBehaviour
{
    public GameObject control, atlas;
    void OnEnable()
    {
        if(!Application.isPlaying || !control || !atlas)return;
        bool useAtlas=false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        useAtlas=RailQuestPerformance.MotelWingVariant=="atlas";
        RailQuestPerformance.AppliedMotelWingVariant=useAtlas?"atlas":"control";
#endif
        control.SetActive(!useAtlas);atlas.SetActive(useAtlas);
    }
}

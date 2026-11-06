using Game.AI;
using UnityEngine;

/// <summary>Train-local offline bake. No runtime sampling or rebaking of graph topology.</summary>
public sealed class RailTrainNavigation : MonoBehaviour
{
    public TextAsset bakedGraph;
    public Vector3 rearLanding = new Vector3(0, .03f, -4.1f);
    public Vector3 rearApproach = new Vector3(0, 0, -5.8f);
    // Centers of the existing two-metre side railing gaps (train-local space).
    public Vector3 leftLanding = new Vector3(-1.45f, .03f, 0);
    public Vector3 leftApproach = new Vector3(-2.9f, 0, 0);
    public Vector3 rightLanding = new Vector3(1.45f, .03f, 0);
    public Vector3 rightApproach = new Vector3(2.9f, 0, 0);
    public const int OpeningCount = 3;
    public Vector3 Landing(int opening) => opening == 0 ? rearLanding : opening == 1 ? leftLanding : rightLanding;
    public Vector3 Approach(int opening) => opening == 0 ? rearApproach : opening == 1 ? leftApproach : rightApproach;
    public static string OpeningName(int opening) => opening == 0 ? "Rear" : opening == 1 ? "Left" : opening == 2 ? "Right" : "None";
    public CrawlGraph Graph { get; private set; }
    void Awake()
    {
        Graph = CrawlGraph.Load(bakedGraph);
        if (Graph != null) Graph.Frame = transform;
    }
}

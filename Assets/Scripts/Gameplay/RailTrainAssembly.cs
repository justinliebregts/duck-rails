using TMPro;
using UnityEngine;

/// <summary>Local prefab references; networking and passenger motion remain on RailTrain.</summary>
[RequireComponent(typeof(RailTrain))]
public sealed class RailTrainAssembly : MonoBehaviour
{
    public RailFurnaceModule furnace;
    public Transform chair, controls, radio;
    public TMP_Text fuelGauge, results;
    public TMP_Text[] runningLabels;
    public Transform[] supplyPoints;

    public void Bind(RailExpedition run)
    {
        if (run == null) return;
        run.furnace = furnace.insertion;
        run.gauge = fuelGauge;
        run.results = results;
        run.runningLabels = runningLabels;
        run.supplyPoints = supplyPoints;
    }
    void Start()
    {
        var world = GetComponentInParent<RailWorld>();
        if (world != null && world.train == GetComponent<RailTrain>()) Bind(world.GetComponent<RailExpedition>());
    }
}

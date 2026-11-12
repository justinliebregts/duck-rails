using TMPro;
using UnityEngine;

/// <summary>Train-mounted odometer. Displays the retained train distance rescaled so the bridge area
/// (RailRoute.FinaleStart) reads roughly <see cref="BridgeMetres"/>. Display only; gameplay distances are unchanged.</summary>
public sealed class RailDistanceDisplay : MonoBehaviour
{
    public const float BridgeMetres = 1000;
    public TMP_Text label;
    int lastShown = -1;
    void Update()
    {
        var train = RailTrain.Instance; var route = RailRoute.Instance;
        if (label == null || train == null || route == null || !route.Ready || route.FinaleStart <= 0) return;
        int metres = Mathf.Max(0, Mathf.RoundToInt(train.Distance * BridgeMetres / route.FinaleStart));
        if (metres == lastShown) return;
        lastShown = metres;
        label.text = "DISTANCE  " + metres + " m";
    }
}

using System.Linq;
using Photon.Pun;
using UnityEngine;

/// <summary>Visual coal is not an inventory object until an authoritative hit commits its release.</summary>
public sealed class RailCoalCarrier : MonoBehaviour
{
    public Transform display;
    public string Marker
    {
        get { var d = GetComponent<PhotonView>().InstantiationData; return d != null && d.Length >= 3 ? d[d.Length - 1] as string : null; }
    }
    void LateUpdate()
    {
        if (display == null) return;
        var state = RailExpedition.Instance?.State.carriers.Find(c => c.marker == Marker);
        display.gameObject.SetActive(state != null && state.carrying);
    }
    public void Hit()
    {
        if (PhotonNetwork.IsMasterClient && RailExpedition.Instance != null && !string.IsNullOrEmpty(Marker))
            RailExpedition.Instance.CarrierHit(Marker);
    }
}

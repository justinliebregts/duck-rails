using TMPro;
using UnityEngine;

public sealed class RailPublicTrainButton : MonoBehaviour
{
    public PressableButton button;
    public TMP_Text heading;
    public GameObject panel;
    public const string Title = "Join in-progress\ntrain";

    void Awake() => button.onPressed.AddListener(Join);
    void OnDestroy() => button.onPressed.RemoveListener(Join);

    void Update()
    {
        var session = PhotonSession.Instance;
        bool shown = PhotonSession.IsRailsRoom && PhotonSession.Kind == RailSessionRules.CompoundKind;
        if (panel.activeSelf != shown) panel.SetActive(shown);
        button.interactable = session != null && session.CanJoinPublicTrain;
        string text = session != null && session.LastError == PhotonSession.NoPublicTrain
            ? "No public train\navailable" : Title;
        if (heading.text != text) heading.text = text;
    }

    void Join() => PhotonSession.Instance?.RequestPublicTrain();
}

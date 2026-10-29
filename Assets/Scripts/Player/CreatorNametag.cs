using System.Collections.Generic;
using EasyTextEffects;
using EasyTextEffects.Effects;
using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// Styles a world-space name and shows the Tier 1 badge beside it.
/// Remote players use the Photon nametag. The local body uses a plate on the chest you see looking down.
/// </summary>
public class CreatorNametag : MonoBehaviour
{
    public const string Tier1 = "cc-1";

    static readonly Color DefaultColor = new Color(0.764151f, 0.764151f, 0.764151f, 1f);
    static readonly Color CreatorRed = new Color(1f, 0.18431371f, 0.18431371f, 1f);
    const float OutlineWidth = 0.28f;
    const float ShineDistance = 10f; // Animation budget only; shader fog has no cutoff.

    static Effect_Color shine;
    static GameObject badgePrefab;

    const string AnchorName = "BadgeAnchor";
    static readonly Vector3 DefaultAnchorPosition = new Vector3(-0.22f, 0.08f, 0f);
    const float DefaultAnchorScale = 0.4f;

    TMP_Text label;
    TextEffect textEffect;
    GameObject badge;
    TMP_Text timeAlive;
    bool localPlate;
    bool shineActive;
    int shownTimeAlive = int.MinValue;
    string appliedBadge = "\0";
    string appliedNametag = "\0";

    public static void AttachRemote(TMP_Text label)
    {
        if (label == null || label.GetComponent<CreatorNametag>() != null)
            return;
        CreatorNametag plate = label.gameObject.AddComponent<CreatorNametag>();
        plate.label = label;
        plate.localPlate = false;
        plate.ApplyCurrent();
    }

    public static void BindLocal(Transform chest)
    {
        if (chest == null)
            return;
        Transform name = chest.Find("Name");
        if (name == null)
            return;
        TMP_Text text = name.GetComponent<TMP_Text>();
        if (text == null || name.GetComponent<CreatorNametag>() != null)
            return;

        Transform time = chest.Find("TimeAlive");
        TMP_Text timeLabel = time != null ? time.GetComponent<TMP_Text>() : null;

        CreatorNametag plate = name.gameObject.AddComponent<CreatorNametag>();
        plate.label = text;
        plate.timeAlive = timeLabel;
        plate.localPlate = true;
        plate.RefreshLocalReadout();
        plate.ApplyCurrent();
    }

    void LateUpdate()
    {
        if (localPlate)
        {
            RefreshLocalReadout();
            ApplyCurrent();
        }
        else
        {
            PhotonView view = GetComponentInParent<PhotonView>();
            if (view != null && view.Owner != null)
                Apply(view.Owner.GetBadge(), view.Owner.GetNametag());
        }

        UpdateShineDistance();
    }

    void RefreshLocalReadout()
    {
        if (label != null)
        {
            PlayerSession session = PlayerSession.Instance;
            string displayName = session != null && !string.IsNullOrEmpty(session.DisplayName) ? session.DisplayName : "Duck";
            if (label.text != displayName)
                label.text = displayName;
        }

        if (timeAlive == null)
            return;
        PhotonPlayer local = PhotonPlayer.Local;
        int seconds = local != null ? local.TimeAliveSeconds : 0;
        if (seconds == shownTimeAlive)
            return;
        shownTimeAlive = seconds;
        timeAlive.text = seconds.ToString();
    }

    void ApplyCurrent()
    {
        if (localPlate)
        {
            PlayerSession session = PlayerSession.Instance;
            Apply(session != null ? session.BadgeId : "", session != null ? session.NametagId : "");
            return;
        }

        PhotonView view = GetComponentInParent<PhotonView>();
        if (view != null && view.Owner != null)
            Apply(view.Owner.GetBadge(), view.Owner.GetNametag());
    }

    void Apply(string badgeId, string nametagId)
    {
        badgeId = string.IsNullOrEmpty(badgeId) ? "" : badgeId.ToLowerInvariant();
        nametagId = string.IsNullOrEmpty(nametagId) ? "" : nametagId.ToLowerInvariant();
        if (badgeId == appliedBadge && nametagId == appliedNametag)
            return;
        appliedBadge = badgeId;
        appliedNametag = nametagId;

        if (label == null)
            return;

        bool creatorName = nametagId == Tier1;
        label.color = creatorName ? CreatorRed : DefaultColor;
        label.outlineColor = Color.black;
        label.outlineWidth = creatorName ? OutlineWidth : 0f;

        if (!creatorName)
        {
            DisableShine();
            shineActive = false;
        }

        if (badgeId == Tier1)
            ShowBadge();
        else
            HideBadge();
    }

    void UpdateShineDistance()
    {
        if (label == null || appliedNametag != Tier1)
            return;

        Camera cam = Camera.main;
        bool near = cam == null || Vector3.Distance(transform.position, cam.transform.position) <= ShineDistance;
        if (near == shineActive)
            return;

        shineActive = near;
        if (near)
        {
            EnableShine();
            return;
        }

        DisableShine();
        label.color = CreatorRed;
        label.outlineColor = Color.black;
        label.outlineWidth = OutlineWidth;
        label.ForceMeshUpdate();
    }

    void EnableShine()
    {
        if (textEffect == null)
        {
            textEffect = label.GetComponent<TextEffect>();
            if (textEffect == null)
                textEffect = label.gameObject.AddComponent<TextEffect>();
        }
        Effect_Color effect = Shine();
        if (effect == null)
            return;
        textEffect.text = label;
        textEffect.usePreset = false;
        textEffect.updatesPerSecond = 30;
        textEffect.globalEffects = new List<GlobalTextEffectEntry>
        {
            new GlobalTextEffectEntry
            {
                triggerWhen = TextEffectEntry.TriggerWhen.OnStart,
                effect = effect,
            },
        };
        textEffect.enabled = true;
        textEffect.Refresh();
    }

    void DisableShine()
    {
        if (textEffect == null)
            textEffect = label.GetComponent<TextEffect>();
        if (textEffect == null)
            return;
        textEffect.globalEffects = null;
        textEffect.enabled = false;
    }

    void ShowBadge()
    {
        Transform anchor = EnsureAnchor();
        if (anchor == null || badge != null)
            return;
        GameObject prefab = BadgePrefab();
        if (prefab == null)
            return;
        badge = Instantiate(prefab, anchor);
        badge.transform.localPosition = Vector3.zero;
        badge.transform.localRotation = Quaternion.identity;
        badge.transform.localScale = Vector3.one;
    }

    Transform EnsureAnchor()
    {
        if (label == null)
            return null;
        Transform anchor = label.transform.Find(AnchorName);
        if (anchor != null)
            return anchor;

        var anchorObject = new GameObject(AnchorName);
        anchorObject.layer = label.gameObject.layer;
        anchor = anchorObject.transform;
        anchor.SetParent(label.transform, false);
        if (!CopyAnchor(anchor))
        {
            anchor.localPosition = DefaultAnchorPosition;
            anchor.localRotation = Quaternion.Euler(0f, 180f, 0f);
            anchor.localScale = Vector3.one * DefaultAnchorScale;
        }
        return anchor;
    }

    static bool CopyAnchor(Transform anchor)
    {
        foreach (PhotonPlayer player in PhotonPlayer.Active)
        {
            if (player == null)
                continue;
            TMP_Text[] labels = player.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null || labels[i].gameObject.name != "Name")
                    continue;
                Transform template = labels[i].transform.Find(AnchorName);
                if (template == null || template == anchor)
                    continue;
                anchor.localPosition = template.localPosition;
                anchor.localRotation = template.localRotation;
                anchor.localScale = template.localScale;
                return true;
            }
        }
        return false;
    }

    void HideBadge()
    {
        if (badge == null)
            return;
        Destroy(badge);
        badge = null;
    }

    static Effect_Color Shine()
    {
        if (shine == null)
            shine = Resources.Load<Effect_Color>("Creator/cc-1_Color");
        return shine;
    }

    static GameObject BadgePrefab()
    {
        if (badgePrefab == null)
            badgePrefab = Resources.Load<GameObject>("Creator/Badge_CC");
        return badgePrefab;
    }
}

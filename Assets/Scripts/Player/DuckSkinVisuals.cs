using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum DuckSkinSource
{
    /// <summary>This client's equipped skin (the body you see when you look down).</summary>
    Local,
    /// <summary>The owning player's Photon "skin" property.</summary>
    Networked,
    /// <summary>Only changes when Apply is called (ragdolls).</summary>
    Manual,
}

/// <summary>
/// Dresses a duck rig in a skin. The rig is never replaced, so IK, fingers and ragdoll posing keep their bones.
/// The skin's body mesh is rebound to this rig's bones by name, and objects placed under the skin's bones
/// (face, hats) are copied onto the matching bones here.
/// </summary>
public class DuckSkinVisuals : MonoBehaviourPunCallbacks
{
    static readonly string[] FaceNames = { "Eye_L", "Eye_R", "Lip_T", "Lip_B" };

    class SkinModel
    {
        public Mesh mesh;
        public Material[] materials;
        public string[] boneNames;
        public string rootBoneName;
        public readonly List<(GameObject source, string bone)> attachments = new List<(GameObject, string)>();
        public bool hasFace;
    }

    struct RestPose
    {
        public Transform transform;
        public Quaternion rotation;
        public Vector3 scale;
    }

    static readonly Dictionary<GameObject, SkinModel> ModelCache = new Dictionary<GameObject, SkinModel>();

    [SerializeField] DuckSkinSource source = DuckSkinSource.Manual;

    PhotonView view;
    SkinnedMeshRenderer body;
    PlayerArmGeometry armGeometry;
    Mesh defaultMesh;
    Material[] defaultMaterials;
    Transform[] defaultBones;
    Transform defaultRootBone;
    readonly Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
    readonly List<GameObject> defaultFaceRoots = new List<GameObject>();
    readonly Transform[] defaultFace = new Transform[4];
    readonly Material[] defaultEyeMaterials = new Material[2];
    readonly List<RestPose> defaultFaceRest = new List<RestPose>();
    readonly List<GameObject> attachments = new List<GameObject>();
    bool initialized;
    int appliedId = -1;
    int colorIndex;
    int colorActor;
    MaterialPropertyBlock tintBlock;
    MaterialPropertyBlock corpseFaceBlock;
    static readonly int TintId = Shader.PropertyToID("_DuckTint");
    static readonly int TintEnabledId = Shader.PropertyToID("_DuckTintEnabled");

    // Ragdolls keep their last color after the owner leaves, but can receive a late assignment.
    public void BindColor(int actor)
    {
        if (colorActor != actor) colorIndex = 0;
        colorActor = actor;
        RefreshColor();
    }

    void RefreshColor()
    {
        int actor = source == DuckSkinSource.Local ? PhotonNetwork.LocalPlayer.ActorNumber :
            source == DuckSkinSource.Networked && view != null ? view.OwnerActorNr : colorActor;
        if (DuckPlayerColors.TryGet(actor, out int index)) colorIndex = index;
        else if (source != DuckSkinSource.Manual) colorIndex = 0;
        ApplyTint();
    }

    void ApplyTint()
    {
        Init();
        if (body == null) return;
        if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
        body.GetPropertyBlock(tintBlock);
        tintBlock.SetColor(TintId, DuckPlayerColors.Tint(colorIndex));
        // Keep the assigned color while wearing a cosmetic skin; only Default uses it.
        tintBlock.SetFloat(TintEnabledId, appliedId <= 0 && colorIndex != 0 ? 1f : 0f);
        body.SetPropertyBlock(tintBlock);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(DuckPlayerColors.Property)) RefreshColor();
    }

    public override void OnJoinedRoom() => RefreshColor();
    public override void OnLeftRoom() => RefreshColor();

    public static DuckSkinVisuals Attach(GameObject rig, DuckSkinSource source)
    {
        if (!rig.TryGetComponent(out DuckSkinVisuals visuals))
            visuals = rig.AddComponent<DuckSkinVisuals>();
        visuals.source = source;
        return visuals;
    }

    /// <summary>The skin this client has equipped, or 0 for the default duck.</summary>
    public static byte LocalSkinId()
    {
        StoreItem item = EquippedLoadout.Instance != null ? EquippedLoadout.Instance.Get(StoreItemType.Skin) : null;
        return item != null ? item.skinId : (byte)0;
    }

    string Who()
    {
        string owner = view != null && view.Owner != null ? $", owner {view.Owner.NickName}#{view.Owner.ActorNumber}{(view.IsMine ? " (me)" : "")}" : "";
        return $"{source} '{name}'{owner}";
    }

    void Start()
    {
        DeathProfile.Trace($"[DuckSkin] Start {source} '{name}'", this);
        switch (source)
        {
            case DuckSkinSource.Local:
                EquippedLoadout.Ensure().OnEquipped += OnLocalEquipped;
                Apply(LocalSkinId());
                break;
            case DuckSkinSource.Networked:
                view = GetComponentInParent<PhotonView>();
                if (view != null && view.Owner != null)
                    Apply(view.Owner.GetSkin());
                else
                    {
                        if (DuckzLog.AllowWarning(out var logNotice1))
                            logNotice1.Write($"[DuckSkin] {Who()}: no PhotonView owner, can't read the skin property.", this);
                    }
                break;
        }
        RefreshColor();
    }

    void OnDestroy()
    {
        if (EquippedLoadout.Instance != null)
            EquippedLoadout.Instance.OnEquipped -= OnLocalEquipped;
    }

    void OnLocalEquipped(StoreItemType type, StoreItem item)
    {
        if (type != StoreItemType.Skin)
            return;
        DeathProfile.Trace($"[DuckSkin] {Who()}: equipped '{(item != null ? item.key : "default")}'", this);
        Apply(item != null ? item.skinId : (byte)0);
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player target, Hashtable changed)
    {
        if (source != DuckSkinSource.Networked || view == null || target != view.Owner || !changed.ContainsKey(PlayerProps.Skin))
            return;
        DeathProfile.Trace($"[DuckSkin] {Who()}: Photon skin property changed to {target.GetSkin()}", this);
        Apply(target.GetSkin());
    }

    public void Apply(byte skinId)
    {
        Init();
        if (body == null)
            return;
        if (appliedId == skinId)
        {
            DeathProfile.Trace($"[DuckSkin] {Who()}: skin {skinId} already applied", this);
            return;
        }
        appliedId = skinId;

        ClearAttachments();
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        GameObject modelAsset = catalog != null ? catalog.FindSkinModel(skinId) : null;
        SkinModel model = modelAsset != null ? Analyze(modelAsset) : null;
        if (catalog == null)
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write($"[DuckSkin] {Who()}: no store catalog in GameConfig.", this);
            }

        if (model == null || !TryApplyBody(model))
        {
            if (skinId != 0)
                {
                    if (DuckzLog.AllowWarning(out var logNotice3))
                        logNotice3.Write($"[DuckSkinVisuals] Skin {skinId} has no usable model. Showing the default duck.", this);
                }
            model = null;
            body.sharedMesh = defaultMesh;
            body.sharedMaterials = defaultMaterials;
            body.bones = defaultBones;
            body.rootBone = defaultRootBone;
        }

        if (model != null)
        {
            foreach ((GameObject src, string boneName) in model.attachments)
            {
                if (!boneMap.TryGetValue(boneName, out Transform bone))
                    continue;
                GameObject copy = Instantiate(src, bone, false);
                copy.name = src.name;
                attachments.Add(copy);
            }
        }

        bool skinFace = model != null && model.hasFace;
        foreach (GameObject root in defaultFaceRoots)
            if (root != null)
                root.SetActive(!skinFace);

        if (skinFace)
        {
            RestoreDefaultEyeMaterials();
            BindFace(FindFace(attachments));
        }
        else
        {
            foreach (RestPose rest in defaultFaceRest)
                if (rest.transform != null)
                {
                    rest.transform.localRotation = rest.rotation;
                    rest.transform.localScale = rest.scale;
                }
            ApplyEyeMaterial(catalog != null ? catalog.FindSkin(skinId) : null);
            BindFace(defaultFace);
        }

        ApplyTint();

        // The owner's own Photon body stays invisible; PhotonPlayer hides it once at Start.
        if (source == DuckSkinSource.Networked && view != null && view.IsMine)
            foreach (GameObject copy in attachments)
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

        DeathProfile.Trace($"[DuckSkin] {Who()}: applied skin {skinId} -> model '{(model != null ? modelAsset.name : "default")}', " +
                  $"mesh '{(body.sharedMesh != null ? body.sharedMesh.name : "none")}', {body.bones.Length} bones, " +
                  $"{attachments.Count} attachments [{string.Join(", ", attachments.ConvertAll(a => a.name + " on " + a.transform.parent.name))}], " +
                  $"face {(skinFace ? "from skin" : "default")}", this);
    }

    bool TryApplyBody(SkinModel model)
    {
        if (model.mesh == null)
            return false;
        var bones = new Transform[model.boneNames.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            if (model.boneNames[i] == null)
                continue;
            if (!boneMap.TryGetValue(model.boneNames[i], out bones[i]))
            {
                {
                    if (DuckzLog.AllowWarning(out var logNotice4, model.boneNames[i]))
                        logNotice4.Write($"[DuckSkinVisuals] Skin bone '{model.boneNames[i]}' is not on this rig.", this);
                }
                return false;
            }
        }
        body.sharedMesh = armGeometry != null ? armGeometry.Fit(model.mesh, bones) : model.mesh;
        body.sharedMaterials = model.materials;
        body.bones = bones;
        body.rootBone = model.rootBoneName != null && boneMap.TryGetValue(model.rootBoneName, out Transform root) ? root : defaultRootBone;
        return true;
    }

    /// <summary>Copy a companion's rendered appearance onto this corpse's own rig.</summary>
    public bool CopyCompanionAppearance(Transform sourceRig, bool redEyes = false)
    {
        Init();
        SkinnedMeshRenderer original = MainRenderer(sourceRig);
        if (body == null || original == null) return false;
        var model = new SkinModel {
            mesh = original.sharedMesh,
            materials = original.sharedMaterials,
            boneNames = System.Array.ConvertAll(original.bones, bone => bone != null ? bone.name : null),
            rootBoneName = original.rootBone != null ? original.rootBone.name : null
        };
        // Rebind by name; never make the corpse depend on the living duck's bones.
        if (!TryApplyBody(model)) return false;
        ClearAttachments();
        foreach (GameObject root in defaultFaceRoots)
            if (root != null) root.SetActive(true);
        foreach (string name in FaceNames)
        {
            Transform from = FindDeep(sourceRig, name);
            Transform to = FindDeep(transform, name);
            if (from == null || to == null) continue;
            var sourceMesh = from.GetComponent<MeshFilter>();
            var targetMesh = to.GetComponent<MeshFilter>();
            if (sourceMesh != null && targetMesh != null) targetMesh.sharedMesh = sourceMesh.sharedMesh;
            var sourceRenderer = from.GetComponent<Renderer>();
            var targetRenderer = to.GetComponent<Renderer>();
            if (sourceRenderer != null && targetRenderer != null)
            {
                targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                // Reuse must also clear the previous death's killer-eye override.
                if (corpseFaceBlock == null) corpseFaceBlock = new MaterialPropertyBlock();
                sourceRenderer.GetPropertyBlock(corpseFaceBlock);
                if (redEyes && (name == "Eye_L" || name == "Eye_R"))
                    corpseFaceBlock.SetColor("_EmissionColor", Color.red);
                targetRenderer.SetPropertyBlock(corpseFaceBlock);
            }
        }
        appliedId = 256; // Companion appearance is not a player/store skin ID.
        colorActor = colorIndex = 0;
        ApplyTint();
        return true;
    }

    void Init()
    {
        if (initialized)
            return;
        initialized = true;

        body = MainRenderer(transform);
        if (body == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice5))
                    logNotice5.Write("[DuckSkinVisuals] No skinned body found under this rig.", this);
            }
            return;
        }
        armGeometry = GetComponent<PlayerArmGeometry>();
        if (armGeometry != null) armGeometry.Prepare(body);
        defaultMesh = body.sharedMesh;
        defaultMaterials = body.sharedMaterials;
        defaultBones = body.bones;
        defaultRootBone = body.rootBone;

        HashSet<Transform> boneSet = BoneSet(body, transform);
        foreach (Transform bone in boneSet)
            if (!boneMap.ContainsKey(bone.name))
                boneMap.Add(bone.name, bone);

        for (int i = 0; i < FaceNames.Length; i++)
        {
            Transform part = FindDeep(transform, FaceNames[i]);
            defaultFace[i] = part;
            if (part == null)
                continue;
            defaultFaceRest.Add(new RestPose { transform = part, rotation = part.localRotation, scale = part.localScale });
            if (i < 2 && part.TryGetComponent(out Renderer eyeRenderer))
                defaultEyeMaterials[i] = eyeRenderer.sharedMaterial;
            GameObject root = AttachmentRoot(part, boneSet, transform);
            if (root != null && !defaultFaceRoots.Contains(root))
                defaultFaceRoots.Add(root);
        }

        DeathProfile.Trace($"[DuckSkin] Init {source} '{name}': body '{body.name}' mesh '{(defaultMesh != null ? defaultMesh.name : "none")}', " +
                  $"{boneMap.Count} rig bones, default face parts [{string.Join(", ", System.Array.ConvertAll(defaultFace, f => f != null ? f.name : "missing"))}], " +
                  $"face roots [{string.Join(", ", defaultFaceRoots.ConvertAll(r => r.name))}]", this);
    }

    void ClearAttachments()
    {
        foreach (GameObject copy in attachments)
        {
            if (copy == null)
                continue;
            // Detach now so name-based bone lookups this frame (ragdoll posing) can't find it.
            copy.transform.SetParent(null, false);
            Destroy(copy);
        }
        attachments.Clear();
    }

    void BindFace(Transform[] face)
    {
        PhotonEyeLook eyes = GetComponentInParent<PhotonEyeLook>();
        if (eyes != null)
            eyes.Bind(face[0], face[1]);
        PhotonVoiceMouth mouth = GetComponentInParent<PhotonVoiceMouth>();
        if (mouth != null)
            mouth.Bind(face[2], face[3]);
        if (eyes != null || mouth != null)
            DeathProfile.Trace($"[DuckSkin] {Who()}: face bound, eye look {(eyes != null ? "yes" : "no")}, lip sync {(mouth != null ? "yes" : "no")}, " +
                      $"parts [{string.Join(", ", System.Array.ConvertAll(face, f => f != null ? f.GetInstanceID() + ":" + f.name : "missing"))}]", this);
    }

    void ApplyEyeMaterial(StoreItem skin)
    {
        Material overrideMat = skin != null ? skin.eyeMaterial : null;
        for (int i = 0; i < 2; i++)
        {
            Transform part = defaultFace[i];
            if (part == null || !part.TryGetComponent(out Renderer eyeRenderer))
                continue;
            eyeRenderer.sharedMaterial = overrideMat != null ? overrideMat : defaultEyeMaterials[i];
        }
    }

    void RestoreDefaultEyeMaterials() => ApplyEyeMaterial(null);

    static Transform[] FindFace(List<GameObject> roots)
    {
        var face = new Transform[FaceNames.Length];
        for (int i = 0; i < FaceNames.Length; i++)
            foreach (GameObject root in roots)
                if ((face[i] = FindDeep(root.transform, FaceNames[i])) != null)
                    break;
        return face;
    }

    static SkinModel Analyze(GameObject asset)
    {
        if (ModelCache.TryGetValue(asset, out SkinModel cached))
            return cached;

        var model = new SkinModel();
        SkinnedMeshRenderer smr = MainRenderer(asset.transform);
        if (smr != null)
        {
            model.mesh = smr.sharedMesh;
            model.materials = smr.sharedMaterials;
            Transform[] bones = smr.bones;
            model.boneNames = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                model.boneNames[i] = bones[i] != null ? bones[i].name : null;
            model.rootBoneName = smr.rootBone != null ? smr.rootBone.name : null;

            HashSet<Transform> boneSet = BoneSet(smr, asset.transform);
            foreach (Transform bone in boneSet)
                foreach (Transform child in bone)
                    if (!boneSet.Contains(child))
                    {
                        model.attachments.Add((child.gameObject, bone.name));
                        foreach (string faceName in FaceNames)
                            if (FindDeep(child, faceName) != null)
                                model.hasFace = true;
                    }
        }

        ModelCache[asset] = model;
        DeathProfile.Trace($"[DuckSkin] Analyzed skin model '{asset.name}': body '{(smr != null ? smr.name : "none")}', " +
                  $"{(model.boneNames != null ? model.boneNames.Length : 0)} bones, root bone '{model.rootBoneName}', " +
                  $"attachments [{string.Join(", ", model.attachments.ConvertAll(a => a.source.name + " on " + a.bone))}], has face {model.hasFace}");
        return model;
    }

    // The body is the renderer driven by the most bones; eyes and lips are separate, smaller meshes.
    static SkinnedMeshRenderer MainRenderer(Transform root)
    {
        SkinnedMeshRenderer best = null;
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (best == null || smr.bones.Length > best.bones.Length)
                best = smr;
        return best;
    }

    static HashSet<Transform> BoneSet(SkinnedMeshRenderer smr, Transform root)
    {
        var set = new HashSet<Transform>();
        foreach (Transform bone in smr.bones)
            for (Transform t = bone; t != null && t != root; t = t.parent)
                if (!set.Add(t))
                    break;
        if (smr.rootBone != null && smr.rootBone != root)
            set.Add(smr.rootBone);
        return set;
    }

    // The object hanging off a bone (or the rig root) that contains part, e.g. Eyes_Default for Eye_L.
    static GameObject AttachmentRoot(Transform part, HashSet<Transform> boneSet, Transform rigRoot)
    {
        Transform t = part;
        while (t.parent != null && t.parent != rigRoot && !boneSet.Contains(t.parent))
            t = t.parent;
        return t.parent != null && t != rigRoot ? t.gameObject : null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}

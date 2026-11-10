#if UNITY_EDITOR
using System;
using System.Linq;
using Game.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public static class RailParkedHelicopterSetup
{
    public const string VariantPath = "Assets/DuckRails/Economy/RailParkedHelicopter.prefab";
    static void Ref(Object target, string name, Object value)
    { var s = new SerializedObject(target); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }

    public static void Install()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Wrong project or Play Mode");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("Save current prefab edits before installing");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) != null) throw new InvalidOperationException("Variant exists; preserve authored edits");
        var scene = EditorSceneManager.NewPreviewScene();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        try
        {
            root.name = "RailParkedHelicopter";
            var heli = root.GetComponent<Helicopter>();
            heli.swayBobMeters = heli.swayPitchDegrees = heli.swayRollDegrees = 0;
            var store = root.GetComponentInChildren<StoreUI>(true);
            Ref(store,"catalogOverride",AssetDatabase.LoadAssetAtPath<StoreCatalog>("Assets/DuckRails/Economy/RailStoreCatalog.asset"));
            store.onConsumablePurchased = new UnityEvent<string>();
            foreach (var display in root.GetComponentsInChildren<StoreWallItem>(true))
            {
                var s = new SerializedObject(display);
                bool keep = RailEconomyBuilder.Keys.Contains(s.FindProperty("itemKey").stringValue);
                var label = s.FindProperty("label").objectReferenceValue as TMP_Text;
                display.gameObject.SetActive(keep);
                if (label != null) label.gameObject.SetActive(keep);
                if (keep) Ref(display,"store",store);
            }
            foreach(var label in root.GetComponentsInChildren<StoreBundleLabel>(true)) label.gameObject.SetActive(false);
            foreach(var trigger in root.GetComponentsInChildren<HelicopterStoreTrigger>(true)) Object.DestroyImmediate(trigger);
            var oldCrate = root.GetComponentInChildren<CrateGoldBar>(true);
            var crate = oldCrate.gameObject;
            Object.DestroyImmediate(oldCrate);
            var receiver = crate.AddComponent<RailGoldReceiver>();
            receiver.receiverId=2;
            receiver.intake=crate.GetComponent<BoxCollider>();receiver.intake.isTrigger=true;
            receiver.coins=crate.GetComponent<CoinShower>();
            // Reuse the existing Rails label styling without replacing the cabin crate.
            var exchange=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Economy/GoldExchange.prefab").GetComponent<RailGoldReceiver>();
            var labelObject=Object.Instantiate(exchange.label.gameObject,crate.transform);
            labelObject.name="Rails gold deposit label";
            labelObject.transform.localPosition=new Vector3(0,1.8f,0);
            labelObject.transform.localScale=Vector3.one*.65f;
            receiver.label=labelObject.GetComponent<TMP_Text>();
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            { text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");text.fontSharedMaterial=text.font.material; }
            var presentation=root.AddComponent<RailParkedHelicopter>();
            presentation.engineSound=root.GetComponentInChildren<SoundEmitter>(true);
            var dust=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/HelicopterGroundDust.prefab"),root.transform);
            dust.name="Concrete rotor wash";dust.transform.localPosition=new Vector3(0,-.05f,0);
            var wash=dust.GetComponent<HelicopterGroundDust>();wash.fullEmissionRate=16;wash.maxParticles=32;wash.spawnRadius=new Vector2(3,7);wash.darkColor=new Color(.3f,.3f,.27f);wash.lightColor=new Color(.58f,.56f,.5f);
            presentation.rotorWash=wash;
            PrefabUtility.SaveAsPrefabAsset(root,VariantPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }

        var block=PrefabUtility.LoadPrefabContents(RailHelipadPreview.Path);
        try
        {
            var env=block.transform.Find("Env");
            if(env.Find("Parked helicopter")!=null)throw new InvalidOperationException("Already installed");
            var mount=new GameObject("Parked helicopter").transform;mount.SetParent(env,false);
            mount.localPosition=new Vector3(-23,.15f,30);
            var parked=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath),mount);
            // Preserve source scale and heading: both side cabin doors open toward pad edges.
            // Fit real renderer footprint to pad center and physical landing gear to its surface.
            var renderers=parked.transform.Find("HelicopterMesh").GetComponentsInChildren<Renderer>(true);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            var colliders=parked.transform.Find("HelicopterMesh").GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger).ToArray();
            float bottom=colliders.Min(c=>c.bounds.min.y);
            parked.transform.position+=new Vector3(mount.position.x-bounds.center.x,mount.position.y-bottom,mount.position.z-bounds.center.z);
            PrefabUtility.SaveAsPrefabAsset(block,RailHelipadPreview.Path);
            Debug.Log("Parked helicopter installed. Source visual bounds="+bounds.size+" ground clearance fitted from physical collider minimum.");
        }
        finally { PrefabUtility.UnloadPrefabContents(block); }
    }

    public static void Validate()
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(RailHelipadPreview.Path);
        var heli=root.GetComponentInChildren<Helicopter>(true);
        if(heli==null)throw new Exception("Missing helicopter");
        var store=heli.GetComponentInChildren<StoreUI>();
        var displays=heli.GetComponentsInChildren<StoreWallItem>();
        if(displays.Length!=7 || store.Catalog.items.Count!=7)throw new Exception("Wrong catalogue/display count");
        foreach(var d in displays)
        {
            var s=new SerializedObject(d);var key=s.FindProperty("itemKey").stringValue;
            if(!RailEconomyBuilder.Keys.Contains(key)||s.FindProperty("store").objectReferenceValue!=store)throw new Exception("Wrong display binding");
            var item=store.Find(key);
            if(item==null || item.durable)throw new Exception("Not Rails consumable "+key);
            // Bags use BagInventory's Rails-specific spawn path rather than StoreItem.prefab.
            if(key=="bag_basic") { if(item.type!=StoreItemType.Bag || item.bagModel==null)throw new Exception("Wrong bag binding"); }
            else if(item.prefab==null || item.prefab.GetComponent<RailCargo>()==null)throw new Exception("Not Rails cargo "+key);
            var label=s.FindProperty("label").objectReferenceValue as TMP_Text;
            if(label==null || !label.transform.IsChildOf(heli.transform))throw new Exception("Foreign price label");
        }
        if(root.GetComponentsInChildren<RailGoldReceiver>(true).Length!=1 || heli.GetComponentInChildren<RailGoldReceiver>().receiverId!=2 || heli.GetComponentInChildren<CrateGoldBar>(true)!=null)throw new Exception("Gold binding");
        if(heli.GetComponentInChildren<HelicopterMirror>()==null || heli.GetComponentInChildren<SoundEmitter>()==null || heli.GetComponentInChildren<HelicopterStoreTrigger>(true)!=null || root.GetComponentInChildren<HelicopterManager>(true)!=null)throw new Exception("Presentation dependency");
        var fx=heli.GetComponent<RailParkedHelicopter>();if(fx.rotorWash==null||fx.rotorWash.particleMesh==null||fx.rotorWash.particleMaterial==null)throw new Exception("Dust missing");
        if(heli.swayBobMeters!=0||heli.swayPitchDegrees!=0||heli.swayRollDegrees!=0||heli.propellerRotateSpeed<=0)throw new Exception("Stationary rotor settings");
        foreach(var t in heli.GetComponentsInChildren<TMP_Text>(true))if(t.font.name!="CutePixel SDF")throw new Exception("Wrong font");
        Debug.Log("Rail parked helicopter authoring checks passed: seven Rails cargo displays/prices, one shared midpoint intake, camera/audio/dust preserved, no flight manager or dependent trigger.");
    }
}
#endif

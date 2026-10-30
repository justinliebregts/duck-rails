using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.XR.Management;
namespace GR {
    public class Player : MonoBehaviour {
        public static Player instance;
        public PlayerHand leftPlayerHand;
        public PlayerHand rightPlayerHand;
        public Transform bottom;
        public PlayerHUD playerHUD;
        public GameObject bloodDecalSpawner;
        public DeathFlare deathFlarePrefab;
        public Ragdoll ragdoll;

        ControllerInput controllerInput;

        bool isDead;
        // PhotonStream takes objects. Box once per death, not on every dead-state packet.
        public object DeathImpactState { get; private set; } = Vector3.zero;
        public object DeathRotationState { get; private set; } = Vector3.zero;
        readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();

        public bool IsDead {
            get => isDead;
            set {
                using var sample = DeathProfile.Equipment.Auto();
                if (isDead == value) return;
                isDead = value;
                if (value) {
                    BagInventory.InvalidateLife();
                    foreach (var menu in FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        menu.CloseForDeath();
                    EquipmentInventory.Clear();
                    if (leftPlayerHand != null) leftPlayerHand.Release();
                    if (rightPlayerHand != null) rightPlayerHand.Release();
                    // Detach equipment before hiding the body so dropped items stay visible.
                    foreach (var equipment in GetComponentsInChildren<HeadEquipment>(true))
                        equipment.DropOnDeath();
                    var bags = BagInventory.Local != null ? BagInventory.Local : GetComponentInChildren<BagInventory>(true);
                    if (bags != null) bags.DropAllOnDeath();
                    else BagInventory.DropWorldBagsOnDeath();
                }
                SetDeadVisibility(value);
            }
        }

        void SetDeadVisibility(bool dead) {
            if (dead) {
                foreach (var body in GetComponentsInChildren<Game.Animation.RemotePlayerBody>(true)) {
                    if (ragdoll != null && body.transform.IsChildOf(ragdoll.transform)) continue;
                    HideRenderers(body);
                }
                foreach (var watch in GetComponentsInChildren<Watch>(true))
                    HideRenderers(watch);
                return;
            }

            foreach (var entry in hiddenRenderers)
                if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
            hiddenRenderers.Clear();
        }

        void HideRenderers(Component visual) {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) {
                if (hiddenRenderers.ContainsKey(renderer)) continue;
                hiddenRenderers.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        public ButtonStatus ReturnButtonPress_Left(ButtonName buttonName) => controllerInput == null ? new ButtonStatus() : controllerInput.ReturnButtonPress(buttonName, leftPlayerHand);
        public ButtonStatus ReturnButtonPress_Right(ButtonName buttonName) => controllerInput == null ? new ButtonStatus() : controllerInput.ReturnButtonPress(buttonName, rightPlayerHand);

        public Transform helmetAnchor;

        private void Awake() {
            instance = this;
            if (GetComponent<EquipmentInventory>() == null)
                gameObject.AddComponent<EquipmentInventory>();
        }

        void Start()
        {
            BloodDecalSpawner.Prewarm(bloodDecalSpawner);
            DeathFlare.Prewarm(deathFlarePrefab);
            if (ragdoll != null) ragdoll.Prepare(transform);
            controllerInput = GetComponent<ControllerInput>();
        }

        public void SetDead() => SetDead("unknown");

        public void SetDead(string cause) => SetDead(cause, Vector3.zero);

        public void SetDead(string cause, Vector3 impact, Vector3 rotation = default) => SetDeadInternal(null, 0, cause, impact, rotation);

        public void SetDeadFromDuck(DuckCompanion killer, int attempt) => SetDeadInternal(killer, attempt, "duck", Vector3.zero);

        void SetDeadInternal(DuckCompanion killer, int attempt, string cause, Vector3 impact, Vector3 rotation = default) {
            if (PhotonSession.IsRailsRoom) { RailExpedition.RequestDown(); return; }
            using var sample = DeathProfile.Local.Auto();
            if (IsDead) {
                return;
            }
            DeathImpactState = impact;
            DeathRotationState = rotation;
            float aliveSeconds = DuckzAnalytics.AliveSeconds();
            string held = HeldItemName();
            IsDead = true;
            if (cause == "duck")
                DuckzAnalytics.DuckKillSuccess();
            DuckzAnalytics.PlayerDied(cause, aliveSeconds, held);
            Vector3 flarePosition = transform.position + Vector3.up * 0.35f;
            DeathFlare.SpawnAfterDeath(deathFlarePrefab, flarePosition);
            PhotonPlayer.BroadcastDeathFlare(flarePosition);
            playerHUD.EnableDeathEffect();
            StartCoroutine(Respawn(killer, attempt));
            Vector3 decalSpawnPos = transform.position;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)) {
                decalSpawnPos = hit.point + hit.normal * 0.01f;
            }
            var driver = GetComponentInChildren<Game.Animation.LocalBodyDriver>(true);
            if (driver == null && Game.Animation.LocalBodyDriver.Active.Count > 0)
                driver = Game.Animation.LocalBodyDriver.Active[0];
            DeathProfile.Trace($"[Ragdoll] Local death: skin {DuckSkinVisuals.LocalSkinId()}, velocity source {(driver != null ? driver.name : "none (no launch)")}", this);
            ragdoll.ApplySkin(DuckSkinVisuals.LocalSkinId(), Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
            RagdollLaunch launch = driver != null ? driver.DeathLaunch() : default;
            launch.impact = impact;
            launch.rotation = rotation;
            ragdoll.PoseToPlayer(transform, launch);
            ragdoll.transform.parent = null;
            int bloodSeed = UnityEngine.Random.Range(1, int.MaxValue);
            BloodDecalSpawner.Spawn(bloodDecalSpawner, decalSpawnPos, bloodSeed);
            PhotonPlayer.BroadcastDeathBlood(decalSpawnPos, bloodSeed);
        }

        string HeldItemName()
        {
            Grabbable right = rightPlayerHand != null ? rightPlayerHand.Held : null;
            Grabbable left = leftPlayerHand != null ? leftPlayerHand.Held : null;
            Grabbable held = right != null ? right : left;
            return held != null ? held.name : null;
        }

        IEnumerator Respawn(DuckCompanion killer, int attempt) {
            if (killer == null) yield return new WaitForSeconds(5);
            else {
                double started = Time.realtimeSinceStartupAsDouble;
                double finished = double.PositiveInfinity;
                bool heard = false;
                while (IsDead) {
                    double now = Time.realtimeSinceStartupAsDouble;
                    // Absolute local deadline: missing RPCs, failed TTS, disconnects,
                    // paused game time and destroyed companions cannot extend this wait.
                    if (DuckCompanion.ShouldRespawnAfterKill(now - started, now - finished)) break;
                    if (killer != null && killer.TryGetAfterKillSpeech(attempt, out int line)) {
                        bool matching = killer.Dialogue.SpeechSequence == line;
                        heard |= matching && killer.Dialogue.IsSpeaking;
                        if (heard && double.IsPositiveInfinity(finished) && (!matching || !killer.Dialogue.Busy))
                            finished = now;
                    }
                    if (DuckCompanion.ShouldRespawnAfterKill(now - started, now - finished)) break;
                    yield return null;
                }
            }
            if (!IsDead) yield break;
            GameManager.instance.RespawnPlayer();
            ragdoll.transform.parent = transform;
        }
    }
}

# Duckz audio

The AudioManager lives on the **Audio** child of `Assets/Prefabs/Manager/GameManager.prefab`, so it belongs to the scene and is rebuilt on each scene load (sounds still playing when a scene unloads are cut off). Scenes without one, such as Login, get a fallback manager created automatically on first use; a scene-placed manager always takes over from a fallback. Each manager prewarms 24 sources. Configure the budget and mixer groups in `Assets/Resources/GameAudioSettings.asset` before entering Play mode. The project currently permits 32 real voices, so 24 SFX sources leave some headroom for Photon Voice; this is a starting budget, not a Quest performance guarantee.

## Create and audition a sound

Use **Create > DuckZ > Audio > Sound Event**. Add clips, set pitch/volume ranges, distance, priority and maximum instances. Select the asset and press **Preview** repeatedly to hear fresh selections without entering Play mode. The selected clip, length, pitch and volume are displayed. **Stop** ends the preview. Preview is dry, 2D and unmixed; it deliberately bypasses scene distance, the mixer and reverb. Preview stops on selection change, compilation and entering Play mode. Preview is disabled during Play mode so it cannot interfere with the game listener.

Lower priority numbers mean more important sounds. A maximum instance count of zero means no limit beyond pool capacity. "Avoid immediate repeat" is tracked by the AudioManager per SoundEvent, game-wide; callers don't manage it. Null clip slots are ignored. Custom rolloff curves are outside this first version; choose linear or logarithmic. Cull Distance rejects distant one-shot requests independently of Unity's Max Distance. Once started, one-shots complete normally.

Starter events under `Assets/Audio/Events` reference existing project clips. The spider footstep event has one clip, so it varies pitch but cannot vary recordings until more clips are added. Hand and weapon impact events per surface live in `Assets/Audio/Events/Impact`. Ambience assets contain the existing variations. Loop boundaries and final loudness need listening/tuning.

## Call from gameplay

```csharp
using Game.Audio;

[SerializeField] SoundEvent impact;

void Hit(Vector3 point)
{
    AudioManager.instance?.Play(impact, point);
}
```

For a sound following an enemy or prop:

```csharp
AudioManager.instance.PlayAttached(roar, mouth, this);
```

The optional owner lets the manager detect a disabled/destroyed component, as well as a disabled/destroyed followed GameObject. There is no need to add an AudioSource to the caller. A `SoundEmitter` exposes `Play`, `StartLoop`, `StopLoop` and `FadeOutLoop(float)` for UnityEvents and animation events.

## Conditional loops

```csharp
AudioHandle hum;

void BeginRepair()
{
    if (!hum.IsPlaying)
        hum = AudioManager.instance.PlayLoop(machineHum, transform, this);
}

void CompleteRepair() => hum.Stop(fadeOut: 0.3f);
void OnDisable() => hum.Stop();
```

The selected clip/pitch stays fixed through the loop. Explicit gameplay loops started with `PlayLoop` hold one source until stopped and are protected from stealing. Ambience started with `PlayAmbience` is budgeted and reclaimable. Stop is idempotent, including during a fade. Handles use a generation check, so a stale handle cannot stop a replacement sound. IsPlaying means the lease is active (including loading/fading), not necessarily audible. Starting can fail when capacity or concurrency is exhausted, returning an inactive handle. Explicit gameplay loops stay alive out of range; use AmbientEmitter for loops that should release their source with distance. Pitch is positive, 0.1–3; reverse/zero-pitch playback is not supported.

## Environment

Add an `AmbientEmitter` component to a scene GameObject, assign a SoundEvent from `Assets/Audio/Events/Quackville`, and tune Loop, activation distance and interval. Empty Points plays at the emitter; otherwise it randomly chooses among active points within range. One-shot delays are randomized; eligible loops start immediately and fade in. Loops stop outside the activation range plus exit hysteresis, fade out, and release their source. They restart from the beginning when reactivated. Emitters check at 4 Hz and retry failed or reclaimed loops at most once per second. Missing listeners stop loops. An optional **Ambience Zone** adds listener membership in a rotated/scaled box; **Outside Zone** excludes that box instead. Zone hysteresis is in world metres and requires no collider or trigger callbacks.

Quackville's 16 original direct looping AudioSources are now AmbientEmitters at the same locations, with individual `Ambience_Quackville_*` SoundEvents preserving clips, volume, pitch, linear rolloff, and maximum distance. Priority is 220 and output goes through Effects. These broad environment beds disable **Use Occlusion**, preserving their previous unoccluded mix without adding wall-raycast/filter work. An AmbienceZone on `AnaltyicsArea_Subway` uses that existing area's bounds to exclude outdoor ambience underground; tune the bounds and transitions by listening at subway entrances. No new subway sound placement was invented. The gameplay helicopter continues to use the SoundEmitter in `PhotonHelicopter.prefab`.

**Ambience Voice Limit** in GameAudioSettings defaults to 6 within the existing 24-source pool. It includes ambient one-shots and loading/fading leases, and clamps below pool capacity to leave at least one gameplay slot. Set it before Play mode; zero disables new ambience. Ambience never steals gameplay audio. At the ambience/event limit, a higher-priority or meaningfully nearer candidate fades out the least relevant eligible ambience voice before retrying; distance is normalized by each event's maximum range with a 0.15 replacement margin to prevent churn. Ordinary fades count against the cap, so transitions never allocate extra sources. When the whole pool is full, gameplay can immediately reclaim an ambience slot, including a looping/fading one, before considering other effects. Immediate reclamation prioritizes effect responsiveness over finishing the ambience fade.

## Footsteps and enemies

ZombieSpider footsteps are wired: they follow each client's procedural gait, so they require no per-step network messages. Clip/pitch randomness is local; identical selections across clients are not required.

FootstepEmitter supports `LeftFoot`, `RightFoot` and `Step` animation events, or `PlayAt(point, normal)` for procedural contacts. SpiderGait, BipedGait and GorillaGait expose FootPlanted events; add ProceduralFootstepAudio plus FootstepEmitter beside a gait to connect them. A small minimum interval suppresses clusters of feet landing together.

## Surfaces

Surfaces describe what they are made of; each sound source decides what that sounds like.

- **Surface Type** assets (`Assets/Audio/Surfaces`: Stone, Metal, Wood, Dirt...) are just names. Add new ones with **Create > DuckZ > Audio > Surface Type**.
- Put an **Audio Surface** component on a collider or a parent and assign its type. It holds no sounds. (The `Surface` components in Environment and GorillaLocomotion are unrelated locomotion slip settings.)
- A **Surface Sound Set** (`Assets/Audio/SurfaceSoundSets`) maps surface types to SoundEvents for one kind of source, plus a fallback for untagged surfaces or missing entries. Current sets: `Player_Footsteps`, `Player_HandHits` (VR hand slaps and body landings on NewPlayer), `ZombieSpider_Footsteps`, and `Weapon_Impacts` (not yet called by any weapon code).

An enemy with its own surface sounds just needs its own set; no surface or code changes are required.

Set FootstepEmitter's Ground Mask to actual environment layers, excluding character colliders. Surface lookup raycasts only on a step, along the contact normal; crawling enemies work on walls too.

For local players, select the root containing NewPlayer or DesktopPlayer and use **Tools > DuckZ > Authoring > Audio > Add Player Footsteps To Selection**. This adds the `Player_Footsteps` set and movement adapter; adjust the ground mask and step distance. It uses body locomotion distance, not headset bob. VR grounding is probed under the body because NewPlayer's generic grounded flag also includes hands/head. Teleports discard accumulated movement. Do not attach both this and an animation/procedural step driver to the same character. Remote-player footstep replication is not automatically provided by this local adapter. Existing player-prefab edits were preserved.

## Mixer, reverb and voice

`Assets/Audio/GameAudio.mixer` contains Effects and Voice groups. SoundEvents play through Effects and may override the mixer group. No spatializer plugin is used: `SoundEvent.Configure` and `PhotonVoiceChat` force `AudioSource.spatialize` off. Project Settings > Audio still names Meta XR Audio because the Meta audio package (pulled in by `com.meta.xr.sdk.all`) resets an empty setting on load; it is loaded but never processes a source. All game audio and voice use Unity's built-in 3D panning and distance attenuation, which is what lets Audio Reverb Zones apply; the Meta XR spatializer ignores Unity reverb zones. PhotonVoiceChat routes its Speaker AudioSources through Voice, keeping its proximity rolloff and also passing through reverb zones. The Voice group is set to +12 dB to supply the close-range boost the voice rolloff curve expects (`PhotonVoiceChat.CloseGainDb`); change both together. Photon sources are never pooled, stolen or pitch-randomized. Group levels can be tuned in the Audio Mixer; exposed settings-menu sliders are not added by this system.

Drag `Reverb_Subway` into a tunnel and adjust its native AudioReverbZone preset and inner/outer radii. Unity supplies distance blending; the space outside zones stays dry. This first version deliberately uses Unity's spherical zones, not box-shaped room simulation or wall occlusion. For hallways and rooms, use `Reverb_Hallway` (a **Box Reverb Zone**): set its Size and rotate it to fit the space. It moves its child AudioReverbZone to the point in the box nearest the listener, giving full reverb anywhere inside and a fade over Fade Distance outside. Pick the preset on the child **Zone**; its Min/Max Distance are driven by Fade Distance. Zones are driven by the listener's position (the camera), not the sound's: everything heard while standing inside a zone gets its reverb. Avoid excessive overlapping zones and audition on Quest. Voice bypasses these zones.

## Pool behavior and verification

For gameplay requests: enforce the event limit first, use a free slot, then reclaim ambience if the pool is full. If no ambience can be reclaimed, steal only a strictly lower-priority non-loop, preferring the least-important and then most-distant candidate. Otherwise skip. Equal-priority gameplay sounds finish. There is no runtime growth, per-sound coroutine, or per-frame allocation in the normal pool update. Clip loading and first-use history entries can allocate; preload short gameplay clips in import settings if first-use latency appears. Large ambience clips should be profiled before changing their import/load settings. One-shot lifetime uses pitch-adjusted DSP time and respects AudioListener.pause.

Select the runtime Audio Manager for active/capacity, ambience/limit, peak use, skipped requests and stolen sounds. Skip/steal counters are collected in the Editor and development builds. The pool does not budget audio memory or replace Unity's real/virtual voice settings. Compare a consistent aboveground/subway route on the target device using Unity's total playing sources, real/virtual voices, DSP CPU, streaming CPU and audio memory. The six-voice cap is a starting mix budget, not a measured CPU or memory saving.

Tests under `Assets/Tests/Audio` cover ambience caps, gameplay reclamation, protected gameplay loops, normal priority stealing, per-event replacement, stale handles, fades, ownership, distance/zone transitions, listener loss, and rotated/scaled zone boundaries. Run in Unity's Play Mode Test Runner. Multiplayer listening, reverb interaction, and CPU/voice/memory budgets still need a busy-scene Quest test.

**Tools > DuckZ > Authoring > Audio > Create Starter Assets** recreates missing starter content and wires the spider prefab; it preserves existing SoundEvent settings. Runtime code is in the Game.Audio assembly; editor preview/setup and test assemblies do not ship in the player.

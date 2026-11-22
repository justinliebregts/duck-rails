using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class VibrationManager : MonoBehaviour {
	public static VibrationManager instance;

	readonly Dictionary<AudioClip, OVRHapticsClip> clips = new Dictionary<AudioClip, OVRHapticsClip>();
    readonly List<PendingVibration> pending = new List<PendingVibration>(32);

    // Index 0 = left, 1 = right. SetControllerVibration runs until explicitly stopped.
    readonly OVRInput.Controller[] pulseControllers = new OVRInput.Controller[2];
    readonly float[] pulseEndTimes = { -1f, -1f };
    readonly float[] nextHeldItemHitTimes = { -1f, -1f };

    public void TriggerHeldItemHit(PlayerHand hand) {
        if (!CanHaptic || hand == null || !isActiveAndEnabled) return;
        if (!hand.IsControllerLTouch && !hand.IsControllerRTouch) return;

        int slot = hand.IsControllerLTouch ? 0 : 1;
        if (Time.time < nextHeldItemHitTimes[slot]) return;
        nextHeldItemHitTimes[slot] = Time.time + 0.1f;
        TriggerVibrationPulse(1f, 1f, 0.12f, hand);
    }

    // ---- Presets. Tune amplitudes here. ----
    readonly float[] nextWarningTimes = { -1f, -1f };

    static bool IsTouchHand(PlayerHand hand) =>
        hand != null && hand.isActiveAndEnabled && (hand.IsControllerLTouch || hand.IsControllerRTouch);

    /// <summary>Very light tick when an item is picked up or let go.</summary>
    public void TriggerGrabTick(PlayerHand hand, bool grabbed) {
        if (!CanHaptic || !IsTouchHand(hand)) return;
        TriggerVibrationPulse(.6f, grabbed ? .2f : .12f, .03f, hand);
    }

    /// <summary>Very subtle thump on both hands each time the rail-joint sound plays. speed01 is 0..1.</summary>
    public void TriggerTrackJoint(float speed01) {
        if (!CanHaptic || GR.Player.instance == null) return;
        float amplitude = Mathf.Lerp(.03f, .1f, Mathf.Clamp01(speed01));
        PulseHandIfIdle(GR.Player.instance.leftPlayerHand, .3f, amplitude, .035f);
        PulseHandIfIdle(GR.Player.instance.rightPlayerHand, .3f, amplitude, .035f);
    }

    void PulseHandIfIdle(PlayerHand hand, float frequency, float amplitude, float duration) {
        if (!IsTouchHand(hand)) return;
        // Ambient feedback never cuts off a warning or other pulse already running on this hand.
        if (pulseEndTimes[hand.IsControllerLTouch ? 0 : 1] >= 0f) return;
        TriggerVibrationPulse(frequency, amplitude, duration, hand);
    }

    /// <summary>Small click as the throttle passes a detent while held.</summary>
    public void TriggerThrottleClick(PlayerHand hand) {
        if (!CanHaptic || !IsTouchHand(hand)) return;
        TriggerVibrationPulse(.7f, .18f, .03f, hand);
    }

    /// <summary>Press of the train horn or radio talk button.</summary>
    public void TriggerButtonPress(PlayerHand hand) {
        if (!CanHaptic || !IsTouchHand(hand)) return;
        TriggerVibrationPulse(.5f, .4f, .06f, hand);
    }

    /// <summary>Call every frame while a hand is being pulled away. t01: 0 = no warning, 1 = about to let go.
    /// Pulses repeat faster and a bit stronger as t01 grows.</summary>
    public void TriggerDriftWarning(PlayerHand hand, float t01) {
        if (!CanHaptic || !IsTouchHand(hand) || t01 <= 0f) return;
        int slot = hand.IsControllerLTouch ? 0 : 1;
        if (Time.time < nextWarningTimes[slot]) return;
        t01 = Mathf.Clamp01(t01);
        nextWarningTimes[slot] = Time.time + Mathf.Lerp(.4f, .06f, t01);
        TriggerVibrationPulse(.5f, Mathf.Lerp(.1f, .5f, t01), .03f, hand);
    }

    /// <summary>Strong buzz on both hands (downed / revived).</summary>
    public void TriggerBothHands(float frequency, float amplitude, float duration) {
        if (!CanHaptic || GR.Player.instance == null) return;
        var left = GR.Player.instance.leftPlayerHand;
        var right = GR.Player.instance.rightPlayerHand;
        if (IsTouchHand(left)) TriggerVibrationPulse(frequency, amplitude, duration, left);
        if (IsTouchHand(right)) TriggerVibrationPulse(frequency, amplitude, duration, right);
    }

    public void TriggerDowned() => TriggerBothHands(.8f, 1f, .45f);
    public void TriggerRevived() => TriggerBothHands(.6f, .7f, .3f);

    struct PendingVibration {
        public float dueTime;
        public AudioClip audio;
        public PlayerHand hand;
        public float frequency, amplitude;
        public bool usesAudio, bothHands;
    }

    OVRHapticsClip GetClip(AudioClip audio) {
        if (!clips.TryGetValue(audio, out var clip)) {
            clip = new OVRHapticsClip(audio);
            clips.Add(audio, clip);
        }
        return clip;
    }

    void Update() {
        if (!CanHaptic) pending.Clear();

        // Preserve request order when multiple vibrations become due together.
        for (int i = 0; i < pending.Count;) {
            var request = pending[i];
            if (Time.time < request.dueTime) { i++; continue; }
            pending.RemoveAt(i);
            if (request.usesAudio) {
                if (request.bothHands) DoTriggerVibration(request.audio);
                else DoTriggerVibration(request.audio, request.hand);
            } else if (request.hand != null) {
                DoTriggerVibration(request.frequency, request.amplitude, request.hand);
            }
        }

        for (int i = 0; i < pulseEndTimes.Length; i++) {
            if (pulseEndTimes[i] < 0f || Time.time < pulseEndTimes[i]) continue;
            OVRInput.SetControllerVibration(0f, 0f, pulseControllers[i]);
            pulseEndTimes[i] = -1f;
        }
    }

    public void TriggerVibrationPulse(float frequency, float amplitude, float duration, PlayerHand pHand) {
        if (!CanHaptic || pHand == null) return;

        VibrateController(frequency, amplitude, pHand);
        int slot = pHand.IsControllerLTouch ? 0 : 1;
        pulseControllers[slot] = pHand.controller;
        pulseEndTimes[slot] = Time.time + duration;
    }

    void OnDisable() {
        pending.Clear();
    }

    void OnDestroy() {
        if (instance == this) instance = null;
        clips.Clear();
    }

	// ---- Player setting (Settings > Controls > Haptics). Every vibration goes through CanHaptic. ----
	public const string HapticsPrefsKey = "HapticsEnabled";
	public const bool DefaultHapticsEnabled = true;
	static bool hapticsEnabled = DefaultHapticsEnabled;

	public static bool HapticsEnabled => hapticsEnabled;

	// Restore the saved choice as soon as account prefs exist, before any menu opens.
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void BootHapticsSetting() {
		hapticsEnabled = DefaultHapticsEnabled;
		UserPlayerPrefs.UserReady -= LoadSavedHapticsSetting;
		UserPlayerPrefs.UserReady += LoadSavedHapticsSetting;
		if (UserPlayerPrefs.IsReady)
			LoadSavedHapticsSetting();
	}

	public static void LoadSavedHapticsSetting() {
		ApplyHapticsEnabled(UserPlayerPrefs.GetInt(HapticsPrefsKey, DefaultHapticsEnabled ? 1 : 0) != 0);
	}

	/// <summary>Saves the player's choice. Turning haptics off also stops anything running now.</summary>
	public static void SetHapticsEnabled(bool enabled) {
		UserPlayerPrefs.SetInt(HapticsPrefsKey, enabled ? 1 : 0);
		UserPlayerPrefs.Save();
		ApplyHapticsEnabled(enabled);
	}

	static void ApplyHapticsEnabled(bool enabled) {
		hapticsEnabled = enabled;
		if (!enabled && instance != null)
			instance.StopAllVibration();
	}

	/// <summary>Drops queued requests and silences both controllers.</summary>
	public void StopAllVibration() {
		pending.Clear();
		for (int i = 0; i < pulseEndTimes.Length; i++)
			pulseEndTimes[i] = -1f;
		if (!Application.isPlaying)
			return;
		OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
		OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
		ClearAllVibration();
	}

	public bool CanHaptic => hapticsEnabled;

	void Start() {
		if (instance && instance != this) {
			Destroy(this);
		} else {
			instance = this;
		}
	}

	public void TriggerVibrationAll(AudioClip vibrationAudio, float delay = 0f) {
		if (!CanHaptic) return;

		if(vibrationAudio == null) {
			{
			    if (DuckzLog.AllowError(out var logNotice1))
			        logNotice1.Write("VibrationAudio clip missing for this haptics");
			}
			return;
		}

		if (delay == 0f) {
			DoTriggerVibration(vibrationAudio);
		} else {
			pending.Add(new PendingVibration { dueTime = Time.time + delay, audio = vibrationAudio, usesAudio = true, bothHands = true });
		}
	}

	public void TriggerVibration(AudioClip vibrationAudio, PlayerHand pHand, float delay = 0f) {
		if (!CanHaptic) return;

		if (vibrationAudio == null) {
			{
			    if (DuckzLog.AllowError(out var logNotice2))
			        logNotice2.Write("VibrationAudio clip missing for this haptics");
			}
			return;
		}

		if (delay == 0f) {
			DoTriggerVibration(vibrationAudio, pHand);
		} else {
			pending.Add(new PendingVibration { dueTime = Time.time + delay, audio = vibrationAudio, hand = pHand, usesAudio = true });
		}
	}
	


	void DoTriggerVibration(AudioClip vibrationAudio, PlayerHand pHand) {

		if (!CanHaptic) {
			return;
		}

		if (vibrationAudio == null) {
			return;
		}

		if (pHand != null && vibrationAudio != null) {
			SoundController(pHand, vibrationAudio);
		}
	}

	void DoTriggerVibration(AudioClip vibrationAudio) {
		if (!CanHaptic) {
			return;
		}

		if (vibrationAudio == null) {
			return;
		}
		
		SoundController(vibrationAudio);
	}
	
	public void TriggerVibration(float frequency, float amplitude, PlayerHand pHand, float delay = 0f) {
		if (!CanHaptic) return;

		if (delay == 0f) {
			DoTriggerVibration(frequency, amplitude, pHand);
		} else {
			pending.Add(new PendingVibration { dueTime = Time.time + delay, frequency = frequency, amplitude = amplitude, hand = pHand });
		}
	}


	void DoTriggerVibration(float frequency, float amplitude, PlayerHand pHand) {

		if(!CanHaptic || pHand == null) return;

		VibrateController(frequency, amplitude, pHand);  //OVRInput.SetControllerVibration(frequency, amplitude, controller);
	}

	void VibrateVive(bool left) {
		InputDevice device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
		HapticCapabilities capabilities;
		if (device.TryGetHapticCapabilities(out capabilities)) {
			if (capabilities.supportsImpulse) {
				device.SendHapticImpulse(0, 0.5f, 0.05f);
			}
		}
	}

	void OnApplicationFocus(bool hasFocus) {
		ClearAllVibration();
	}

	void OnApplicationPause(bool isPaused) {
		ClearAllVibration();
	}


	private void SoundController(AudioClip vibrationAudio)
	{
#if UNITY_EDITOR
		// if(GameManager.instance.controllerType == GameManager.Controller.Mobile) return;
#endif

		if (vibrationAudio == null) {
			return;
		}

		try {
			var clip = GetClip(vibrationAudio);
			OVRHaptics.LeftChannel.Preempt(clip);
			OVRHaptics.RightChannel.Preempt(clip);
		} catch (System.Exception e) {
			{
			    if (DuckzLog.AllowError(out var logNotice3, e.GetType().FullName))
			        logNotice3.Write(e.Message);
			}
		}
	}

	private void SoundController(PlayerHand pHand, AudioClip vibrationAudio)
	{
#if UNITY_EDITOR
		// if(GameManager.instance.controllerType == GameManager.Controller.Mobile) return;
#endif
		if (vibrationAudio == null) {
			return;
		}

		try {

			var clip = GetClip(vibrationAudio);

			if (pHand.IsControllerLTouch) {
				OVRHaptics.LeftChannel.Preempt(clip);
			} else if (pHand.IsControllerRTouch) {
				OVRHaptics.RightChannel.Preempt(clip);
			}

		} catch (System.Exception e) {
			{
			    if (DuckzLog.AllowError(out var logNotice4, e.GetType().FullName))
			        logNotice4.Write(e.Message);
			}
		}
		
	}

	private void VibrateController(float frequency, float amplitude, PlayerHand pHand)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, pHand.controller);
    }

	private void ClearAllVibration() {

		try {

			OVRHaptics.LeftChannel.Clear();
			OVRHaptics.RightChannel.Clear();

		} catch (System.Exception e) {
			{
			    if (DuckzLog.AllowError(out var logNotice5, e.GetType().FullName))
			        logNotice5.Write(e.Message);
			}
		}
		
	}
}

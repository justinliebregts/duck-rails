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

	// bool isOnStandby = false;

	// Start is called before the first frame update
	public bool CanHaptic => true;//GameManager.instance.controllerVibration && !GameManager.instance.OnStandby();

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

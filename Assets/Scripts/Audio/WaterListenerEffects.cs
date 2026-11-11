using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Local-player feedback when the listener (the head camera) dips below a WaterVolume surface: splash in/out,
    /// muffled audio (low-pass on the listener), tinted exponential fog, and occasional bubbles. Created on demand by
    /// the first enabled WaterVolume. Everything is restored when the head surfaces or this object is disabled.
    /// </summary>
    public sealed class WaterListenerEffects : MonoBehaviour
    {
        const float BlendSeconds = 0.15f;
        const float SurfaceHysteresis = 0.03f;
        const float ClearCutoffHz = 22000f;
        static WaterListenerEffects instance;

        public static bool HeadSubmerged => instance != null && instance.volume != null;

        WaterVolume volume;       // volume the head is currently under, null when dry
        WaterVolume fadingVolume; // keeps settings during the fade-out
        float blend;
        float bubbleTimer;
        Transform listenerTransform;
        AudioLowPassFilter lowPass;
        bool lowPassWasAdded;
        bool fogSaved;
        bool fogWasOn;
        FogMode fogMode;
        Color fogColor;
        float fogDensity;

        public static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("Water Listener Effects");
            instance = go.AddComponent<WaterListenerEffects>();
        }

        void OnDisable()
        {
            Restore();
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            Transform head = AudioManager.instance != null ? AudioManager.instance.ListenerTransform : null;
            if (head != listenerTransform)
            {
                Restore();
                listenerTransform = head;
            }
            if (head == null) { Restore(); return; }

            UpdateSubmersion(head.position);

            float dt = Time.unscaledDeltaTime;
            float target = volume != null ? 1f : 0f;
            blend = Mathf.MoveTowards(blend, target, dt / BlendSeconds);
            if (blend > 0f) Apply(volume != null ? volume : fadingVolume, head);
            else if (fogSaved || lowPass != null) Restore();

            if (volume != null && volume.bubblesSound != null && AudioManager.instance != null)
            {
                bubbleTimer += dt;
                if (bubbleTimer >= volume.bubbleInterval)
                {
                    bubbleTimer = 0f;
                    AudioManager.instance.Play(volume.bubblesSound, head.position);
                }
            }
        }

        void UpdateSubmersion(Vector3 headPosition)
        {
            if (volume != null)
            {
                // Stay submerged until clearly above the surface, so ripples/bobbing don't flutter the effect.
                if (volume.isActiveAndEnabled && volume.Contains(headPosition, SurfaceHysteresis)) return;
                var leaving = volume;
                volume = null;
                fadingVolume = leaving;
                if (leaving.exitSound != null && AudioManager.instance != null)
                    AudioManager.instance.Play(leaving.exitSound, headPosition);
                return;
            }
            var found = WaterVolume.Find(headPosition);
            if (found == null) return;
            volume = found;
            fadingVolume = found;
            bubbleTimer = 0f;
            if (found.enterSound != null && AudioManager.instance != null)
                AudioManager.instance.Play(found.enterSound, headPosition);
        }

        void Apply(WaterVolume v, Transform head)
        {
            if (v == null) return;
            if (lowPass == null)
            {
                lowPass = head.GetComponent<AudioLowPassFilter>();
                lowPassWasAdded = lowPass == null;
                if (lowPass == null) lowPass = head.gameObject.AddComponent<AudioLowPassFilter>();
            }
            lowPass.enabled = true;
            lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(ClearCutoffHz), Mathf.Log(v.underwaterCutoffHz), blend));

            if (!fogSaved)
            {
                fogSaved = true;
                fogWasOn = RenderSettings.fog;
                fogMode = RenderSettings.fogMode;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(fogWasOn ? fogColor : v.fogColor, v.fogColor, blend);
            float from = fogWasOn && fogMode == FogMode.Exponential ? fogDensity : 0f;
            RenderSettings.fogDensity = Mathf.Lerp(from, v.fogDensity, blend);
        }

        void Restore()
        {
            if (lowPass != null)
            {
                if (lowPassWasAdded) Destroy(lowPass);
                else lowPass.enabled = false;
            }
            lowPass = null;
            lowPassWasAdded = false;
            if (fogSaved)
            {
                RenderSettings.fog = fogWasOn;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                fogSaved = false;
            }
            blend = 0f;
            volume = null;
            fadingVolume = null;
        }
    }
}

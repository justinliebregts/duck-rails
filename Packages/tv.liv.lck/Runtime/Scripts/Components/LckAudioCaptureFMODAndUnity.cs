#if LCK_FMOD_2_03
#define LCK_FMOD
#endif
using System;
using System.Runtime.InteropServices;
using Liv.Lck.Collections;
using Liv.Lck.Utilities;
using UnityEngine;

namespace Liv.Lck
{
    internal class LckAudioCaptureFMODAndUnity : MonoBehaviour, ILckAudioSource
    {

#if LCK_FMOD
        private FMOD.DSP_READ_CALLBACK _mReadCallback;
        private FMOD.DSP _mCaptureDSP;
#endif
        private GCHandle _mObjHandle;
        private readonly AudioBuffer _tmpRemixBuffer = new AudioBuffer(98000);
        private readonly AudioBuffer _tmpAudio = new AudioBuffer(98000);
        // Source buffers
        private readonly AudioBuffer _fmodBuffer = new AudioBuffer(98000);
        private readonly AudioBuffer _unityBuffer = new AudioBuffer(98000);
        // Mixed output buffer
        private readonly AudioBuffer _mixBuffer = new AudioBuffer(98000);
        
        private int _fmodSampleRate = 0;
        private int _unitySampleRate = 0;

        private bool _isCapturing = false;

        private readonly System.Object _audioThreadLock = new System.Object();

        public bool IsCapturing()
        {
            return _isCapturing;
        }

        private static void TryAppendToBuffer(float[] srcDataBuffer, int srcStartIdx, int srcDataLength, AudioBuffer destBuffer)
        {
            if (!destBuffer.TryExtendFrom(srcDataBuffer, srcStartIdx, srcDataLength))
            {
                LckLog.LogWarning("LCK Audio Capture (FMOD + Unity) losing data. Expecting this to be a lag spike.");
            }
        }

        private static void TryAppendToBuffer(AudioBuffer srcBuffer, AudioBuffer destBuffer)
        {
            if (!destBuffer.TryExtendFrom(srcBuffer))
            {
                LckLog.LogWarning("LCK Audio Capture (FMOD + Unity) losing data. Expecting this to be a lag spike.");
            }
        }
        
        private static void AppendToBufferAsStereo(
            float[] sourceAudioBuffer, int sourceAudioStartIdx, int sourceAudioLength, int sourceChannels, AudioBuffer destBuffer, AudioBuffer remixBuffer)
        {
            switch (sourceChannels)
            {
                case ChannelMixingUtils.StereoChannelCount:
                    LckLog.LogWarning("LCK Audio Capture (FMOD + Unity): Got stereo input. No remixing necessary.");
                    TryAppendToBuffer(sourceAudioBuffer, sourceAudioStartIdx, sourceAudioLength, destBuffer);
                    break;
                case ChannelMixingUtils.MonoChannelCount:
                    LckLog.Log("LCK Audio Capture (FMOD + Unity): Got mono input. Remixing to stereo.");
                    ChannelMixingUtils.ConvertMonoToStereo(sourceAudioBuffer, sourceAudioStartIdx, sourceAudioLength, remixBuffer);
                    TryAppendToBuffer(remixBuffer, destBuffer);
                    break;
                case ChannelMixingUtils.FiveOneChannelCount:
                    LckLog.Log("LCK Audio Capture (FMOD + Unity): Got 5.1 input. Remixing to stereo.");
                    ChannelMixingUtils.ConvertFiveOneToStereo(sourceAudioBuffer, sourceAudioStartIdx, sourceAudioLength, remixBuffer);
                    TryAppendToBuffer(remixBuffer, destBuffer);
                    break;
                default:
                {
                    // Unsupported channel configuration
                    LckLog.LogError(
                        "LCK Audio Capture (FMOD + Unity): LCK only supports Mono, Stereo or 5.1 input at this time. " +
                        $"Got: {sourceChannels} channels");
                    break;
                }
            }
        }
        
        protected virtual void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isCapturing)
            {
                return;
            }

            lock (_audioThreadLock)
            {
                AppendToBufferAsStereo(data, 0, data.Length, channels, _unityBuffer, _tmpRemixBuffer);
            }
        }


#if LCK_FMOD
        [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
        static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint numFrames, int numChannels, ref int outchannels)
        {

#if LCK_FMOD_2_03
            FMOD.DSP_STATE_FUNCTIONS functions = dsp_state.functions;
#else
            FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));
#endif

            functions.getuserdata(ref dsp_state, out var userData);

            GCHandle objHandle = GCHandle.FromIntPtr(userData);
            LckAudioCaptureFMODAndUnity lckCapture = objHandle.Target as LckAudioCaptureFMODAndUnity;

            var numSamples = (int)Math.Min(numFrames * numChannels, lckCapture._tmpAudio.Capacity);

            lckCapture._tmpAudio.TryCopyFrom(inbuffer, numSamples);
            Marshal.Copy(lckCapture._tmpAudio.Buffer, 0, outbuffer, numSamples);

            if (lckCapture._isCapturing)
            {
                lock (lckCapture._audioThreadLock)
                {
                    AppendToBufferAsStereo(
                        lckCapture._tmpAudio.Buffer,
                        0,
                        numSamples,
                        numChannels, 
                        lckCapture._fmodBuffer, 
                        lckCapture._tmpRemixBuffer);
                }
            }

            return FMOD.RESULT.OK;
        }
#endif

        void Start()
        {
#if LCK_FMOD
            // Assign the callback to a member variable to avoid garbage collection
            _mReadCallback = CaptureDSPReadCallback;

            uint bufferLength;
            int numBuffers;
            FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out bufferLength, out numBuffers);

            // Get a handle to this object to pass into the callback
            _mObjHandle = GCHandle.Alloc(this);
            if (_mObjHandle != null)
            {
                // Define a basic DSP that receives a callback each mix to capture audio
                FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
                desc.numinputbuffers = 1;
                desc.numoutputbuffers = 1;
                desc.read = _mReadCallback;
                desc.userdata = GCHandle.ToIntPtr(_mObjHandle);

                // Create an instance of the capture DSP and attach it to the master channel group to capture all audio
                FMOD.ChannelGroup masterCG;
                if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) == FMOD.RESULT.OK)
                {
                    if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out _mCaptureDSP) == FMOD.RESULT.OK)
                    {
                        if (masterCG.addDSP(0, _mCaptureDSP) != FMOD.RESULT.OK)
                        {
                            LckLog.LogWarning("LCK FMOD: Unable to add mCaptureDSP to the master channel group");
                        }
                    }
                    else
                    {
                        LckLog.LogWarning("LCK FMOD: Unable to create a DSP: mCaptureDSP");
                    }
                }
                else
                {
                    LckLog.LogWarning("LCK FMOD: Unable to create a master channel group: masterCG");
                }
            }
            else
            {
                LckLog.LogWarning("LCK FMOD: Unable to create a GCHandle: mObjHandle");
            }
            
            FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out _fmodSampleRate, out _, out _);
#endif
            _unitySampleRate = AudioSettings.outputSampleRate;
            if (_unitySampleRate != _fmodSampleRate)
            {
                // TODO: Implement re-sampling to support configurations where audio engines use different sample rates
                LckLog.LogError($"LCK Audio Capture (FMOD + Unity): Unity sample rate ({_unitySampleRate}) and FMOD " + 
                                $"sample rate ({_fmodSampleRate}) do not match - this is not currently supported, so " +
                                "audio pitch may be incorrect in captures");
            }
        }

        void OnDestroy()
        {
#if LCK_FMOD
            if (_mObjHandle.IsAllocated)
            {
                // Remove the capture DSP from the master channel group
                FMOD.ChannelGroup masterCG;
                if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) == FMOD.RESULT.OK)
                {
                    if (_mCaptureDSP.hasHandle())
                    {
                        masterCG.removeDSP(_mCaptureDSP);

                        // Release the DSP and free the object handle
                        _mCaptureDSP.release();
                    }
                }
                _mObjHandle.Free();
            }
#endif
        }

        public void GetAudioData(ILckAudioSource.AudioDataCallbackDelegate callback)
        {
            lock (_audioThreadLock)
            {
                // Mix FMOD and Unity audio together
                _mixBuffer.Clear();

                var fmodCount = _fmodBuffer.Count;
                var unityCount = _unityBuffer.Count;

                // Only mix the number of samples that both buffers have available.
                // Leave any extra samples in either buffer for the next pass.
                var samplesToMix = Math.Min(fmodCount, unityCount);

                if (samplesToMix > 0)
                {
                    for (var i = 0; i < samplesToMix; i++)
                    {
                        var mixed = _fmodBuffer[i] + _unityBuffer[i];
                        _mixBuffer.TryAdd(mixed);
                    }
                }

                // Provide whatever we mixed this pass (possibly 0 samples)
                callback(_mixBuffer);

                // Consume only what we mixed, keeping leftovers in the source buffers for future passes
                if (samplesToMix > 0)
                {
                    _fmodBuffer.SkipAudioSamples(samplesToMix);
                    _unityBuffer.SkipAudioSamples(samplesToMix);
                }

                _mixBuffer.Clear();
            }
        }

        public void EnableCapture()
        {
            _isCapturing = true;
            _fmodBuffer.Clear();
            _unityBuffer.Clear();
            _mixBuffer.Clear();
        }

        public void DisableCapture()
        {
            _isCapturing = false;
            _fmodBuffer.Clear();
            _unityBuffer.Clear();
            _mixBuffer.Clear();
        }
    }
}

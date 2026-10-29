using Liv.Lck.Collections;

namespace Liv.Lck.Utilities
{
    public static class ChannelMixingUtils
    {
        public const int MonoChannelCount = 1;
        public const int StereoChannelCount = 2;
        public const int FiveOneChannelCount = 6;
        
        public static void ConvertMonoToStereo(
            float[] sourceMonoAudio, int sourceAudioStartIdx, int sourceAudioLength, AudioBuffer outputBuffer)
        {
            // Upmix mono input to stereo by duplicating samples
            outputBuffer.Clear();
            for (var i = 0; i < sourceAudioLength; i++)
            {
                var sample = sourceMonoAudio[sourceAudioStartIdx + i];
                for (var channel = 0; channel < StereoChannelCount; channel++)
                {
                    outputBuffer.TryAdd(sample);
                }
            }
        }

        public static void ConvertFiveOneToStereo(
            float[] sourceFiveOneAudio, int sourceAudioStartIdx, int sourceAudioLength, AudioBuffer outputBuffer)
        {
            // Downmix 5.1 input (FL, FR, C, LFE, SL, SR) to stereo
            var numFrames = sourceAudioLength / FiveOneChannelCount;
            outputBuffer.Clear();
            for (int i = 0; i < numFrames; i++)
            {
                int baseIndex = sourceAudioStartIdx + i * FiveOneChannelCount;
                float frontLeft = sourceFiveOneAudio[baseIndex + 0];
                float frontRight = sourceFiveOneAudio[baseIndex + 1];
                float center = sourceFiveOneAudio[baseIndex + 2];
                // LFE (index 3) ignored
                float backLeft = sourceFiveOneAudio[baseIndex + 4];
                float backRight = sourceFiveOneAudio[baseIndex + 5];

                float left = 0.707f * frontLeft + 0.5f * center + 0.354f * backLeft;
                float right = 0.707f * frontRight + 0.5f * center + 0.354f * backRight;

                outputBuffer.TryAdd(left);
                outputBuffer.TryAdd(right);
            }
        }
    }
}


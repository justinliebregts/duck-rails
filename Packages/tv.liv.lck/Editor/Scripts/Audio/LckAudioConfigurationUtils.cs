using Liv.Lck.Audio.FMOD;
using Liv.Lck.Audio.Wwise;

namespace Liv.Lck.Audio
{
    internal static class LckAudioConfigurationUtils
    {
        public static readonly LckFMODConfigurer FMODConfigurer = new LckFMODConfigurer();
        
        public static readonly LckWwiseConfigurer WwiseConfigurer = new LckWwiseConfigurer();
    }
}

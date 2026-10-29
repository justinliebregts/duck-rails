using System.Linq;
using Liv.Lck.Util;

namespace Liv.Lck.Audio.FMOD
{
    internal class LckFMODOptions : LckAudioSystemOptions
    {
        public static LckAudioSystemType AudioSystemType => LckAudioSystemType.FMOD;
        
        public LckFMODCompatabilityType Compatability { get; set; }
        public bool CombineWithUnityAudio { get; set; }

        protected override string[] GetToStringDisplayFields()
        {
            return base.GetToStringDisplayFields().Concat(new string[]
            {
                $"{nameof(Compatability)}={Compatability}",
                $"{nameof(CombineWithUnityAudio)}={CombineWithUnityAudio}"
            }).ToArray();
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Liv.Lck.Audio.FMOD;
using Liv.Lck.Audio.Wwise;

namespace Liv.Lck.Audio
{
    internal static class LckAudioScriptingDefines
    {
        public static IEnumerable<string> GetAllLckAudioScriptingDefines()
        {
            return LckUnityAudioScriptingDefines.All
                .Concat(LckFMODScriptingDefines.All)
                .Concat(LckWwiseScriptingDefines.All);
        }
    }
}

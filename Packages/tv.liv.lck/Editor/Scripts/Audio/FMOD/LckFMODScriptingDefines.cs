namespace Liv.Lck.Audio.FMOD
{
    internal static class LckFMODScriptingDefines
    {
        public const string UseFMOD = "LCK_FMOD";
        public const string UseFMODWithLatestCompatability = "LCK_FMOD_2_03";
        public const string UseFMODCombinedWithUnity = "LCK_FMOD_WITH_UNITY_AUDIO";

        public static readonly string[] All =
        {
            UseFMOD, 
            UseFMODWithLatestCompatability, 
            UseFMODCombinedWithUnity
        };
    }
}

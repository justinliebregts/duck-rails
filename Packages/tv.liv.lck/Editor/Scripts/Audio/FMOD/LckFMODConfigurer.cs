using Liv.Lck.Util;

namespace Liv.Lck.Audio.FMOD
{
    internal class LckFMODConfigurer : LckAudioSystemConfigurer<LckFMODOptions>
    {
        protected override LckAudioSystemType AudioSystemType => LckFMODOptions.AudioSystemType;

        public override LckFMODOptions GetCurrentOptions()
        {
            return new LckFMODOptions
            {
                ShouldCapture = ScriptingDefineUtils.HasDefine(LckFMODScriptingDefines.UseFMOD),
                Compatability = ScriptingDefineUtils.HasDefine(LckFMODScriptingDefines.UseFMODWithLatestCompatability)
                    ? LckFMODCompatabilityType.Latest
                    : LckFMODCompatabilityType.Pre2_03,
                CombineWithUnityAudio = ScriptingDefineUtils.HasDefine(LckFMODScriptingDefines.UseFMODCombinedWithUnity)
            };
        }

        protected override void ConfigureScriptingDefines(LckFMODOptions options)
        {
            // Start with a clean slate - remove all FMOD defines
            ScriptingDefineUtils.RemoveDefines(LckFMODScriptingDefines.All);
            
            if (!options.ShouldCapture)
            {
                // Only add FMOD scripting defines if LCK should capture FMOD audio
                return;
            }

            // Add FMOD scripting define so that FMOD-specific LCK code is compiled
            ScriptingDefineUtils.AddDefine(LckFMODScriptingDefines.UseFMOD);

            // If FMOD version is >= 2.03, add scripting define so that LCK uses compatible APIs
            if (options.Compatability == LckFMODCompatabilityType.Latest)
            {
                ScriptingDefineUtils.AddDefine(LckFMODScriptingDefines.UseFMODWithLatestCompatability);
            }

            // If FMOD audio should be combined with Unity audio (as opposed to exclusively capturing FMOD audio), add
            // scripting define
            if (options.CombineWithUnityAudio)
            {
                ScriptingDefineUtils.AddDefine(LckFMODScriptingDefines.UseFMODCombinedWithUnity);
            }
        }
    }
}

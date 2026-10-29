using Liv.Lck.Util;

namespace Liv.Lck.Audio.Wwise
{
    internal class LckWwiseConfigurer : LckAudioSystemConfigurer<LckWwiseOptions>
    {
        protected override LckAudioSystemType AudioSystemType => LckWwiseOptions.AudioSystemType;

        public override LckWwiseOptions GetCurrentOptions()
        {
            return new LckWwiseOptions
            {
                ShouldCapture = ScriptingDefineUtils.HasDefine(LckWwiseScriptingDefines.UseWwise)
            };
        }

        protected override void ConfigureScriptingDefines(LckWwiseOptions options)
        {
            // Start with a clean slate - remove all Wwise defines
            ScriptingDefineUtils.RemoveDefines(LckWwiseScriptingDefines.All);
            
            if (!options.ShouldCapture)
            {
                // Only add scripting defines if LCK should capture Wwise audio
                return;
            }

            // Add Wwise scripting define so that Wwise-specific LCK code is compiled
            ScriptingDefineUtils.AddDefine(LckWwiseScriptingDefines.UseWwise);
        }
    }
}

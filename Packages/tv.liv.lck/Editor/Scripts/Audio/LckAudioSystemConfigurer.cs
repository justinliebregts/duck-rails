using UnityEditor.Compilation;
using UnityEngine;

namespace Liv.Lck.Audio
{
    internal abstract class LckAudioSystemConfigurer<TOptions> where TOptions : LckAudioSystemOptions
    {
        /// <summary>
        /// Whether to show a dialog to display audio configuration validation results whenever a validation occurs.
        /// </summary>
        public bool ValidationDialogEnabled { get; set; } = true;
        
        protected abstract LckAudioSystemType AudioSystemType { get; }
        
        public void Configure(TOptions options, bool shouldValidate = true)
        {
            Debug.Log($"Configuring {AudioSystemType} with options: {options}");
            ConfigureScriptingDefines(options);
            
            // Re-compile so that changes take effect
            CompilationPipeline.RequestScriptCompilation();
            
            if (shouldValidate)
                Validate();
        }

        public abstract TOptions GetCurrentOptions();
        
        protected abstract void ConfigureScriptingDefines(TOptions options);

        private void Validate()
        {
            if (ValidationDialogEnabled)
            {
                LckAudioConfigurationValidator.ValidateAndShowDialog(
                    $"LCK {AudioSystemType} Audio Configuration",
                    $"{AudioSystemType} audio configuration completed.\n");
            }
            else
            {
                LckAudioConfigurationValidator.ValidateAndLog();
            }
        }
    }
}

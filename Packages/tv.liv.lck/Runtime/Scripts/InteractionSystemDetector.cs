using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Liv.Lck
{
    /// <summary>
    /// Utility to detect which XR interaction system(s) are available in the project at runtime.
    /// </summary>
    /// <remarks>
    /// Uses reflection so it has no hard dependency on any particular interaction system.
    /// </remarks>
    internal class InteractionSystemDetector
    {
        internal enum InteractionSystem
        {
            XRInteractionToolkit,
            OculusInteraction
        }
    
        private static bool _scanned;
        
        private static readonly List<InteractionSystem> _detectedSystems = new List<InteractionSystem>();
    
        private static readonly string[] _xrInteractionToolkitTypeNames = 
        {
            "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager",
            "UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor",
            "UnityEngine.XR.Interaction.Toolkit.XRDirectInteractor"
        };
    
        private static readonly string[] _oculusInteractionTypeNames =
        {
            "Oculus.Interaction.Interactor",
            "Oculus.Interaction.HandGrab.HandGrabInteractable",
            "Oculus.Interaction.Interactable",
            "Oculus.Interaction.Input.Hand"
        };
    
        /// <summary>
        /// Get all the available known XR interaction systems in the project.
        /// </summary>
        /// <returns>
        /// A collection of the available known XR interaction systems detected in the project, or an empty collection
        /// if none were found.
        /// </returns>
        /// <seealso cref="InteractionSystem"/>
        public static IReadOnlyCollection<InteractionSystem> GetAvailableInteractionSystems()
        {
            EnsureScanned();
            return _detectedSystems;
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureScanned()
        {
            if (_scanned)
                return;
            
            _scanned = true;
    
            // Detect Unity XR Interaction Toolkit
            if (AnyTypeExists(_xrInteractionToolkitTypeNames))
            {
                _detectedSystems.Add(InteractionSystem.XRInteractionToolkit);
            }
    
            // Detect Oculus Interaction
            if (AnyTypeExists(_oculusInteractionTypeNames))
            {
                _detectedSystems.Add(InteractionSystem.OculusInteraction);
            }
        }
    
        private static bool AnyTypeExists(string[] typeNames)
        {
            return typeNames.Any(TypeExists);
        }
    
        private static bool TypeExists(string fullTypeName)
        {
            // See if type can already be determined, or search all loaded assemblies in the current domain for the type
            return Type.GetType(fullTypeName, throwOnError: false) != null ||
                   AppDomain.CurrentDomain.GetAssemblies().Any(assembly => TypeExistsInAssembly(fullTypeName, assembly));
        }
    
        private static bool TypeExistsInAssembly(string fullTypeName, Assembly assembly)
        {
            try
            {
                if (assembly.GetType(fullTypeName, throwOnError: false) != null) 
                    return true;
            }
            catch (Exception ex)
            {
                LckLog.LogTrace($"Ignoring exception thrown by {nameof(Assembly.GetType)} (for {assembly.FullName}): {ex}");
                // ignore and continue
            }
    
            return false;
        }
    }
}

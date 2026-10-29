using UnityEngine;

/// <summary>
/// When using a third party audio engine, use this class to 'mark' the custom audio
/// capture component's GameObject. The audio mixer will then either add the relevant pre-supported AudioCapture
/// component along-side this marker, or find the custom <see cref="ILckAudioSource"/> implenenation
/// you have added.
/// 
/// <see href="https://lck-docs.liv.tv/integration/audio"/> for details.
/// </summary>
public class LckAudioMarker : MonoBehaviour {}

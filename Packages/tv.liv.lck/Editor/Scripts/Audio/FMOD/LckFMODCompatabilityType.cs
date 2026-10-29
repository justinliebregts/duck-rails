// ReSharper disable InconsistentNaming (allow underscores in enum names to represent FMOD version numbers)

namespace Liv.Lck.Audio.FMOD
{
    public enum LckFMODCompatabilityType
    {
        /// <summary>
        /// Support for FMOD API in versions prior to 2.03
        /// </summary>
        /// <remarks>
        /// For FMOD 2.03 and above, use <see cref="Latest"/> instead.
        /// </remarks>
        Pre2_03,
        
        /// <summary>
        /// Support for FMOD API in releases 2.03 and later
        /// </summary>
        /// <remarks>
        /// For FMOD versions prior to 2.03, use <see cref="Pre2_03"/> instead.
        /// </remarks>
        Latest,
    }
}

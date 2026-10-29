using UnityEngine;

namespace Liv.Lck
{
    public interface ILckMonitor
    {
        /// <summary>
        /// Get the unique Monitor ID, set at a GUID if one is not provided.
        /// </summary>
        string MonitorId { get; }
        /// <summary>
        /// Called internally. To set a monitor as active, see <see cref="LckService.SetActiveCamera"/>.
        /// </summary>
        /// <param name="renderTexture"></param>
        void SetRenderTexture(RenderTexture renderTexture);
    }
}

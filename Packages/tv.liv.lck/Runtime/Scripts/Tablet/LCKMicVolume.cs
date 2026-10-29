using Liv.Lck.DependencyInjection;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Provides a visual representation of the mic output. 
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class LCKMicVolume : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;
        
        [SerializeField]
        private float _incomingVolume = 0;

        [SerializeField]
        private UnityEngine.UI.Image _micVolumeImage;

        private void Awake()
        {
            if (_micVolumeImage)
            {
                _micVolumeImage.transform.SetSiblingIndex(0);
            }
        }
        
        void Update()
        {
            if(_lckService == null)
            {
                return;
            }

            _incomingVolume = Mathf.Clamp01(_lckService.GetMicrophoneOutputLevel().Result * 10f);

            if (_micVolumeImage)
            {
                _micVolumeImage.fillAmount = _incomingVolume;
            }
        }
    }
}

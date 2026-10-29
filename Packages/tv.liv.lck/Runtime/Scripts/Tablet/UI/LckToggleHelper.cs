using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liv.Lck.UI
{
    public class LckToggleHelper : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField]
        private LckToggle _lckToggle;
        [SerializeField]
        private Toggle _toggle;

        #region Using ray and poke
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_lckToggle.IsDisabled) return;

            _lckToggle.OnPointerEnter(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_lckToggle.IsDisabled) return;

            _lckToggle.OnPointerDown(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_lckToggle.IsDisabled) return;

            _lckToggle.OnPointerUp(eventData);
            _toggle.isOn = true;
            _lckToggle.SetToggleVisualsOn();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_lckToggle.IsDisabled) return;

            _lckToggle.OnPointerExit(eventData);
        }
        #endregion
    }
}

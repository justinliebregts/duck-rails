using UnityEngine;

namespace Liv.Lck.Tablet
{
    public class LckSplitNotification : LckBaseNotification
    {
        [field: SerializeField, Header("UI References")] 
        public GameObject AndroidUI {  get; private set; }

        [field: SerializeField] 
        public GameObject DesktopUI { get; private set; }

        private void Start()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                AndroidUI.SetActive(true);
                DesktopUI.SetActive(false);
            }
            else
            {
                DesktopUI.SetActive(true);
                AndroidUI.SetActive(false);
            }
        }
    }
}

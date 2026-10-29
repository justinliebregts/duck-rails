using System.Collections;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    public class LckLivHubButton : MonoBehaviour
    {
        private const long PRODUCTION_APPID = 24199129276346881;

        [SerializeField]
        private GameObject _livHubButtonGameObject;

        private void Start()
        {
            if (Application.platform != RuntimePlatform.Android && Application.isEditor == false)
            {
                if (_livHubButtonGameObject)
                {
                    _livHubButtonGameObject.SetActive(false);
                }
            }
        }

        public void OpenMetaStoreApp()
        {
            StartCoroutine(OpenStoreAppCoroutine());
        }

        private IEnumerator OpenStoreAppCoroutine()
        {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor)
            {
                yield break;
            }

            AndroidJavaClass unityPlayer = null;
            AndroidJavaObject currentActivity = null;
            AndroidJavaObject packageManager = null;
            AndroidJavaObject intent = null;
            AndroidJavaObject checkIntent = null;

            try
            {
                unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                if (unityPlayer == null) throw new System.Exception("Failed to create UnityPlayer class");

                currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (currentActivity == null) throw new System.Exception("Failed to get current activity");

                packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
                if (packageManager == null) throw new System.Exception("Failed to get package manager");

                // check if we have permission to launch / control center is installed
                checkIntent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", "tv.liv.controlcenter");
                if (checkIntent != null)
                {
                    intent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", "com.oculus.vrshell");
                    if (intent == null) throw new System.Exception("Failed to find com.oculus.vrshell package.");

                    intent.Call<AndroidJavaObject>("putExtra", "intent_data", "tv.liv.controlcenter/.MainActivity");
                    if (intent == null) throw new System.Exception("Failed to add extra intent data tv.liv.controlcenter/.MainActivity");

                    // Add flags to ensure the VR shell starts properly
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000 | 0x00040000); // FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_REORDER_TO_FRONT

                    currentActivity.Call("startActivity", intent);
                }
                else
                {
                    // if checkIntent fails just open store to control center app page
                    intent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", "com.oculus.vrshell");
                    if (intent == null) throw new System.Exception("Failed to set launch intent to com.oculus.vrshell");

                    intent.Call<AndroidJavaObject>("putExtra", "intent_data", "com.oculus.store/.StoreActivity");
                    if (intent == null) throw new System.Exception("Failed to put extra intent data com.oculus.store/.StoreActivity");
                    intent.Call<AndroidJavaObject>("putExtra", "uri", "/item/" + PRODUCTION_APPID.ToString());
                    if (intent == null) throw new System.Exception("Failed to put extra intent data appID: " + PRODUCTION_APPID);

                    currentActivity.Call("startActivity", intent);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to launch store app: {ex.Message}\n{ex.StackTrace}");
            }

            if (intent != null) intent.Dispose();
            if (checkIntent != null) checkIntent.Dispose();
            if (packageManager != null) packageManager.Dispose();
            if (currentActivity != null) currentActivity.Dispose();
            if (unityPlayer != null) unityPlayer.Dispose();

            yield break;
        }
    }
}
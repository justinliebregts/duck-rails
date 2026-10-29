using UnityEngine;

namespace Liv.Lck.Tablet
{
    public abstract class LckBaseNotification : MonoBehaviour
    {
        [field: SerializeField, Header("Settings")] 
        public bool RemainOnScreen { get; private set; } = false;
        [field: SerializeField, Header("Duration To Show On Screen When Remain On Screen Is False")]
        public float ShowDuration { get; private set; } = 3;

        public GameObject SpawnedGameObject { get; private set; }

        public virtual void ShowNotification()
        {
            if (SpawnedGameObject != null) SpawnedGameObject.SetActive(true);
        }
        public virtual void HideNotification()
        {
            if (SpawnedGameObject != null) SpawnedGameObject.SetActive(false);
        }

        public void SetSpawnedGameObject(GameObject go)
        {
            SpawnedGameObject = go;
        }
    }
}

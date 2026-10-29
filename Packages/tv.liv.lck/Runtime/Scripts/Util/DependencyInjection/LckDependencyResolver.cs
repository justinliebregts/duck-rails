using UnityEngine;

namespace Liv.Lck.DependencyInjection
{
    [DefaultExecutionOrder(-800)]
    public class LckDependencyResolver : MonoBehaviour
    {
        private void Awake()
        {
            var injector = LckDiContainer.Instance.GetInjector();
            if (injector == null)
            {
                Debug.LogError($"LCK initialization error: Ensure {nameof(LckServiceInitializer)} is in the scene");
                return;
            }
            
            var allComponents = gameObject.GetComponents<MonoBehaviour>();
            foreach (var monoBehaviour in allComponents)
            {
                injector.Inject(monoBehaviour);
            }
        }
    }
}
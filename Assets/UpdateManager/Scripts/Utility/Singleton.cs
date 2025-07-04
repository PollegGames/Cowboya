using UnityEngine;

namespace JoostenProductions {
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour {
        protected abstract bool DoNotDestroyOnLoad { get; }
        protected static bool isShuttingDown = false;

        private static T instance;
        private static readonly object objectLock = new object(); // Lock object should always be initialized.

        private static readonly System.Type instanceType = typeof(T);

        public static T Instance {
            get {
                if (isShuttingDown) {
                    Debug.LogWarning("Tried to access " + instanceType.Name + " while the application is shutting down! This is not allowed.");
                    return null;
                }

                lock (objectLock) {
                    if (instance != null) return instance;

                    // Use the updated method to find the instance.
                    instance = FindFirstObjectByType<T>();

                    if (instance != null) return instance;

                    // Create a new GameObject to hold the singleton instance if one doesn't exist.
                    instance = new GameObject(instanceType.Name).AddComponent<T>();

                    // Handle the DoNotDestroyOnLoad setting.
                    SingletonBehaviour<T> singleton = instance as SingletonBehaviour<T>;
                    if (singleton != null && singleton.DoNotDestroyOnLoad) {
                        DontDestroyOnLoad(instance.gameObject);
                    }

                    return instance;
                }
            }
        }

        private void OnApplicationQuit() {
            isShuttingDown = true;
        }

        private void OnDestroy() {
            isShuttingDown = true;
        }
    }
}

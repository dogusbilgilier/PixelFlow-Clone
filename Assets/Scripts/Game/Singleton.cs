using UnityEngine;

namespace Game
{
    /// <summary>
    /// An abstract class that provides base functionalities of a singleton for its derived classes
    /// </summary>
    /// <typeparam name="T">The type of singleton instance</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : Component
    {
        private static T s_Instance;

        public static T Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<T>();
                    if (s_Instance == null)
                    {
                        GameObject gameObject = new GameObject() { name = typeof(T).Name };
                        s_Instance = gameObject.AddComponent<T>();
                    }
                }

                return s_Instance;
            }
        }

        private void Awake()
        {
            bool destroyed = false;

            if (s_Instance == null)
            {
                s_Instance = this as T;
            }
            else
            {
                destroyed = true;
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }

            if (!destroyed)
            {
                OnPostAwake();
            }
        }

        protected virtual void OnPostAwake() => _ = 0;
    }
}
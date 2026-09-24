namespace ZGameFramework.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    public abstract class PersistentMonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;

        private static readonly object lockObj = new object();

        public static T Instance
        {
            get
            {
                {
                    if (instance == null)
                    {
                        lock (lockObj)
                        {
                            if (instance == null)
                            {
                                instance=FindObjectOfType<T>();

                                if (instance == null)
                                {
                                    GameObject singletonObj = new GameObject();
                                    singletonObj.name = typeof(T).Name+"AutoCreator";
                                    instance = singletonObj.AddComponent<T>();
                                }

                                DontDestroyOnLoad(instance.gameObject);
                            }
                        }

                    }

                    return instance;

                }
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
    }
}

namespace ZGameFramework.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    public abstract class Singleton<T> where T : Singleton<T>
    {
        private static T instance;

        private static readonly object lockObj = new object();

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        if (instance == null)
                        {
                            Type type = typeof(T);
                            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
                            ConstructorInfo constructor = Array.Find(constructors, c => c.GetParameters().Length == 0);

                            if (constructor == null)
                            {
                                throw new Exception($"【单例报错】{type.Name} 缺少私有的无参构造函数！");
                            }

                            instance = constructor.Invoke(null) as T;
                        }
                    }

                }

                return instance;
            }
        }

        protected Singleton() { }
    }
    
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ZGameFramework
{
    public class IOCContainer 
    {
        private Dictionary<Type, object> m_Instances = new();
        public void Register<T>(T instance)
        {
            var key=typeof(T);

            if (m_Instances.ContainsKey(key)) 
            {
                m_Instances[key] = instance;
            }
            else
            {
                m_Instances.Add(key, instance);
            }
        }

        public T Get<T>()where T:class
        {
            var key = typeof(T);
            if (m_Instances.TryGetValue(key,out var instance))
            {
                return instance as T;
            }
            else 
            {
                UnityEngine.Debug.LogError($"{key.Name}Î´×¢²áµ«³¢ÊÔ»ñÈ¡");
                return null;
            }
        }

        public IEnumerable<T> GetInstanceByType<T>()
        {
            var type = typeof(T);
            var instances=m_Instances.Values.Where
                (instance=>type.IsInstanceOfType(instance)).Cast<T>();
            return instances;
        } 

        public void Clear() => m_Instances.Clear();
    }
}
using System;
using System.Collections.Generic;

namespace ZGameFramework
{
    public interface IReadOnlyBindableProperty<T>
    {
        public T Value { get; }
        public IUnregister Register(Action<T> onValueChanged);
        public void  Unregister(Action<T>  onValueChanged);
        public IUnregister RegisterWithInitValue(Action<T>  onValueChanged);
    }

    public interface IBindableProperty<T>: IReadOnlyBindableProperty<T>
    {
        new T  Value { get; set; }
        public void SetValueWithoutEvent(T value);
    }

    public class BindableProperty<T>:IBindableProperty<T>
    {
        private T m_Value;
        private Action<T> m_OnValueChanged;
        private Func<T,T,bool> m_Compare;

        
        public T Value
        {
            get => m_Value;
            set
            {
                if (m_Compare(value,m_Value))
                {
                    return;
                }
                m_Value = value;
                m_OnValueChanged?.Invoke(m_Value);
            }
        }
        

        public BindableProperty (T defaultValue =default,
            Func<T,T,bool> compare=null)
        {
            m_Value = defaultValue;
            m_Compare=compare??EqualityComparer<T>.Default.Equals;
        }

        public void SetValueWithoutEvent(T value)
        {
              m_Value = value;
        }

        
        
        public IUnregister Register(Action<T> onValueChanged)
        {
            m_OnValueChanged+= onValueChanged;
            return new CustomUnregister(()=>m_OnValueChanged-= onValueChanged);
        }

        public void Unregister(Action<T> onValueChanged)
        {
            m_OnValueChanged-= onValueChanged;
        }

        public IUnregister RegisterWithInitValue(Action<T> onValueChanged)
        {
            onValueChanged?.Invoke(m_Value);
            return Register(onValueChanged);
        }

       }
}
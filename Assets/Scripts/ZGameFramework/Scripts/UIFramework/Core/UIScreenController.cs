using System;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZGameFramework.UIFramework
{
    public class UIScreenController<TProps>:MonoBehaviour,IScreenController
        where TProps:IScreenProperties
    {
        [Label("面板显示动画")] 
        [SerializeField] private AniComponent animIn;
        
        [Label("面板关闭动画")] 
        [SerializeField] private AniComponent animOut;

        [SerializeField] private TProps properties;
        
        private IArchitecture m_Architecture;
        
        public string ScreenId { get; set; }

        public bool IsVisible { get; private set; }

        protected TProps Properties
        {
            get => properties;
            set => properties = value; 
        }

        public Action<IScreenController> InTransitionFinished { get; set; }
        public Action<IScreenController> OutTransitionFinished { get; set; }
        public Action<IScreenController> CloseRequest { get; set; }
        public Action<IScreenController> ScreenDestroyed { get; set; }

        public AniComponent AnimIn {  get => animIn; set => animIn = value; }
        public AniComponent AnimOut { get => animOut; set => animOut = value; }

        protected virtual void Awake()
        {
            AddListener();
        }

        protected virtual void OnDestroy()
        {
            ScreenDestroyed?.Invoke(this);

            InTransitionFinished = null;
            OutTransitionFinished = null;
            CloseRequest = null;
            ScreenDestroyed = null;
            RemoveListener();
        }

        protected virtual void RemoveListener()
        {
            
        }

        protected virtual void AddListener()
        {
            
        }

        protected virtual void OnPropertiesSet()
        {
        }

        protected virtual void WhileHiding()
        {
            
        }

        protected virtual void HierarchyFixOnShow()
        {
        }

        public void Hide(bool animate = true)
        {
            DoAnimation(animate ? animOut : null, OnTransitionOutFinished,false);
            WhileHiding();
        }

        public void Show(IScreenProperties props = null)
        {
            if (props!=null)
            {
                if (props is TProps)
                {
                    SetProperties((TProps)props);
                }
                else
                {
                    Debug.LogError($"{ScreenId}设置的属性与其面板属性不匹配");
                    return;
                }
            }    
            
            HierarchyFixOnShow();
            OnPropertiesSet();

            if (!gameObject.activeSelf)
            {
                DoAnimation(animIn, OnTransitionInFinished, true);
            }
            else
            {
                InTransitionFinished?.Invoke(this);
            }
        }

        private void DoAnimation(AniComponent caller, Action callWhenFinished
            , bool isVisible)
        {
            if (caller==null)
            {
                gameObject.SetActive(isVisible);
                callWhenFinished?.Invoke();
            }
            else
            {
                if (isVisible&&!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                caller.Animate(transform, callWhenFinished);
            }
        }

        protected virtual void SetProperties(TProps properties)
        {
            this.properties = properties;
        }

        private void OnTransitionInFinished() 
        {
            IsVisible = true;

            InTransitionFinished?.Invoke(this);
        }

        private void OnTransitionOutFinished()
        {
            IsVisible = false;
            gameObject.SetActive(false);

            if (OutTransitionFinished != null)
            {
                OutTransitionFinished(this);
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture() => m_Architecture;
        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            m_Architecture = architecture;
        }
    }
}
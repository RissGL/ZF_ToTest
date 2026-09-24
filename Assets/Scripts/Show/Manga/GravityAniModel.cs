using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZGameFramework;

namespace ZF.DialoguePresentation
{
    public interface IGravityAniModel : IModel
    {
         IReadOnlyBindableProperty<int> Total { get; }
         IReadOnlyBindableProperty<int> CurrentIndex { get; }
         void MoveNext();
         void Setup(int total,int startIndex = -1);
    }

    public class GravityAniModel:AbstractModel,IGravityAniModel
    {
        private readonly BindableProperty<int> m_Total = new BindableProperty<int>(0);

        public IReadOnlyBindableProperty<int> Total => m_Total;
        //初始为-1相当于标题页面
        private readonly BindableProperty<int>
            m_CurrentIndex = new BindableProperty<int>(-1);
        public IReadOnlyBindableProperty<int>CurrentIndex => m_CurrentIndex;
        /*public int Total { get; private set;}*/
        /*private int currentTargetPositionIndex = 0;*/
        
        public GravityAniModel(int total)
        {
            m_Total.SetValueWithoutEvent(total);
        }

        public void Setup(int total, int startIndex = -1)
        {
            m_Total.SetValueWithoutEvent(total);
            m_CurrentIndex.SetValueWithoutEvent(startIndex);
        }

        

        public void MoveNext()
        {
            if(m_Total.Value==0) return;
            Debug.Log("xyg");
            m_CurrentIndex.Value = (m_CurrentIndex.Value+1) % m_Total.Value;
        }

        /*
        public int GetNextIndex()
        {
            var i=currentTargetPositionIndex;
            currentTargetPositionIndex= (currentTargetPositionIndex+1)%Total;
            return i;
        }*/

        protected override void OnInit()
        {
            
        }
    }
}
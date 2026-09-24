using UnityEngine;
using UnityEngine.UI;

namespace ZGameFramework.UIFramework
{
    public class UIDemoStartWindow:UIDemoWindowController
    {
        [SerializeField] private Button startBtn;
        
        protected override void AddListener()
        {
            startBtn.onClick.AddListener(UIDemoStart);
        }

        private void UIDemoStart()
        {
            this.SendEvent<StartUIDemoEvent>();
        }
    }
}
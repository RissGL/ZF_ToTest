using UnityEngine;

namespace ZGameFramework.UIFramework
{
    public class PanelUILayer:UILayer<IPanelController>
    {
        [SerializeField] [Tooltip("优先级并行层的设置，注册到此层的面板将根据其优化级重新归属到不懂的并行层对象")]
        private PanelPriorityLayerList priorityLayers = null;

        public override void ShowScreen(IPanelController screen)
        {
            screen.Show();
        }

        public override void ShowScreen<TProps>(IPanelController screen, TProps props)
        {
            screen.Show(props);
        }

        public override void HideScreen(IPanelController screen)
        {
            screen.Hide();
        }

        public override void ReparentScreen(IScreenController controller, Transform screenTransform)
        {
            var ctl=controller as IPanelController;
            if (ctl!=null)
            {
                ReparentToParaLayer(ctl.Priority, screenTransform);
            }
            else
            {
                base.ReparentScreen(controller, screenTransform);
            }
        }

        private void ReparentToParaLayer(PanelPriority priority, Transform screenTransform)
        {
             if (!priorityLayers.ParaLayerLookup.TryGetValue(priority, out Transform trans))
             {
                 trans = transform;
             }
             
             screenTransform.SetParent(trans,false);
        }

        public bool IsPanelVisible(string panelId)
        {
            if (registerScreens.TryGetValue(panelId,out var panel))
            {
                return panel.IsVisible;
            }

            return false;
        }
    }
}
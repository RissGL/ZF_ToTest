using UnityEngine;

namespace ZGameFramework.Utility
{
    using ZGameFramework.Core;
    public class MouseWorld :MonoSingleton<MouseWorld>
    {
        [SerializeField] private LayerMask layerMask;

        private Camera mainCamera; // 缓存主相机
        protected override void Awake()
        {
            base.Awake();
            mainCamera= Camera.main; // 获取主相机
        }

        public static bool TryGetMouseWorldPosition(out Vector3 hitPotion)
        {
            hitPotion=Vector3.zero;

            //防止在场景视图或者没有主相机的情况下调用导致报错
            if (Instance == null || Instance.mainCamera == null) return false;

            Ray ray = Instance.mainCamera.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());
            if (Physics.Raycast(ray, out RaycastHit hitInfo, float.MaxValue, Instance.layerMask)) 
            {
                hitPotion = hitInfo.point;
                return true;
            }

            return false;
        }
    }
}

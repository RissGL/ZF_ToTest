using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZGameFramework.UIFramework
{
    public class UIFollowComponent:MonoBehaviour
    {
        [SerializeField] private Text label = null;
        [SerializeField] private Image icon = null;
        [SerializeField] private bool clampAtBorders = true;
        [SerializeField] private bool rotateWhenClamped = true;
        [SerializeField] private RectTransform rotatingElement = null;
        
        public event Action<UIFollowComponent> LabelDestroyed;

        private Transform currentFollow;
        private RectTransform rectTransform;
        private CanvasScaler parentScaler;
        private RectTransform mainCanvasRectTransform;

        public static Vector2 GetAnchoredPosition(Camera viewingCamera, Transform followTransform
            , CanvasScaler canvasScaler, Rect followElementRect)
        {
            var relativePosition = viewingCamera.transform.InverseTransformPoint(followTransform.position);
            relativePosition.z = Mathf.Max(relativePosition.z, 1f);
            var viewportPos = viewingCamera.WorldToViewportPoint
                (viewingCamera.transform.TransformPoint(relativePosition));
			
            return new Vector2(viewportPos.x * canvasScaler.referenceResolution.x - followElementRect.size.x / 2f,
                viewportPos.y * canvasScaler.referenceResolution.y - followElementRect.size.y / 2f);
        }
        
        public static Vector2 GetClampedOnScreenPosition(Vector2 onScreenPosition, Rect followElementRect, RectTransform mainCanvasRectTransform) 
        {
            return new Vector2(Mathf.Clamp(onScreenPosition.x, 0f, mainCanvasRectTransform.sizeDelta.x - followElementRect.size.x),
                Mathf.Clamp(onScreenPosition.y, 0f, mainCanvasRectTransform.sizeDelta.y - followElementRect.size.y));
        }
        
        private void Start() {
            mainCanvasRectTransform = transform.root as RectTransform;
            rectTransform = transform as RectTransform;
            parentScaler = mainCanvasRectTransform.GetComponent<CanvasScaler>();

            if (rotatingElement == null) {
                rotatingElement = rectTransform;
            }
        }

        private void OnDestroy() {
            if (LabelDestroyed != null) {
                LabelDestroyed(this);
            }
        }

        public void SetFollow(Transform toFollow) {
            currentFollow = toFollow;
        }

        public void SetText(string label) {
            this.label.text = label;
        }
	
        public void SetIcon(Sprite icon) {
            this.icon.sprite = icon;
        }
        
        protected void PositionAtOrigin() {
            var mainSize = mainCanvasRectTransform.sizeDelta;
            var labelSize = rectTransform.rect.size;
            rectTransform.anchoredPosition = new Vector2((mainSize.x - labelSize.x) / 2f, mainSize.y / 2f);
        }
	
        public void UpdatePosition(Camera cam) {
            if (currentFollow != null) {
                var onScreenPosition = GetAnchoredPosition(cam, currentFollow.transform, parentScaler, rectTransform.rect);
                if (!clampAtBorders) {
                    rectTransform.anchoredPosition = onScreenPosition;
                    return;
                }
	
                var clampedPosition = GetClampedOnScreenPosition(onScreenPosition, rectTransform.rect, mainCanvasRectTransform);
                rectTransform.anchoredPosition = clampedPosition;
	
                if (!rotateWhenClamped) {
                    return;
                }
	
                if (onScreenPosition != clampedPosition) {
                    var delta = clampedPosition - onScreenPosition;
                    var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    rotatingElement.localRotation = Quaternion.AngleAxis(angle-90f, Vector3.forward);
                }
                else {
                    rotatingElement.localRotation = Quaternion.identity;
                }
            }
        }
    }
}
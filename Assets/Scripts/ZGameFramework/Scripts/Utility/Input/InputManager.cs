using System;
using UnityEngine;
using UnityEngine.InputSystem; // 必须引用命名空间
using ZGameFramework.Core;

namespace ZGameFramework.Utility
{
    public class InputManager : Singleton<InputManager>
    {
        private PlayerInputAction playerInputActions;

        public event Action OnLeftMouseClick;
        public event Action OnRightMouseClick;
        public event Action OnMiddleMouseClick;

        private InputManager() 
        {
            playerInputActions = new PlayerInputAction();
            playerInputActions.Player.LeftMouseClick.performed += ctx => OnLeftMouseClick?.Invoke();
            playerInputActions.Player.RightMouseClick.performed += ctx => OnRightMouseClick?.Invoke();
            playerInputActions.Player.MiddleMouseClick.performed += ctx => OnMiddleMouseClick?.Invoke();
        }

        public Vector2 GetMouseScreenPosition()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;// 获取鼠标屏幕位置，防止在没有鼠标设备的情况下调用导致报错
        }
        public bool GetMouseLeftDown() => playerInputActions.Player.LeftMouseClick.WasPressedThisFrame();// 获取左键点击
        public bool GetMouseRightDown() => playerInputActions.Player.RightMouseClick.WasPressedThisFrame();// 获取左键点击

        public bool GetMiddleMouseDown() => playerInputActions.Player.MiddleMouseClick.WasPressedThisFrame();// 获取中键点击

        public Vector2 GetScrollValue()=> playerInputActions.Player.Scroll.ReadValue<Vector2>();// 获取滚轮输入

        public void EnableInput() => playerInputActions.Enable();// 启用输入

        public void DisableInput() => playerInputActions.Disable();// 禁用输入
    }
}
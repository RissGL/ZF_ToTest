using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一个选项按钮：只管「长什么样 + 被点了/被高亮了」。
    /// 不认识对话数据 —— 文本和下标由 DialogueChoicePanel 塞进来。
    ///
    /// 选中高亮走底图换色 + 文字加粗（鼠标 hover 的变色是 Button 自带的 tint，两者叠加不冲突）。
    /// </summary>
    public class DialogueChoiceButton : MonoBehaviour
    {
        [Header("引用")]
        [Label("按钮")]
        [SerializeField] private Button button;

        [Label("文本")]
        [SerializeField] private TMP_Text label;

        [Label("底图（高亮换色用，可空）")]
        [SerializeField] private Image background;

        [Header("外观")]
        [Label("未选中的底色")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.94f);

        [Label("选中（高亮）的底色")]
        [SerializeField] private Color selectedColor = new Color(1f, 0.87f, 0.62f, 1f);

        /// <summary>选项下标（Model 的 Choose(index) 认这个），Setup 时填</summary>
        public int Index { get; private set; } = -1;

        /// <summary>被鼠标点了</summary>
        public event Action<int> Clicked;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (button != null)
            {
                button.onClick.AddListener(() => Clicked?.Invoke(Index));
            }
        }

        /// <summary>填内容：index 是选项下标，text 是选项文本</summary>
        public void Setup(int index, string text)
        {
            Index = index;
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
            SetSelected(false);
        }

        /// <summary>键盘选中的高亮（鼠标 hover 不归它管）</summary>
        public void SetSelected(bool selected)
        {
            if (background != null)
            {
                background.color = selected ? selectedColor : normalColor;
            }

            if (label != null)
            {
                label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}

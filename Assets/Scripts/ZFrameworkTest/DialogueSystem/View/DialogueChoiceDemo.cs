using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 选项面板单测（挂在气泡测试 rig 上）：不启动对话也能看面板的手感。
    ///   C = 弹一组假选项
    ///   X = 收起
    ///   ↑↓/WS = 移动高亮   回车/空格 = 确认   数字键 1-9 = 直选
    /// 选了什么会打到 Console。
    /// </summary>
    public class DialogueChoiceDemo : MonoBehaviour
    {
        [Label("选项面板")]
        [SerializeField] private DialogueChoicePanel panel;

        private void Start()
        {
            if (panel != null)
            {
                panel.Chosen += index => Debug.Log($"[选项演示] 选了第 {index} 个");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                panel?.Show(new List<DialogueChoiceData>
                {
                    new DialogueChoiceData { text = "选项一：往左边的巷子走" },
                    new DialogueChoiceData { text = "选项二：翻过围墙看看" },
                    new DialogueChoiceData { text = "选项三：原地等天亮" },
                });
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                panel?.Hide();
            }
        }
    }
}

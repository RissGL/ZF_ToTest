using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 气泡系统自测脚本：挂在 BubbleStage 同一个物体上（或者手动拖引用）。
    /// 完全不碰对话系统，用来单独验证气泡行为。
    ///
    /// 按键：
    ///   1 / 2 / 3  三个不同槽说话（同一个键连按 = 同一个槽收掉重弹）
    ///   4          换样式（轮换 styles 列表）
    ///   空格       立刻补全当前正在打字的那些气泡（等价于点「继续」快进）
    ///   0          全部收起
    ///   H          打印当前状态到 Console
    ///
    /// ★ 这里的"槽 id"就是场景里 BubbleStage 上那三个槽的 slotId（搭建器生成的：阿岚 / 老张 / 旁白）。
    ///   槽 id 对不上时会自动领一个空槽，Console 会有提示。
    /// </summary>
    public class BubbleDemo : MonoBehaviour
    {
        [Label("气泡舞台")]
        [SerializeField] private BubbleStage stage;

        [Label("演示样式（按 4 轮换；空的就用气泡自己的默认样式）")]
        [SerializeField] private List<BubbleStyleSO> styles = new List<BubbleStyleSO>();

        [Label("打字速度覆盖（秒/字，<=0 用样式里的）")]
        [SerializeField] private float speedOverride;

        private readonly Dictionary<string, int> m_LineIndex = new Dictionary<string, int>();
        private int m_StyleIndex;

        /// <summary>三个演示槽（要和舞台上的槽 id 一致）</summary>
        private static readonly string[] Slots = { "阿岚", "老张", "旁白" };

        private static readonly string[][] Lines =
        {
            new[]
            {
                "这地方不对劲。",
                "你看那边的影子，它刚才动了。",
                "别管那么多，先把东西拿到手再说——剩下的等下再想。",
            },
            new[]
            {
                "我早说了吧。",
                "撤。",
                "……行，这次听你的。不过下次别再让我走前面了。",
            },
            new[]
            {
                "（风从巷口灌进来，带着铁锈味。）",
                "（远处有什么东西倒下了。）",
            },
        };

        private void Awake()
        {
            if (stage == null)
            {
                stage = GetComponent<BubbleStage>();
            }
        }

        private void Update()
        {
            if (stage == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) Say(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Say(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Say(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) CycleStyle();
            if (Input.GetKeyDown(KeyCode.Space)) stage.CompleteTyping();
            if (Input.GetKeyDown(KeyCode.Alpha0)) stage.HideAll();
            if (Input.GetKeyDown(KeyCode.H)) PrintState();
        }

        private void Say(int speakerIndex)
        {
            string key = Slots[speakerIndex];
            string[] pool = Lines[speakerIndex];

            m_LineIndex.TryGetValue(key, out int index);
            string text = pool[index % pool.Length];
            m_LineIndex[key] = index + 1;

            stage.Show(new BubbleRequest
            {
                key = key,
                text = text,
                displayName = key,
                style = CurrentStyle(),
                speedOverride = speedOverride,
                onTyped = () => Debug.Log($"[气泡] 「{key}」这一句打完了"),
            });
        }

        private BubbleStyleSO CurrentStyle()
        {
            if (styles == null || styles.Count == 0)
            {
                return null;
            }

            return styles[m_StyleIndex % styles.Count];
        }

        private void CycleStyle()
        {
            if (styles == null || styles.Count == 0)
            {
                Debug.Log("[气泡] 没有配演示样式");
                return;
            }

            m_StyleIndex = (m_StyleIndex + 1) % styles.Count;
            Debug.Log($"[气泡] 当前样式：{styles[m_StyleIndex].name}");
        }

        private void PrintState()
        {
            Debug.Log($"[气泡] 可见 {stage.VisibleCount} 个，正在打字：{stage.IsTyping}");
        }
    }
}

using UnityEngine;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.Game;

namespace ZF.Puzzle
{
    /// <summary>
    /// 把谜题规则表交给 PuzzleSystem。
    /// System 是架构在 OnInit 里 new 出来的，拿不到场景里的资产引用，所以由这个脚本在 Awake 里递一次。
    /// （规则 = System 的事，状态 = Model 的事，资产引用不放进 Model。）
    ///
    /// 顺带负责**装配**：反馈气泡那条线（谜题 → 对话系统）也在这儿挂上。
    /// 挂在这儿而不是让你手动加组件，是因为这个脚本本来就在场景里、又本来就知道规则表 ——
    /// "能跑起来"这件事不该再多一步配置。
    /// </summary>
    [DisallowMultipleComponent]
    public class PuzzleBootstrap : MonoBehaviour, IController
    {
        [Label("谜题规则表")]
        [SerializeField] private PuzzleTableSO table;

        [Label("开局在 Console 里打一句操作提示")]
        [SerializeField] private bool logHelp = true;

        [Label("反馈用对话气泡冒出来（没配模板时用代码搭的默认单人模板）")]
        [SerializeField] private bool useFeedbackBubble = true;

        public PuzzleTableSO Table => table;

        public IArchitecture GetArchitecture() => GameApp.Interface;

        private void Awake()
        {
            if (table == null)
            {
                Debug.LogError("[谜题] 规则表没填。跑一下菜单 Tools/谜题/搭建解密演示，或者手动拖一张表上来。");
                return;
            }

            this.GetSystem<IPuzzleSystem>().SetTable(table);

            if (useFeedbackBubble && GetComponent<PuzzleFeedbackBubble>() == null)
            {
                gameObject.AddComponent<PuzzleFeedbackBubble>();
            }

            if (logHelp)
            {
                Debug.Log($"[谜题] 规则表「{table.name}」已装载：{table.puzzles.Count} 个谜题 / {table.interactionRules.Count} 条交互规则。\n" +
                          "  左键点物体 = 交互　Tab = 切换手里的道具（背包 UI 还没做，先看 Console 日志）\n" +
                          "  背包里有正好合用的道具时会自动拿在手里用掉，不用先按 Tab。\n" +
                          "  反馈会以对话气泡冒出来，点一下关掉（不点也会自己收，不影响你继续点东西）。");
            }
        }
    }
}

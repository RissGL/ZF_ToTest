using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话布局模板（一个 templateId 对应一个 Prefab）上的引用集合。
    /// View 通过它拿部件，不需要认识模板内部的具体层级和物体名。
    ///
    /// 模板里该有什么：
    ///   - 一个 BubbleStage，里面是若干「槽（DialogueSlot）」
    ///       每个槽 = 一个角色位 = 气泡（Bubble）+ 立绘（Portrait），两者都可选
    ///       ★ 槽不认识角色：谁站哪个槽由数据决定（台词上的 slotId，空则自动分配）
    ///   - 选项面板的锚位 + 选项面板
    ///
    /// 立绘不再单独挂引用 —— 它属于槽，由 DialogueSlot / BubbleStage 管。
    /// </summary>
    public class DialogueLayoutRefs : MonoBehaviour
    {
        [Label("模板 id（跟对话数据的 templateId 对上；运行时靠模板表映射，这里只是备注）")]
        public int templateId;

        [Label("气泡舞台（里面的槽就是这一格的所有角色位）")]
        public BubbleStage bubbleStage;

        [Label("选项面板锚位（代码会把它提到最上层，别排在 BubbleStage 前面）")]
        public RectTransform choiceAnchor;

        [Label("选项面板（可空 = 这个模板没有选项 UI）")]
        public DialogueChoicePanel choicePanel;

        /// <summary>
        /// 模板实例化 / 切换时调：初始化舞台 + 把**选项面板提到最上层**。
        ///
        /// ★ 为什么要强制提上来：UGUI 里"谁画在上面"由**兄弟顺序**决定，越靠后越上层。
        ///   选项面板要是排在 BubbleStage **前面**，它就会被三个槽（框 / 立绘 / 背景 / 气泡）盖住 ——
        ///   而槽里非说话人的部件会被压到 dimAlpha（默认 0.55），盖在面板上就会让面板看起来"有点透明"，
        ///   还随谁说话变来变去，特别难查（模板 Prefab 里摆错顺序就中招）。
        ///   这里统一把选项锚位放到最后：面板永远在框 / 立绘 / 气泡之上。
        /// </summary>
        public void Init()
        {
            if (bubbleStage != null)
            {
                bubbleStage.Init();
            }

            if (choiceAnchor != null)
            {
                choiceAnchor.SetAsLastSibling();
            }
        }
    }
}

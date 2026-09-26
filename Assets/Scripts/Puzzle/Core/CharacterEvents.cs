using ZGameFramework.Core;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>有人换时代了（数据已经改完，人已经在新时代）。到场演出、音效、成就都挂这个。</summary>
    public class CharacterMovedEvent : GameEvent
    {
        public string CharacterId;
        public EraId FromEra;
        public EraId ToEra;
    }

    /// <summary>
    /// 点名 / 取消点名了某个人（CharacterId 为空 = 取消）。
    /// 想给"选中"加光圈、加音效、显示名字，都挂这个。
    /// </summary>
    public class CharacterSelectedEvent : GameEvent
    {
        public string CharacterId;
    }

    /// <summary>
    /// 【动画接口】要求某个人播一段动画。规则表里的 `PlayCharacterAnimationEffect` 发这个。
    ///
    /// 怎么用：把「播离场动画 → 等一会儿 → 真正搬过去 → 播到场动画」编排成一条规则里的效果链 ——
    /// 效果基类有 `delayBefore`，"等动画播完再改状态"不用写代码。
    ///
    /// 谁来播：`CharacterView` 监听这个事件，`OnPlayAnimation` 是**可以 override 的虚方法** ——
    /// 接 Animator / DOTween 序列 / 帧动画都行；不想用 CharacterView 就自己监听这个事件。
    /// </summary>
    public class CharacterAnimationEvent : GameEvent
    {
        public string CharacterId;

        /// <summary>动画名，自己约定。演示里用 "leave"（离场）/"arrive"（到场）。</summary>
        public string Clip;
    }
}

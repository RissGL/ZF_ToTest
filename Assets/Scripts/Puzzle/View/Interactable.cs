using UnityEngine;
using ZGameFramework;
using ZGameFramework.Utility;
using ZF.EraGallery;

namespace ZF.Puzzle
{
    /// <summary>
    /// 场景里一个能点的**东西**（岩壁、柴堆、壁炉、时间裂隙…）。人物是另一个类（CharacterView），
    /// 两者共用 StateVisualBehaviour 那套「状态组 + 状态规则 + 高亮 + 层级」。
    ///
    /// 它只做三件事：报自己的 id、按状态开关自己的状态组、被悬停时亮个框。
    /// 「点了之后发生什么」全在规则表里，这个组件一个字都不写。
    /// </summary>
    [DisallowMultipleComponent]
    public class Interactable : StateVisualBehaviour
    {
        [Header("身份")]
        [Label("唯一 id（规则表靠它引用这个物体）")]
        [SerializeField] private string id = "";

        [Label("所在时代（只用来在 Inspector 里归类，条件判定不看它）")]
        [SerializeField] private EraId era = EraId.Stone;

        [Label("显示名（日志 / 提示用）")]
        [SerializeField] private string displayName = "";

        public string Id => id;
        public EraId Era => era;
        public override string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;

        protected override EraId SceneEra => era;

        public override string InteractionId => id;
    }
}

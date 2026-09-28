using System.Collections.Generic;
using UnityEngine.UI;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 「旧画面要活到擦完」的标记接口 —— 溶解 / 交叉类擦除实现它。
    ///
    /// 为什么需要：普通擦除是"一层不透明纸盖上去"，所以换格一开始就可以把旧气泡收掉（观众看不见）。
    /// 但**溶解**是旧画面**当场被侵蚀掉**，旧画面必须完整活到溶解结束 —— 时序器看到这个标记就会：
    ///   ① 不在换格开始时收旧气泡
    ///   ② 让模板 Host **保留旧模板实例**（压在新实例上面）
    ///   ③ 擦完了再调 <see cref="IDialogueFrameSource.ReleasePreviousFrame"/> 放掉旧画面
    ///
    /// 不实现它的擦除（遮罩平移 / 条纹 / Instant）行为完全不变。
    /// </summary>
    public interface IDialogueTransitionKeepsOldFrame
    {
    }

    /// <summary>
    /// 「给我旧画面由哪些可绘制部件组成」+「擦完把旧画面放掉」。
    ///
    /// 溶解类擦除要把自己那个材质贴到旧画面的每个 <see cref="Graphic"/> 上，
    /// 所以它需要这一份清单；擦完还要把材质还原（模板实例是缓存复用的，
    /// 不还原的话下次切回这个模板会带着"半溶解"状态回来）。
    ///
    /// 默认实现是 <see cref="DialogueView"/>（挂在场景里），擦除组件上拖一下就行。
    /// </summary>
    public interface IDialogueFrameSource
    {
        /// <summary>现在这一格（= 新画面）里所有可绘制部件（含未激活的，按层级顺序）</summary>
        IReadOnlyList<Graphic> CurrentGraphics { get; }

        /// <summary>现在有没有"被保留的旧画面"（换模板了才有；同一个模板换格时没有）</summary>
        bool HasHeldPreviousFrame { get; }

        /// <summary>
        /// 被保留的旧画面里的可绘制部件。
        /// ★ 溶解要在**换内容之后**才收它（换之前收的是新画面，会溶错对象）。
        /// 没保留时返回空列表。
        /// </summary>
        IReadOnlyList<Graphic> HeldPreviousGraphics { get; }

        /// <summary>
        /// 擦完了：放掉旧画面 —— 停用被保留的旧模板实例、收掉旧气泡舞台。
        /// **不触发任何回调**，调用方（时序器）自己决定后面做什么。
        /// </summary>
        void ReleasePreviousFrame();
    }
}

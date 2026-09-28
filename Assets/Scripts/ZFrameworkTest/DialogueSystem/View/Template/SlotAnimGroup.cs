using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一组动画 = **在哪个目标上播** + 播什么效果。
    ///
    /// ★ 这就是"播动画的口"：代码不知道你的框里有什么（四条边？浅色底？花纹？），
    ///   它只认「目标 Transform + 效果列表」。你想让哪个子物体怎么动，
    ///   就在模板 Prefab 里把这个子物体拖到「目标」上，然后按「添加动画效果」加效果。
    ///
    /// 典型用法（那种"边从短到长围出框 → 底色淡入 → 人物出现"的入场）：
    ///   框的 enterGroups 里放几组：
    ///     组1 目标 = 上边（加 ScaleEffect / 以后加的"改尺寸"效果）
    ///     组2 目标 = 下边 …… 四条边各一组（或共用一组也行，看你要的效果）
    ///     组3 目标 = 浅色底（加 CanvasGroupFadeEffect 淡入）
    ///   先后顺序用**效果自己的 delay** 排（边的 delay 小、底色的 delay 大）。
    ///   人物最后出现：立绘是兄弟节点，给它自己的入场效果调一个更大的 delay 即可。
    ///
    /// 目标留空 = 作用在本组件所在的物体上（整个框/整个立绘一起动）。
    /// ⚠️ 目标只能指向**同一个 Prefab 里**的物体，所以这种组配在 Prefab 上；
    ///    想跨模板复用的"整体动画"请用 animSet（共享资产，效果作用于本物体）。
    /// </summary>
    [Serializable]
    public class SlotAnimGroup
    {
        [Label("目标（空 = 本物体；填了就是它下面某个子物体）")]
        public Transform target;

        [Label("效果列表（点「添加动画效果」加；先后用效果自己的 delay 排）")]
        [SerializeReference]
        public List<MangaAnimEffect> effects = new List<MangaAnimEffect>();
    }
}
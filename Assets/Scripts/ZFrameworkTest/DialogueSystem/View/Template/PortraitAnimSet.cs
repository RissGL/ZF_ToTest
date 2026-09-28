using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 立绘的一套「手感」：入场 / 退场动画 + 音效。
    /// 和 BubbleAnimSet 一个思路：做成资产，一套手感到处复用，实例上换的是资产引用。
    ///
    /// ⚠️ 动画是编辑期配置；运行时代码改这里改的是内存副本，不会存回资产。
    ///
    /// 入场 / 退场建议互为镜像（入场 scale→1 + alpha→1，退场 scale→0.94 + alpha→0），
    /// 因为 Portrait 每次 Enter 前会强制回到「收起状态」（hiddenScale / hiddenAlpha）。
    /// </summary>
    [CreateAssetMenu(menuName = "Presentation/Portrait Anim Set", fileName = "PortraitAnim_")]
    public class PortraitAnimSet : ScriptableObject
    {
        [Header("两套动画（用「添加动画效果」下拉加）")]
        [Label("入场")]
        [SerializeReference]
        public List<MangaAnimEffect> enterEffects = new List<MangaAnimEffect>();

        [Label("退场")]
        [SerializeReference]
        public List<MangaAnimEffect> exitEffects = new List<MangaAnimEffect>();

        [Header("音效")]
        [Label("入场")]
        public ShowAudioEventSO enterAudio;

        [Label("退场")]
        public ShowAudioEventSO exitAudio;
    }
}

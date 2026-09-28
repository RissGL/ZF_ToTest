using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 气泡的一套「手感」：两套动画 + 弹出/收起音效 + 打字速度。
    ///
    /// 为什么单独做成资产：
    ///   ① 一套手感到处复用（"轻弹" / "从左飘入" / "喊叫" / "拟声词" 各做一个）
    ///   ② 实例上想换手感时换的是「资产引用」——这是 Unity 最可靠的一种 Prefab override；
    ///      而 [SerializeReference] 的列表直接配在 Prefab 实例上，历史上坑比较多，所以这里绕开
    ///   ③ 能进版本控制被 diff
    ///
    /// ⚠️ 动画是「编辑期配置」。运行时代码改这里改的是内存副本，不会存回资产，
    ///    而且会影响所有引用它的地方。以后真要剧情中途换手感，另开显式接口。
    ///
    /// 进场 / 退场两套效果建议互为镜像（比如进场 scale→1 + alpha→1，退场 scale→0.8 + alpha→0），
    /// 因为 Bubble 每次 Show 前会强制回到「收起状态」（气泡上的 hiddenScale / hiddenAlpha）。
    /// </summary>
    [CreateAssetMenu(menuName = "Presentation/Bubble Anim Set", fileName = "BubbleAnim_")]
    public class BubbleAnimSet : ScriptableObject
    {
        [Header("两套动画（用「添加动画效果」下拉加）")]
        [Label("入场")]
        [SerializeReference]
        public List<MangaAnimEffect> enterEffects = new List<MangaAnimEffect>();

        [Label("退场")]
        [SerializeReference]
        public List<MangaAnimEffect> exitEffects = new List<MangaAnimEffect>();

        [Header("音效")]
        [Label("弹出")]
        public ShowAudioEventSO enterAudio;

        [Label("收起")]
        public ShowAudioEventSO exitAudio;

        [Header("打字机")]
        [Label("速度（秒/字，越小越快）")]
        public float typingSpeed = 0.035f;

        [Label("标点停顿（秒）")]
        public float punctuationPause = 0.14f;

        [Label("标点字符")]
        public string pauseChars = "，。！？…、,.!?;：";
    }
}

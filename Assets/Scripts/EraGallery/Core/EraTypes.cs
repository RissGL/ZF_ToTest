using System;
using UnityEngine;

namespace ZF.EraGallery
{
    /// <summary>
    /// 四个时代。数值就是默认的窗口序号（窗口按这个值从左到右、从上到下排）。
    /// </summary>
    public enum EraId
    {
        Stone = 0,          // 石器时代
        Steam = 1,          // 蒸汽时代
        Electric = 2,       // 电气时代
        Information = 3,    // 信息时代
    }

    /// <summary>相机现在在看哪儿 / 正往哪儿去。</summary>
    public enum EraFocusState
    {
        /// <summary>全景：四个窗口都在画面里。</summary>
        Overview = 0,

        /// <summary>正在推进某个窗口。</summary>
        Entering = 1,

        /// <summary>已经进到某个窗口里了。</summary>
        Focused = 2,

        /// <summary>正在退回全景。</summary>
        Leaving = 3,
    }

    /// <summary>窗口该长什么样。由 EraWorldController 按模型状态算出来，再交给 EraWindow 刷。</summary>
    public enum EraVisualState
    {
        /// <summary>还锁着，进不去。</summary>
        Locked = 0,

        /// <summary>可以进。</summary>
        Available = 1,

        /// <summary>已经通关过，还能再进。</summary>
        Completed = 2,

        /// <summary>现在就在里面。</summary>
        Focused = 3,
    }

    /// <summary>
    /// 时代解锁规则。默认 <see cref="AllOpen"/>：四个时代一开始就都能操作。
    /// 这条链是留给后面的流程的 —— 想改成一关一关解，把 EraWorldController 上的解锁规则换成
    /// <see cref="PreviousCompleted"/> 就行，其余代码不用动。
    /// </summary>
    public enum EraUnlockRule
    {
        /// <summary>四个时代一开始全部可以进（默认）。</summary>
        AllOpen = 0,

        /// <summary>要等前一个时代通关才能进。（留着的接口）</summary>
        PreviousCompleted = 1,
    }

    /// <summary>
    /// 一个时代的展示信息。运行时真正用的是 EraWindow 上序列化的那一份，这里只给搭建菜单当默认值。
    /// </summary>
    [Serializable]
    public struct EraPresentation
    {
        public EraId id;

        /// <summary>时代名（暂时只进日志，等有了中文字体再挂到窗口上）。</summary>
        public string title;

        /// <summary>年代。</summary>
        public string timeline;

        /// <summary>一句话简介，留给后面的谜题/图鉴用。</summary>
        public string summary;

        /// <summary>主题色：窗口内容块的底色。</summary>
        public Color theme;
    }

    /// <summary>四个时代的默认设定，搭场景时抄进 EraWindow。</summary>
    public static class EraCatalog
    {
        public const int Count = 4;

        public static readonly EraPresentation[] All =
        {
            new EraPresentation
            {
                id = EraId.Stone,
                title = "石器时代",
                timeline = "公元前 10000",
                summary = "火种、岩壁，和第一次抬头看星星的人。",
                theme = new Color(0.80f, 0.48f, 0.27f, 1f),
            },
            new EraPresentation
            {
                id = EraId.Steam,
                title = "蒸汽时代",
                timeline = "公元 1780",
                summary = "齿轮咬着齿轮，煤烟把天空染成了铜色。",
                theme = new Color(0.76f, 0.60f, 0.22f, 1f),
            },
            new EraPresentation
            {
                id = EraId.Electric,
                title = "电气时代",
                timeline = "公元 1890",
                summary = "灯丝亮起来的那一夜，黑夜第一次变短了。",
                theme = new Color(0.25f, 0.62f, 0.78f, 1f),
            },
            new EraPresentation
            {
                id = EraId.Information,
                title = "信息时代",
                timeline = "公元 2024",
                summary = "所有人都连在一起，却谁也说不清信号去了哪儿。",
                theme = new Color(0.51f, 0.42f, 0.80f, 1f),
            },
        };

        public static EraPresentation Get(EraId id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].id == id)
                {
                    return All[i];
                }
            }

            return All[0];
        }

        /// <summary>下一个时代（到最后一个绕回第一个）。「这个时代的人搬到下一个时代」用得上。</summary>
        public static EraId Next(EraId id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].id == id)
                {
                    return All[(i + 1) % All.Length].id;
                }
            }

            return All[0].id;
        }
    }

    /// <summary>窗口用到的排序层级（2D 都是同一层，靠 order 决定谁压谁）。</summary>
    public static class EraSortingOrder
    {
        /// <summary>进到这个时代时打开的遮光板，压在所有东西下面。</summary>
        public const int Backdrop = -100;

        /// <summary>内容底色。</summary>
        public const int Content = 0;

        /// <summary>地景 / 时代图案。</summary>
        public const int Decoration = 5;

        /// <summary>边框。</summary>
        public const int Frame = 10;

        /// <summary>通关/锁定标记。</summary>
        public const int Mark = 20;
    }
}

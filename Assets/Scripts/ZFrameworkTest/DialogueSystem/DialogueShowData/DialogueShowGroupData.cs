using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一组对话（= 一个分镜）：组内台词线性播放，出口是「线性下一组」或「选项分叉」。
    ///
    ///   这里只放静态数据。进度（播到第几句）由 DialogueShowModel 持有，
    ///   因为本类的实例是挂在 DialogueChapterSO 资产里的：
    ///   在它身上写运行时状态，改的就是资产本身（Editor 会脏、重进 Play 还在、存档也没法做）。
    /// </summary>
    [Serializable]
    public class DialogueShowGroupData
    {
        [Label("组 id")]
        public string groupId;

        [Label("分镜模板 id")]
        public int templateId;

        /// <summary>
        /// 这一格**离场**（演完要走、换到下一格）时播什么动画 —— 值 = 场景里 DialogueTransitionLibrary 登记表里的 id。
        ///
        /// ★ 语义：字段挂在**本格**上，**离开本格那一刻**播。
        ///   所以"从 A 擦到 B"那一下用的是 **A**（上一格）的字段；
        ///   第一格开场没有"上一格"，不播任何动画；最后一格的值要等章末（章末现在不播）。
        ///
        /// 空 = 本格离场时不播动画（直接切）；填了查不到 → 警告 + 回落默认离场动画（不会卡流程）。
        /// </summary>
        [Label("离场动画 id（空 = 直接切到下一格）")]
        public string transitionId;

        /// <summary>
        /// 离场动画的方向（覆盖擦除组件上配的方向）。
        /// 默认 <see cref="WipeDirection.ComponentDefault"/> = 不覆盖、用组件上的 ——
        /// 这样一套擦除组件就能服务"这一格从左往右、下一格从右往左"，不用配两套。
        /// </summary>
        [Label("离场方向（用组件上的 = 不覆盖）")]
        public WipeDirection transitionDirection = WipeDirection.ComponentDefault;

        /// <summary>
        /// 离场动画的颜色（覆盖擦除组件的纸色 / 墨边色）。
        /// **Alpha = 0 视为没填** = 不覆盖，用组件上的颜色。
        /// </summary>
        [Label("离场颜色（Alpha = 0 = 不覆盖）")]
        public Color transitionColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// 离场动画的时长（覆盖擦除组件上配的时长）。
        /// **0 或负数视为没填** = 不覆盖，用组件上的。不同格子快慢不同也不用配第二套组件。
        /// （条纹那种擦除，这里的"时长"指**每一条刷进来/刷出去的时长**。）
        /// </summary>
        [Label("离场动画时长（秒；0 = 用组件上的）")]
        public float transitionDuration;

        [Label("进场音效")]
        public ShowAudioEventSO enterAudio;

        /// <summary>
        /// 本格默认气泡样式：组内台词没自己填 bubbleStyle 时用它。
        /// 空 = 用气泡实例上的默认样式。想让整格换画风（比如爆点格用夸张气泡）就填这里，不用逐句填。
        /// </summary>
        [Label("本格默认气泡样式（空 = 用气泡自己的默认）")]
        public BubbleStyleSO bubbleStyle;

        /// <summary>
        /// 本格出场角色：**不在这格说话的也算在内** —— 换格时他们就会先分到槽上、站在场上，
        /// 不用等他们开口才登场。空 = 只有说话的人才会登场（老行为）。
        /// </summary>
        [Label("本格出场角色（不说话的也会站在场上）")]
        public List<DialogueSpeaker> cast = new List<DialogueSpeaker>();

        [Label("台词列表")]
        public List<DialogueShowTextItem> groupTexts = new List<DialogueShowTextItem>();

        [Header("出口：choices 非空 → 等玩家选；choices 空 → 走 nextGroupId；nextGroupId 空 → 本章结束")]
        [Label("选项（分叉）")]
        public List<DialogueChoiceData> choices = new List<DialogueChoiceData>();

        [Label("线性下一组 id")]
        public string nextGroupId;

        /// <summary>台词数量（数组为空的兜底，省得每处都判 null）</summary>
        public int TextCount => groupTexts != null ? groupTexts.Count : 0;

        /// <summary>这个组会不会分叉（有选项就是分叉点）</summary>
        public bool HasChoice => choices != null && choices.Count > 0;
    }
}

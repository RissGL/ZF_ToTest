using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 槽导演：负责「这一格谁站在哪个槽」。
    ///
    /// ★ 它和 BubbleStage 的分工（把角色语义从气泡层搬出来）：
    ///   SlotDirector  —— **认识 DialogueSpeaker**：领槽、立绘 / 立绘框 / 背景的分配、说话人高亮、换格清空
    ///   BubbleStage   —— 只认槽（SlotId）+ 文本：气泡的弹出 / 顶掉 / 上限淘汰
    ///   于是"气泡层不认识对话/角色"这条设计底线回来了，角色语义只集中在这一处。
    ///
    /// ★ 它不是 MonoBehaviour：槽列表在模板里由 BubbleStage（舞台）持有，
    ///   这里只拿着那份列表做分配 —— 这样已有的模板 Prefab 不需要迁移序列化数据。
    ///
    /// 分配规则：
    ///   ① 台词填了 slotId → 用指定的（不存在 / 被别人占了 → 警告并退回自动）
    ///   ② 本格内这个角色已占过槽 → 复用（同一格内位置稳定）
    ///   ③ 否则领一个空槽；旁白优先挑"没有框/立绘/背景"的槽（纯旁白槽）
    ///   ④ 组头的「本格出场角色」先按列表顺序占**能站人的槽**（有框或立绘），
    ///      表情用他本格第一句的表情；占不到就警告说谁没站上 ——
    ///      纯旁白槽不算，占了也看不见（老坑："3 个人只上了 2 个"）
    /// 换格时清空占用表（ComposeGroup 会预扫描本组台词、把出场角色先分好槽、立绘就位）。
    /// </summary>
    public class SlotDirector
    {
        private readonly MonoBehaviour m_Owner;
        private readonly List<DialogueSlot> m_Slots;

        private readonly Dictionary<string, DialogueSlot> m_SlotById = new Dictionary<string, DialogueSlot>();
        private readonly Dictionary<DialogueSpeaker, DialogueSlot> m_Assignment =
            new Dictionary<DialogueSpeaker, DialogueSlot>();

        private DialogueSlot m_NarratorSlot;
        private int m_PortraitsEntering;
        private Action m_PortraitsEnteredCallback;

        public SlotDirector(MonoBehaviour owner, List<DialogueSlot> slots)
        {
            m_Owner = owner;
            m_Slots = slots;
        }

        /// <summary>舞台上的槽（只读）</summary>
        public IReadOnlyList<DialogueSlot> Slots => m_Slots;

        /// <summary>老板（日志上下文用）</summary>
        private string OwnerName => m_Owner != null ? m_Owner.name : "?";

        // ===================== 初始化 =====================

        /// <summary>建 id 索引，并把槽里的部件都复位到收起状态（舞台 Init 之后调）</summary>
        public void Init()
        {
            m_SlotById.Clear();
            m_Assignment.Clear();
            m_NarratorSlot = null;
            m_PortraitsEntering = 0;
            m_PortraitsEnteredCallback = null;

            foreach (var slot in m_Slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.Collect();

                string id = slot.SlotId;
                if (m_SlotById.ContainsKey(id))
                {
                    Debug.LogWarning($"[槽] 舞台「{OwnerName}」里有重复的槽 id：\"{id}\"，后一个被忽略");
                    continue;
                }

                m_SlotById.Add(id, slot);

                ResetParts(slot);
            }
        }

        private static void ResetParts(DialogueSlot slot)
        {
            slot.frame?.ResetToEmpty();
            slot.portrait?.ResetToEmpty();
            slot.background?.ResetToEmpty();
        }

        // ===================== 换格 =====================

        /// <summary>
        /// 换格时调：清空占用表，然后把这一组里出场的角色预先分到槽上、立绘就位。
        /// （此时画面被转场盖着，观众看不见）
        /// </summary>
        public void ComposeGroup(DialogueShowGroupData group)
        {
            ClearAssignments();

            if (group == null)
            {
                return;
            }

            var castLog = new List<string>();
            var castSkipped = new List<string>();

            // ① 本格出场角色：不说话的也先站到场上
            //    ★ 只认"能站人的槽"（有框或立绘）—— 纯旁白槽（只有气泡）占了也看不见，
            //      以前就是把这种槽也占了，结果"3 个人只上了 2 个"。
            //    ★ 表情直接用这个人**本格第一句**的表情，避免"先默认脸、第一句再变"的白闪。
            if (group.cast != null)
            {
                foreach (var speaker in group.cast)
                {
                    if (speaker == null)
                    {
                        Debug.LogWarning($"[槽] 舞台「{OwnerName}」的本格出场角色里有空条目，已跳过（检查组头上的列表）");
                        continue;
                    }

                    if (m_Assignment.ContainsKey(speaker))
                    {
                        continue;      // 列表里重复了
                    }

                    var slot = FindFreeCharacterSlot();
                    if (slot == null)
                    {
                        castSkipped.Add(speaker.name);
                        continue;
                    }

                    Assign(speaker, slot, FirstExpressionOf(group, speaker));
                    castLog.Add($"{slot.SlotId}={speaker.name}");
                }
            }

            if (castSkipped.Count > 0)
            {
                Debug.LogWarning($"[槽] 舞台「{OwnerName}」本格出场角色「{string.Join("、", castSkipped)}」" +
                                 $"没抢到能站人的空槽（纯旁白槽不算），这一格他们不会出现。" +
                                 $"槽：{DescribeSlots()}");
            }

            if (group.groupTexts == null)
            {
                return;
            }

            // ② 说话的人（没被 cast 收进来的，这时才登场）
            foreach (var item in group.groupTexts)
            {
                if (item == null || item.speaker == null)
                {
                    continue;      // 旁白没有立绘，等真的说话时再领槽
                }

                if (m_Assignment.ContainsKey(item.speaker))
                {
                    continue;      // 本组已经分过了
                }

                ClaimSlot(item.speaker, item.slotId, item.expression);
            }

            // 每组一行，方便自查"谁站哪儿、谁没站上"
            Debug.Log($"[槽] 舞台「{OwnerName}」本格编排：出场角色 {castLog.Count} 人" +
                      $"（{(castLog.Count == 0 ? "无" : string.Join("、", castLog))}）" +
                      $"，说话人 {m_Assignment.Count} 人，槽：{DescribeSlots()}");
        }

        /// <summary>这个人本格第一次说话用的表情（cast 预分配时用它，省得先摆默认脸再变）</summary>
        private static DialogueCharExpressionEnum FirstExpressionOf(DialogueShowGroupData group, DialogueSpeaker speaker)
        {
            if (group != null && group.groupTexts != null)
            {
                foreach (var item in group.groupTexts)
                {
                    if (item != null && item.speaker == speaker)
                    {
                        return item.expression;
                    }
                }
            }

            return DialogueCharExpressionEnum.defaultFace;
        }

        /// <summary>找一个**能站人**的空槽（有立绘或框）；纯旁白槽不算</summary>
        private DialogueSlot FindFreeCharacterSlot()
        {
            foreach (var slot in m_Slots)
            {
                if (slot == null || IsSlotTaken(slot))
                {
                    continue;
                }

                if (slot.HasPortrait || slot.HasFrame)
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>列出每个槽"能不能站人 / 有没有气泡"，排查用</summary>
        private string DescribeSlots()
        {
            var parts = new List<string>();

            foreach (var slot in m_Slots)
            {
                if (slot == null)
                {
                    continue;
                }

                string sit = slot.HasPortrait || slot.HasFrame ? "可站人" : "纯旁白";
                string bubble = slot.HasBubble ? "有气泡" : "无气泡";
                parts.Add($"{slot.SlotId}({sit}/{bubble})");
            }

            return parts.Count > 0 ? string.Join("、", parts) : "（一个槽都没有）";
        }

        /// <summary>清空占用表，并让所有部件立刻收起（换格时用：画面被盖着，不用播退场）</summary>
        public void ClearAssignments()
        {
            m_Assignment.Clear();
            m_NarratorSlot = null;

            foreach (var slot in m_Slots)
            {
                if (slot != null)
                {
                    ResetParts(slot);
                }
            }
        }

        // ===================== 领槽 =====================

        /// <summary>
        /// 认领一个槽：
        ///   ① 台词填了 slotId → 用指定的；不存在或被别人占了就报警并退回自动分配
        ///   ② 本格内这个角色已占过 → 复用
        ///   ③ 否则领一个空槽（旁白优先挑没有框 / 立绘 / 背景的槽）
        /// </summary>
        public DialogueSlot ClaimSlot(DialogueSpeaker speaker, string explicitSlotId,
            DialogueCharExpressionEnum expression = DialogueCharExpressionEnum.defaultFace)
        {
            DialogueSlot slot = null;

            if (!string.IsNullOrEmpty(explicitSlotId))
            {
                slot = FindSlot(explicitSlotId);

                if (slot == null)
                {
                    Debug.LogWarning($"[槽] 槽 \"{explicitSlotId}\" 在舞台「{OwnerName}」上不存在，改用自动分配。" +
                                     $"舞台有的槽：{SlotIdList()}");
                }
                else if (IsSlotTakenByOther(slot, speaker))
                {
                    Debug.LogWarning($"[槽] 槽 \"{explicitSlotId}\" 本格已经被别人占了，改用自动分配");
                    slot = null;
                }
            }

            if (slot == null && speaker != null &&
                m_Assignment.TryGetValue(speaker, out var assigned) && assigned != null)
            {
                slot = assigned;
            }

            if (slot == null && speaker == null && m_NarratorSlot != null)
            {
                slot = m_NarratorSlot;
            }

            if (slot == null)
            {
                slot = FindFreeSlot(speaker == null);
            }

            if (slot == null)
            {
                Debug.LogWarning($"[槽] 舞台「{OwnerName}」上没有空的槽了（槽：{SlotIdList()}），这一句没有位置可用。" +
                                 "要更多位置就在模板里加槽");
                return null;
            }

            Assign(speaker, slot, expression);
            return slot;
        }

        /// <summary>找槽（按 id）</summary>
        public DialogueSlot FindSlot(string slotId)
        {
            string id = slotId ?? string.Empty;
            return m_SlotById.TryGetValue(id, out var slot) ? slot : null;
        }

        private void Assign(DialogueSpeaker speaker, DialogueSlot slot, DialogueCharExpressionEnum expression)
        {
            if (speaker == null)
            {
                m_NarratorSlot = slot;
            }
            else
            {
                m_Assignment[speaker] = slot;
            }

            // 立绘 / 框 / 背景就位：谁占了这个槽，就是这个槽的那个人
            slot.frame?.SetSpeaker(speaker, expression);
            slot.portrait?.SetSpeaker(speaker, expression);
            slot.background?.SetSpeaker(speaker, expression);
        }

        private bool IsSlotTakenByOther(DialogueSlot slot, DialogueSpeaker speaker)
        {
            if (speaker == null)
            {
                return m_NarratorSlot != null && m_NarratorSlot != slot;
            }

            foreach (var pair in m_Assignment)
            {
                if (pair.Value == slot && pair.Key != speaker)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 找一个空槽：旁白优先"什么都没有"的槽，其他人优先有气泡的槽。
        /// ★ 旁白找不到纯旁白槽时会退回能站人的槽（否则台词没地方弹），但这属于配错了 —— 打个警告说清楚。
        /// </summary>
        private DialogueSlot FindFreeSlot(bool forNarrator)
        {
            DialogueSlot anyFree = null;
            DialogueSlot narratorBorrow = null;

            foreach (var slot in m_Slots)
            {
                if (slot == null || IsSlotTaken(slot))
                {
                    continue;
                }

                if (forNarrator && !slot.HasFrame && !slot.HasPortrait && !slot.HasBackground)
                {
                    return slot;            // 纯旁白位
                }

                if (anyFree == null && slot.HasBubble)
                {
                    anyFree = slot;
                }

                if (forNarrator && narratorBorrow == null && (slot.HasFrame || slot.HasPortrait))
                {
                    narratorBorrow = slot;
                }
            }

            if (forNarrator && narratorBorrow != null)
            {
                Debug.LogWarning($"[槽] 舞台「{OwnerName}」没有「纯旁白槽」（只有气泡、不带框/立绘的槽），" +
                                 $"旁白只能借用能站人的槽「{narratorBorrow.SlotId}」。" +
                                 $"想避免就去模板里留一个纯旁白槽（或别把旁白槽分给角色）");
            }

            if (anyFree != null)
            {
                return anyFree;
            }

            foreach (var slot in m_Slots)
            {
                if (slot != null && !IsSlotTaken(slot))
                {
                    return slot;
                }
            }

            return null;
        }

        private bool IsSlotTaken(DialogueSlot slot)
        {
            if (m_NarratorSlot == slot)
            {
                return true;
            }

            foreach (var pair in m_Assignment)
            {
                if (pair.Value == slot)
                {
                    return true;
                }
            }

            return false;
        }

        // ===================== 立绘 / 高亮 =====================

        /// <summary>
        /// 换格露出后调：让这一格分到角色的部件（框 / 立绘 / 背景）各自播入场动画。
        /// 全部播完（或没有要入场的）时回调 —— DialogueView 拿它当"入场门"的第二个条件。
        /// </summary>
        public void EnterAssignedParts(Action onAllEntered)
        {
            m_PortraitsEntering = 0;

            foreach (var slot in m_Slots)
            {
                if (NeedEnter(slot?.frame)) m_PortraitsEntering++;
                if (NeedEnter(slot?.portrait)) m_PortraitsEntering++;
                if (NeedEnter(slot?.background)) m_PortraitsEntering++;
            }

            if (m_PortraitsEntering == 0)
            {
                onAllEntered?.Invoke();
                return;
            }

            m_PortraitsEnteredCallback = onAllEntered;

            foreach (var slot in m_Slots)
            {
                EnterVisual(slot?.frame);
                EnterVisual(slot?.portrait);
                EnterVisual(slot?.background);
            }
        }

        private static bool NeedEnter(SlotVisual visual)
        {
            return visual != null && visual.Speaker != null;
        }

        private void EnterVisual(SlotVisual visual)
        {
            if (!NeedEnter(visual))
            {
                return;
            }

            visual.Entered -= HandleVisualEntered;
            visual.Entered += HandleVisualEntered;
            visual.Enter();
        }

        private void HandleVisualEntered(SlotVisual visual)
        {
            visual.Entered -= HandleVisualEntered;

            m_PortraitsEntering--;
            if (m_PortraitsEntering > 0)
            {
                return;
            }

            var callback = m_PortraitsEnteredCallback;
            m_PortraitsEnteredCallback = null;
            callback?.Invoke();
        }

        /// <summary>说话人高亮：他占的槽正常显示，其他槽变暗</summary>
        public void SetSpeakerHighlight(DialogueSpeaker speaker)
        {
            foreach (var slot in m_Slots)
            {
                HighlightVisual(slot?.frame, speaker);
                HighlightVisual(slot?.portrait, speaker);
                HighlightVisual(slot?.background, speaker);
            }
        }

        private static void HighlightVisual(SlotVisual visual, DialogueSpeaker speaker)
        {
            if (visual == null || visual.Speaker == null)
            {
                return;
            }

            visual.SetHighlight(speaker != null && visual.Speaker == speaker);
        }

        // ===================== 工具 =====================

        /// <summary>舞台上的槽 id 列表（排查用）</summary>
        public string SlotIdList()
        {
            if (m_Slots.Count == 0)
            {
                return "（一个槽都没有 —— 检查 BubbleStage 的 slots 列表）";
            }

            var ids = new List<string>();
            foreach (var slot in m_Slots)
            {
                if (slot != null)
                {
                    ids.Add($"\"{slot.SlotId}\"");
                }
            }

            return string.Join("、", ids);
        }
    }
}

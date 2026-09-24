using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 气泡舞台：只管**气泡**—— 弹出 / 顶掉（收掉重弹）/ 上限淘汰 / 全部收起。
    ///
    /// ★ 它不认识 DialogueSpeaker，也不碰立绘 / 框 / 背景：
    ///   那些"谁站在哪个槽"的角色语义在 <see cref="SlotDirector"/> 里（通过 <see cref="Director"/> 拿到）。
    ///   于是"气泡层不认识对话 / 角色"这条底线成立，气泡系统也能脱离对话单独用。
    ///
    /// ★ 槽列表仍然放在这里：它是模板 Prefab 上的数据，SlotDirector 只是拿着这份列表做分配
    ///   （所以已有的模板 Prefab 不需要迁移序列化）。
    ///
    /// 兼容：老的「裸气泡列表（bubbles，key 当角色名）」在 Init 里会被自动包成槽
    ///   （槽 id = 气泡 key，没有框 / 立绘 / 背景），老场景不用改。
    /// </summary>
    public class BubbleStage : MonoBehaviour
    {
        [Header("槽")]
        [Label("槽列表（每个槽 = 一个角色位；谁站哪个槽由 SlotDirector 决定）")]
        [SerializeField] private List<DialogueSlot> slots = new List<DialogueSlot>();

        [Header("兼容老结构（气泡直接挂在舞台下、key = 角色名）")]
        [Label("预置气泡（没被槽收走的会被自动包成槽）")]
        [SerializeField] private List<Bubble> bubbles = new List<Bubble>();

        [Label("同时最多显示几个（0 = 不限）")]
        [SerializeField] private int maxVisible = 3;

        [Label("样式兜底（气泡没配默认样式时用）")]
        [SerializeField] private BubbleStyleSO fallbackStyle;

        /// <summary>某个槽的这一句打完了</summary>
        public event Action<string> OnTyped;

        /// <summary>某个槽的气泡收起了</summary>
        public event Action<string> OnHidden;

        private readonly List<DialogueSlot> m_Slots = new List<DialogueSlot>();
        private readonly List<Bubble> m_Active = new List<Bubble>();

        /// <summary>已经提醒过"这个槽没配气泡"的槽 id（每个只提醒一次，别刷屏）</summary>
        private readonly HashSet<string> m_WarnedMissingKeys = new HashSet<string>();

        private bool m_Ready;

        /// <summary>
        /// 槽导演：**认识角色**，负责分槽 / 框 / 立绘 / 背景 / 说话人高亮。
        /// 需要槽的时候从它这里拿（气泡层自己不认识角色）。
        /// </summary>
        public SlotDirector Director { get; private set; }

        /// <summary>有没有气泡正在逐字显示（退场重弹的空档也算，防连点跳句）</summary>
        public bool IsTyping
        {
            get
            {
                for (int i = 0; i < m_Active.Count; i++)
                {
                    if (m_Active[i].IsTyping || m_Active[i].IsTransitioning)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>现在可见几个</summary>
        public int VisibleCount => m_Active.Count;

        /// <summary>舞台上的槽（只读，排查用）</summary>
        public IReadOnlyList<DialogueSlot> SlotList => m_Slots;

        private void Awake()
        {
            Director = new SlotDirector(this, m_Slots);
            Init();
        }

        private void OnDestroy()
        {
            m_Active.Clear();
            m_Slots.Clear();
        }

        // ===================== 初始化 =====================

        /// <summary>收集舞台上的槽（换模板生成出来的舞台也要调一次）</summary>
        public void Init()
        {
            m_Slots.Clear();
            m_Active.Clear();
            m_WarnedMissingKeys.Clear();

            // ① 显式配的槽
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    AddSlot(slot);
                }
            }

            // ② 兼容老结构：没被任何槽收走的气泡，自动包成槽（槽 id = 气泡 key）
            foreach (var bubble in bubbles)
            {
                if (bubble == null || IsInsideSlot(bubble))
                {
                    continue;
                }

                AddSlot(WrapLegacyBubble(bubble));
            }

            m_Ready = true;

            // 保证导演存在（万一 Init 早于 Awake），再建索引 + 把框 / 立绘 / 背景复位
            if (Director == null)
            {
                Director = new SlotDirector(this, m_Slots);
            }

            Director.Init();
        }

        private void AddSlot(DialogueSlot slot)
        {
            slot.Collect();

            string id = slot.SlotId;
            foreach (var exist in m_Slots)
            {
                if (exist != null && exist.SlotId == id)
                {
                    Debug.LogWarning($"[气泡] 舞台「{name}」里有重复的槽 id：\"{id}\"，后一个被忽略");
                    return;
                }
            }

            m_Slots.Add(slot);

            // 开局把槽里的气泡收成藏起来的状态
            if (slot.bubble != null)
            {
                Hook(slot.bubble);
                slot.bubble.gameObject.SetActive(false);
            }
        }

        /// <summary>这个气泡是不是已经在某个槽里面了</summary>
        private static bool IsInsideSlot(Bubble bubble)
        {
            for (Transform t = bubble.transform.parent; t != null; t = t.parent)
            {
                if (t.GetComponent<DialogueSlot>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>老结构兼容：给这个气泡套一个拉伸的槽容器（位置不变），槽 id 用气泡的 key</summary>
        private DialogueSlot WrapLegacyBubble(Bubble bubble)
        {
            var slotGo = new GameObject($"Slot_{bubble.Key}", typeof(RectTransform));
            slotGo.transform.SetParent(transform, false);

            var rect = slotGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var slot = slotGo.AddComponent<DialogueSlot>();
            slot.slotId = bubble.Key;

            // worldPositionStays = false：anchoredPosition 原样保留；槽容器和舞台同尺寸 → 位置不变
            bubble.transform.SetParent(slotGo.transform, false);
            slot.Collect();
            return slot;
        }

        // ===================== 说一句 =====================

        /// <summary>在指定槽里说一句（DialogueView 走这条）</summary>
        public Bubble ShowInSlot(DialogueSlot slot, BubbleRequest request)
        {
            if (slot == null)
            {
                return null;
            }

            var bubble = slot.bubble;

            if (bubble == null)
            {
                // 这个槽没配气泡（纯立绘槽）。旁白之类落到这，多半是模板里槽配少了
                if (m_WarnedMissingKeys.Add(slot.SlotId))
                {
                    Debug.LogWarning($"[气泡] 槽 \"{slot.SlotId}\" 上没有气泡（纯立绘槽？），这一句弹不出来。" +
                                     $"舞台「{name}」的槽：{SlotIdList()}");
                }
                return null;
            }

            if (request.style == null)
            {
                request.style = bubble.defaultStyle != null ? bubble.defaultStyle : fallbackStyle;
            }

            // 谁在说话谁的槽排到最上面（同父节点内排后面的画在前面；不碰 sortingOrder）
            slot.transform.SetAsLastSibling();

            if (bubble.IsVisible)
            {
                bubble.Replay(request);      // 同一个槽连着说：旧气泡先收场，收完重弹
            }
            else
            {
                bubble.Play(request);
                m_Active.Add(bubble);
                TrimExcess(bubble);
            }

            return bubble;
        }

        /// <summary>
        /// 老接口：按"槽 id"说一句（BubbleDemo 这类不经过对话的直接调用走它）。
        /// 找不到同名槽就交给导演自动领一个。
        /// </summary>
        public Bubble Show(BubbleRequest request)
        {
            if (!m_Ready)
            {
                Init();
            }

            string id = request.key ?? string.Empty;
            var slot = Director != null ? Director.FindSlot(id) : FindSlotFallback(id);

            if (slot == null && Director != null)
            {
                slot = Director.ClaimSlot(null, id);
            }

            return slot != null ? ShowInSlot(slot, request) : null;
        }

        private DialogueSlot FindSlotFallback(string id)
        {
            foreach (var slot in m_Slots)
            {
                if (slot != null && slot.SlotId == id)
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>收掉某个槽的气泡</summary>
        public void Hide(string slotId)
        {
            var slot = Director != null ? Director.FindSlot(slotId) : FindSlotFallback(slotId);
            if (slot?.bubble == null)
            {
                return;
            }

            slot.bubble.Close();
            m_Active.Remove(slot.bubble);
        }

        /// <summary>全部收掉（换格 / 本章结束时用）</summary>
        public void HideAll()
        {
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                m_Active[i].Close();
            }
            m_Active.Clear();
        }

        /// <summary>所有正在打字的气泡立刻显示全文（玩家点「继续」）</summary>
        public void CompleteTyping()
        {
            var snapshot = m_Active.ToArray();
            foreach (var bubble in snapshot)
            {
                bubble?.CompleteTyping();
            }
        }

        // ===================== 内部 =====================

        /// <summary>舞台上的槽 id 列表（排查用）</summary>
        private string SlotIdList()
        {
            if (Director != null)
            {
                return Director.SlotIdList();
            }

            if (m_Slots.Count == 0)
            {
                return "（一个槽都没有 —— 检查 BubbleStage 的 slots 列表）";
            }

            var ids = new List<string>();
            foreach (var slot in m_Slots)
            {
                ids.Add($"\"{slot.SlotId}\"");
            }

            return string.Join("、", ids);
        }

        private void Hook(Bubble bubble)
        {
            bubble.Typed -= HandleTyped;
            bubble.Typed += HandleTyped;
            bubble.Closed -= HandleClosed;
            bubble.Closed += HandleClosed;
        }

        private void HandleTyped(Bubble bubble)
        {
            OnTyped?.Invoke(bubble.Key);
        }

        private void HandleClosed(Bubble bubble)
        {
            m_Active.Remove(bubble);
            OnHidden?.Invoke(bubble.Key);
        }

        private void TrimExcess(Bubble current)
        {
            if (maxVisible <= 0)
            {
                return;
            }

            while (m_Active.Count > maxVisible)
            {
                Bubble oldest = null;
                foreach (var bubble in m_Active)
                {
                    if (bubble != current)
                    {
                        oldest = bubble;
                        break;
                    }
                }

                if (oldest == null)
                {
                    break;      // 只剩当前这一个了，超就超着
                }

                oldest.Close();
                m_Active.Remove(oldest);
            }
        }
    }
}

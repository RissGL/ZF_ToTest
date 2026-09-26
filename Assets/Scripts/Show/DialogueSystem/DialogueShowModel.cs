using System.Collections.Generic;
using UnityEngine;
using ZGameFramework;

namespace ZF.DialoguePresentation
{
    public interface IDialogueShowModel : IModel
    {
        /// <summary>当前正在显示的这句（null = 暂时没有内容，比如刚开始或已结束）</summary>
        IReadOnlyBindableProperty<DialogueShowTextItem> CurrentItem { get; }

        /// <summary>当前所在组。它一变 = 该换分镜了（擦除转场 + 重新构图 + 播组音效）</summary>
        IReadOnlyBindableProperty<DialogueShowGroupData> CurrentGroup { get; }

        /// <summary>非 null = 该弹选项等玩家点；null = 正常继续</summary>
        IReadOnlyBindableProperty<List<DialogueChoiceData>> CurrentChoices { get; }

        /// <summary>整章结束了</summary>
        IReadOnlyBindableProperty<bool> IsEnd { get; }

        void StartChapter(DialogueChapterSO chapter);

        /// <summary>玩家点「继续」：下一句 / 出口 / 弹选项</summary>
        void MoveNext();

        /// <summary>玩家点了第 index 个选项：跳到他选的组</summary>
        bool Choose(int index);
    }

    /// <summary>
    /// 对话流程 Model：只维护「当前在哪一组、播到第几句、该不该弹选项」，
    /// 不认识任何 UI / 场景对象。表现层订阅上面四个 BindableProperty 自己更新。
    ///
    /// 分叉规则（就这三条，按顺序判）：
    ///   ① 组内还有下一句          → 显示下一句
    ///   ② 组内播完 + 有选项        → 停在最后一句上弹选项，等 Choose()
    ///   ③ 组内播完 + 没选项        → 进 nextGroupId；nextGroupId 为空 = 本章结束
    /// </summary>
    public class DialogueShowModel : AbstractModel, IDialogueShowModel
    {
        private DialogueChapterSO m_Chapter;

        /// <summary>groupId → 组，避免每次跳转都遍历列表</summary>
        private readonly Dictionary<string, DialogueShowGroupData> m_GroupMap =
            new Dictionary<string, DialogueShowGroupData>();

        private DialogueShowGroupData m_Group;
        private int m_ItemIndex;

        private readonly BindableProperty<DialogueShowTextItem> m_CurrentItem =
            new BindableProperty<DialogueShowTextItem>();

        private readonly BindableProperty<DialogueShowGroupData> m_CurrentGroup =
            new BindableProperty<DialogueShowGroupData>();

        private readonly BindableProperty<List<DialogueChoiceData>> m_CurrentChoices =
            new BindableProperty<List<DialogueChoiceData>>();

        private readonly BindableProperty<bool> m_IsEnd = new BindableProperty<bool>(false);

        public IReadOnlyBindableProperty<DialogueShowTextItem> CurrentItem => m_CurrentItem;
        public IReadOnlyBindableProperty<DialogueShowGroupData> CurrentGroup => m_CurrentGroup;
        public IReadOnlyBindableProperty<List<DialogueChoiceData>> CurrentChoices => m_CurrentChoices;
        public IReadOnlyBindableProperty<bool> IsEnd => m_IsEnd;

        protected override void OnInit()
        {
        }

        // ===================== 对外 =====================

        public void StartChapter(DialogueChapterSO chapter)
        {
            m_Chapter = chapter;
            m_GroupMap.Clear();

            if (chapter != null && chapter.groups != null)
            {
                foreach (var group in chapter.groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(group.groupId))
                    {
                        Debug.LogWarning("[对话] 有组的 groupId 是空的，已跳过（检查节点编辑器导出的数据）");
                        continue;
                    }

                    if (m_GroupMap.ContainsKey(group.groupId))
                    {
                        Debug.LogError($"[对话] 组 id 重复：{group.groupId}，后一个被忽略");
                        continue;
                    }

                    m_GroupMap.Add(group.groupId, group);
                }
            }

            m_Group = null;
            m_ItemIndex = 0;
            m_CurrentItem.SetValueWithoutEvent(null);
            m_CurrentChoices.SetValueWithoutEvent(null);
            m_IsEnd.SetValueWithoutEvent(false);

            EnterGroup(chapter != null ? chapter.startGroupId : null);
        }

        public void MoveNext()
        {
            if (m_IsEnd.Value)
            {
                return;
            }

            // 正在等玩家选：忽略「继续」，防误触把剧情推进过去
            if (m_CurrentChoices.Value != null)
            {
                return;
            }

            if (m_Group == null)
            {
                EndChapter();
                return;
            }

            // ① 组内还有下一句
            if (m_ItemIndex + 1 < m_Group.TextCount)
            {
                ShowItem(m_ItemIndex + 1);
                return;
            }

            // ② 组内播完 + 有选项 → 停在最后一句上把选项弹出来
            if (m_Group.HasChoice)
            {
                m_CurrentChoices.Value = m_Group.choices;
                return;
            }

            // ③ 线性继续（nextGroupId 为空 → EnterGroup 里会走结束分支）
            EnterGroup(m_Group.nextGroupId);
        }

        public bool Choose(int index)
        {
            var choices = m_CurrentChoices.Value;

            if (choices == null)
            {
                Debug.LogWarning("[对话] 当前没有可选的选项");
                return false;
            }

            if (index < 0 || index >= choices.Count)
            {
                Debug.LogWarning($"[对话] 选项下标越界：{index}（共 {choices.Count} 个）");
                return false;
            }

            string targetGroupId = choices[index].targetGroupId;

            // ★ 先收起选项面板再进组：否则表现层会在「还显示着旧选项」的状态下收到换组事件
            m_CurrentChoices.Value = null;
            EnterGroup(targetGroupId);
            return true;
        }

        // ===================== 内部 =====================

        /// <summary>跳到某个组并从第一句开始播（groupId 为空 = 正常结束本章）</summary>
        private void EnterGroup(string groupId, int depth = 0)
        {
            // 空组会直接跳过继续跳，这里挡一下"组之间成环"导致的无限递归
            if (depth > 64)
            {
                Debug.LogError("[对话] 组跳转层数过深，组之间可能成环了，强制结束本章");
                EndChapter();
                return;
            }

            if (string.IsNullOrEmpty(groupId))
            {
                EndChapter();
                return;
            }

            if (!m_GroupMap.TryGetValue(groupId, out var group) || group == null)
            {
                Debug.LogError($"[对话] 找不到组：{groupId}（检查组 id 拼写 / 是否导出过）");
                EndChapter();
                return;
            }

            m_Group = group;
            m_ItemIndex = 0;
            m_CurrentChoices.Value = null;

            // ★ 表现层收到这个才开始擦除转场 + 重新构图 + 播组音效（enterAudio）
            m_CurrentGroup.Value = group;

            // 空组：没有台词就直接往下跳（但如果这个组带着选项，就该弹选项）
            if (group.TextCount == 0)
            {
                if (group.HasChoice)
                {
                    m_CurrentChoices.Value = group.choices;
                    return;
                }

                EnterGroup(group.nextGroupId, depth + 1);
                return;
            }

            ShowItem(0);
        }

        private void ShowItem(int index)
        {
            m_ItemIndex = index;
            m_CurrentItem.Value = m_Group.groupTexts[index];
        }

        private void EndChapter()
        {
            m_Group = null;
            m_ItemIndex = 0;
            m_CurrentItem.Value = null;
            m_CurrentChoices.Value = null;
            m_IsEnd.Value = true;
        }
    }
}

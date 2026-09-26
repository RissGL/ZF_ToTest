using System.Collections.Generic;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 模板自检：把一个模板（Prefab 或运行时实例）从头到尾看一遍，把"美术/策划容易配错、
    /// 但运行时只会表现为『某个角色不显示』『台词没地方弹』"的问题提前说出来。
    ///
    /// 什么时候跑：
    ///   - 运行时：`DialogueTemplateHost` 实例化模板后、`DialogueView` 指向布局时各跑一次（同一个物体只跑一次）
    ///   - 编辑器：菜单 `Tools/对话系统/检查所有模板（槽 / 部件）`
    ///
    /// 检查内容：
    ///   槽：有没有、slotId 重不重复、有没有气泡（没气泡的槽永远说不了话）
    ///   接线：DialogueLayoutRefs 在不在、气泡舞台 / 选项面板有没有接
    /// </summary>
    public static class DialogueTemplateValidator
    {
        /// <summary>已经报过的模板（同一个物体只报一次，免得每次切模板刷屏）</summary>
        private static readonly HashSet<int> s_Validated = new HashSet<int>();

        /// <summary>检查一个模板物体；errors 非空 = 这个模板跑不起来，warnings = 能跑但多半配错了</summary>
        public static void Validate(GameObject root, List<string> errors, List<string> warnings)
        {
            if (root == null)
            {
                errors?.Add("模板是空的（null）");
                return;
            }

            string label = root.name;

            // ---- 接线 ----
            var layout = root.GetComponent<DialogueLayoutRefs>();
            if (layout == null)
            {
                errors?.Add($"模板「{label}」根上没挂 DialogueLayoutRefs，换模板时接不上气泡舞台 / 选项面板");
            }
            else
            {
                if (layout.bubbleStage == null)
                {
                    errors?.Add($"模板「{label}」的 DialogueLayoutRefs 没接气泡舞台，台词没有地方弹");
                }

                if (layout.choicePanel == null)
                {
                    warnings?.Add($"模板「{label}」没有选项面板：这一格要是有选项就弹不出来");
                }
            }

            // ---- 槽 ----
            var slots = root.GetComponentsInChildren<DialogueSlot>(true);
            if (slots.Length == 0)
            {
                errors?.Add($"模板「{label}」里一个槽（DialogueSlot）都没有，台词没有位置可弹");
                return;
            }

            var seen = new Dictionary<string, DialogueSlot>();
            bool hasUsableBubble = false;

            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                // 部件没收集过就收一遍（纯 Prefab 检查路径下 Awake 不会跑）
                slot.Collect();

                string id = slot.SlotId;

                if (string.IsNullOrEmpty(slot.slotId))
                {
                    warnings?.Add($"模板「{label}」的槽「{slot.name}」没填 slotId，现在拿物体名「{id}」当 id 用" +
                                  "（改物体名会让数据里的 slotId 对不上）");
                }

                if (seen.TryGetValue(id, out var first))
                {
                    errors?.Add($"模板「{label}」里有重复槽 id「{id}」（{first.name} / {slot.name}）：" +
                                "数据里的 slotId 会指到其中一个，另一个永远轮不到");
                }
                else
                {
                    seen.Add(id, slot);
                }

                if (!slot.HasBubble)
                {
                    warnings?.Add($"模板「{label}」的槽「{id}」没有气泡，永远不会说话（纯立绘位）");
                }
                else
                {
                    hasUsableBubble = true;
                }
            }

            if (!hasUsableBubble)
            {
                errors?.Add($"模板「{label}」里所有槽都没有气泡，台词一句都弹不出来");
            }
        }

        /// <summary>检查 + 打日志（同一个物体只打一次；切模板会反复调用这里）</summary>
        public static bool ValidateAndLog(GameObject root, string label = null)
        {
            if (root == null)
            {
                return false;
            }

            if (!s_Validated.Add(root.GetInstanceID()))
            {
                return true;        // 已经检查过
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(root, errors, warnings);

            string who = string.IsNullOrEmpty(label) ? root.name : label;

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log($"[对话模板] 「{who}」自检通过", root);
                return true;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"[对话模板] {error}", root);
            }
            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[对话模板] {warning}", root);
            }

            return errors.Count == 0;
        }

        /// <summary>清掉"已检查过"的记录（编辑器重进 Play 用）</summary>
        public static void ResetCache()
        {
            s_Validated.Clear();
        }
    }
}

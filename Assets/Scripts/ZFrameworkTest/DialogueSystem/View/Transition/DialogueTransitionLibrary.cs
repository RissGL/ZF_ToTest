using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>登记表里的一条：id → 离场动画实现</summary>
    [Serializable]
    public class DialogueTransitionEntry
    {
        [Label("id（组头的「离场动画 id」填它，比如 wipe / bars_in / manga_dissolve）")]
        public string id;

        [Label("离场动画实现（要实现 IDialogueTransition 的组件）")]
        public MonoBehaviour source;
    }

    /// <summary>
    /// 离场动画登记表：把场景里配好的几种擦除按 id 登记起来，让**数据**决定这一格离场时用哪种。
    ///
    /// 为什么要它：擦除的种类是"这一格要走时怎么翻过去"的表现选择，不该写死在代码或单个组件上。
    /// 组头的「离场动画 id」填 `wipe` → 这里查到对应的实现 → 播它；
    /// **组头留空 = 这一格离场时直接切（不播动画）**。方向和颜色也能由组数据传（见 IDialogueTransitionStyle）。
    /// 和 `templateId → 模板 Prefab` 一个套路，策划不用碰代码。
    ///
    /// 同一个组件可以登记多个 id（比如同一套擦除挂 `wipe` 和 `wipe_lr` 两个名字）。
    /// </summary>
    public class DialogueTransitionLibrary : MonoBehaviour
    {
        [Label("登记表（id → 擦除实现）")]
        [SerializeField] private List<DialogueTransitionEntry> entries = new List<DialogueTransitionEntry>();

        /// <summary>按 id 找擦除（找不到返回 null + 警告；调用方自己决定回落到哪个）</summary>
        public IDialogueTransition Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry == null || entry.id != id)
                {
                    continue;
                }

                if (entry.source == null)
                {
                    Debug.LogWarning($"[对话擦除] 登记表里「{id}」没挂实现组件", this);
                    return null;
                }

                if (entry.source is IDialogueTransition transition)
                {
                    return transition;
                }

                Debug.LogError($"[对话擦除] 登记表里「{id}」挂的「{entry.source.GetType().Name}」" +
                               "没有实现 IDialogueTransition", this);
                return null;
            }

            return null;
        }

        /// <summary>列出所有 id（报错信息用）</summary>
        public string IdList()
        {
            if (entries == null || entries.Count == 0)
            {
                return "（登记表是空的）";
            }

            var ids = new List<string>();
            foreach (var entry in entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.id))
                {
                    ids.Add(entry.id);
                }
            }

            return ids.Count > 0 ? string.Join("、", ids) : "（登记表是空的）";
        }

        private void Awake()
        {
            Validate();
        }

        /// <summary>校验：重复 id / 空 id / 没实现接口（错在配置上，早说早好）</summary>
        public void Validate()
        {
            if (entries == null)
            {
                return;
            }

            var seen = new HashSet<string>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogWarning($"[对话擦除] 登记表里有一条没填 id（{entry.source?.name}），它永远不会被用到", this);
                    continue;
                }

                if (!seen.Add(entry.id))
                {
                    Debug.LogWarning($"[对话擦除] 登记表里 id「{entry.id}」重复了，用第一条", this);
                }

                if (entry.source != null && !(entry.source is IDialogueTransition))
                {
                    Debug.LogError($"[对话擦除] 「{entry.id}」挂的「{entry.source.GetType().Name}」" +
                                   "没有实现 IDialogueTransition", this);
                }
            }
        }
    }
}

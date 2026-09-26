using System;
using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 模板表：templateId → 模板 Prefab。
    /// 对话数据里每个组有一个 templateId，换组时按它来这里找模板。
    ///
    /// 创建：右键 Create → Dialogue → Dialogue Template Table
    /// 校验：重复 id / 空 prefab / 同一个 prefab 挂多个 id 都会报出来（DialogueTemplateHost 激活时也会校验一次）
    /// </summary>
    [CreateAssetMenu(menuName = "Dialogue/Dialogue Template Table", fileName = "DialogueTemplateTable")]
    public class DialogueTemplateTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Label("模板 id")]
            public int templateId;

            [Label("模板 Prefab（根上要挂 DialogueLayoutRefs）")]
            public DialogueLayoutRefs prefab;
        }

        [Label("模板列表")]
        public List<Entry> entries = new List<Entry>();

        /// <summary>按 id 找模板（找不到返回 null）</summary>
        public DialogueLayoutRefs Get(int templateId)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.templateId == templateId && entry.prefab != null)
                {
                    return entry.prefab;
                }
            }

            return null;
        }

        /// <summary>表里有没有配置（空表 = 没接模板系统，用场景里那一份）</summary>
        public bool IsEmpty => entries == null || entries.Count == 0;

        /// <summary>列出所有 id（报错信息用）</summary>
        public string IdList()
        {
            if (IsEmpty)
            {
                return "（表是空的）";
            }

            var ids = new List<string>();
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    ids.Add(entry.templateId.ToString());
                }
            }

            return string.Join("、", ids);
        }

        /// <summary>校验：重复 id / 空 prefab（errors 非空说明这张表不能用）</summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            if (IsEmpty)
            {
                warnings?.Add("模板表是空的：换组时不会换模板，只用场景里那一份布局");
                return;
            }

            var seen = new Dictionary<int, DialogueLayoutRefs>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.prefab == null)
                {
                    errors?.Add($"模板表里 templateId={entry.templateId} 没填 Prefab");
                    continue;
                }

                if (seen.ContainsKey(entry.templateId))
                {
                    errors?.Add($"模板表里 templateId={entry.templateId} 重复了（组 id 一样会不知道用哪个）");
                    continue;
                }

                seen.Add(entry.templateId, entry.prefab);

                if (entry.prefab.GetComponent<DialogueLayoutRefs>() == null)
                {
                    errors?.Add($"模板「{entry.prefab.name}」根上没挂 DialogueLayoutRefs");
                }
            }
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(errors, warnings);

            foreach (var error in errors)
            {
                Debug.LogError($"[对话模板表] {name}：{error}", this);
            }
        }
    }
}

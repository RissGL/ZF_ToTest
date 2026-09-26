using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 模板自检的编辑器入口。
    ///
    /// 和运行时那套（<see cref="DialogueTemplateValidator"/>）是同一份检查逻辑：
    ///   运行时在切模板时报错（只看当前用到的那些）
    ///   这里在**不改场景、不进 Play** 的情况下把工程里所有模板一次性查完（美术交图前跑一遍最省事）
    /// </summary>
    public static class DialogueTemplateChecker
    {
        [MenuItem("Tools/对话系统/检查所有模板（槽 / 部件）")]
        private static void CheckAllTemplates()
        {
            var checkedRoots = new HashSet<int>();
            int checkedCount = 0;
            int errorCount = 0;
            int warningCount = 0;

            // ① 模板表里的 Prefab
            foreach (var table in FindAssets<DialogueTemplateTable>())
            {
                if (table.entries == null)
                {
                    continue;
                }

                foreach (var entry in table.entries)
                {
                    if (entry == null || entry.prefab == null)
                    {
                        continue;
                    }

                    CheckOne(entry.prefab.gameObject, $"{table.name} → templateId={entry.templateId}",
                        checkedRoots, ref checkedCount, ref errorCount, ref warningCount);
                }
            }

            // ② Templates 文件夹下的所有 Prefab（还没进表的新模板也要能查）
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Templates" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(path));
                if (prefab == null || prefab.GetComponentInChildren<DialogueSlot>(true) == null)
                {
                    continue;
                }

                CheckOne(prefab, "（文件夹）", checkedRoots, ref checkedCount, ref errorCount, ref warningCount);
            }

            if (checkedCount == 0)
            {
                Debug.LogWarning("[对话模板] 没找到任何带槽的模板 Prefab —— 先跑一次「生成默认模板 Prefab」或确认路径");
                return;
            }

            string summary = $"[对话模板] 自检完毕：检查了 {checkedCount} 个模板，{errorCount} 个错误，{warningCount} 个警告";

            if (errorCount > 0)
            {
                Debug.LogError(summary);
            }
            else if (warningCount > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        private static void CheckOne(GameObject root, string from, HashSet<int> checkedRoots,
            ref int checkedCount, ref int errorCount, ref int warningCount)
        {
            if (root == null || !checkedRoots.Add(root.GetInstanceID()))
            {
                return;
            }

            checkedCount++;

            var errors = new List<string>();
            var warnings = new List<string>();
            DialogueTemplateValidator.Validate(root, errors, warnings);

            foreach (var error in errors)
            {
                Debug.LogError($"[对话模板] {from} {error}", root);
            }
            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[对话模板] {from} {warning}", root);
            }

            errorCount += errors.Count;
            warningCount += warnings.Count;
        }

        private static IEnumerable<T> FindAssets<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }
}

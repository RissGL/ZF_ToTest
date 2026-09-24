using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 诊断脚本（临时用）：在命令行里跑一次「建图 → 连线 → 保存 → 重载」的全过程，
    /// 看连线到底卡在哪一步。跑法：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;工程&gt; \
    ///             -executeMethod ZF.DialoguePresentation.DialogueGraphDiagnostics.Run -logFile &lt;日志&gt;
    /// </summary>
    public static class DialogueGraphDiagnostics
    {
        public static void Run()
        {
            try
            {
                RunInner();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[诊断] 抛异常：" + e);
            }

            EditorApplication.Exit(0);
        }

        private static void RunInner()
        {
            var view = new DialogueGraphView();

            var header = view.CreateGroupNode(new Vector2(0f, 0f));
            var line1 = view.CreateDialogueNode(new Vector2(320f, 0f));
            var line2 = view.CreateDialogueNode(new Vector2(680f, 0f));

            // 手工造两条线：组头 out → 第一句 → 第二句
            var edge1 = header.OutputPort.ConnectTo(line1.InputPort);
            view.AddElement(edge1);
            var edge2 = line1.OutputPort.ConnectTo(line2.InputPort);
            view.AddElement(edge2);

            Debug.Log($"[诊断] header.GUID=\"{header.GUID}\"");
            Debug.Log($"[诊断] line1.GUID=\"{line1.GUID}\"");
            Debug.Log($"[诊断] edges 属性 = {Count(view.edges)} 条");
            Debug.Log($"[诊断] graphElements 属性 = {Count(view.graphElements)} 个");
            Debug.Log($"[诊断] nodes 属性 = {Count(view.nodes)} 个");
            Debug.Log($"[诊断] GetCompatiblePorts(header.InputPort) → {view.GetCompatiblePorts(header.InputPort, null).Count} 个可用端口");
            Debug.Log($"[诊断] GetCompatiblePorts(line1.InputPort) → {view.GetCompatiblePorts(line1.InputPort, null).Count} 个可用端口");

            // ---- 保存 ----
            var data = ScriptableObject.CreateInstance<DialogueGraphData>();
            view.CollectInto(data);
            Debug.Log($"[诊断] 保存结果：nodes={data.nodes.Count} links={data.links.Count} boxes={data.groupBoxes.Count}");

            foreach (var node in data.nodes)
            {
                Debug.Log($"[诊断]   node kind={node.kind} guid=\"{node.guid}\" groupTitle=\"{node.groupTitle}\"");
            }

            foreach (var link in data.links)
            {
                Debug.Log($"[诊断]   link from=\"{link.fromGuid}\" to=\"{link.toGuid}\" port=\"{link.fromPortName}\"");
            }

            // ---- 重载 ----
            var view2 = new DialogueGraphView();
            view2.LoadFromData(data);
            Debug.Log($"[诊断] 重载后 edges 属性 = {Count(view2.edges)} 条");
            Debug.Log($"[诊断] 重载后 graphElements 属性 = {Count(view2.graphElements)} 个");

            // ---- 再存一遍，看空 guid 会不会被继承下去 ----
            var data2 = ScriptableObject.CreateInstance<DialogueGraphData>();
            view2.CollectInto(data2);
            Debug.Log($"[诊断] 二次保存：nodes={data2.nodes.Count} links={data2.links.Count}");
            foreach (var link in data2.links)
            {
                Debug.Log($"[诊断]   link from=\"{link.fromGuid}\" to=\"{link.toGuid}\" port=\"{link.fromPortName}\"");
            }

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(data2);
        }

        private static int Count<T>(IEnumerable<T> items)
        {
            int count = 0;
            foreach (var _ in items)
            {
                count++;
            }
            return count;
        }
    }
}

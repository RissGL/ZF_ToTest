using System.Collections.Generic;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一条校验结果。
    /// 故意支持从 string 隐式转换：校验代码里照旧写 errors.Add("...") 就行，
    /// 需要"点一下跳过去"的地方再显式填 NodeGuid。
    /// </summary>
    public class GraphIssue
    {
        public bool IsError;
        public string Message;

        /// <summary>能定位到节点时填上：面板里点这条会选中并聚焦该节点</summary>
        public string NodeGuid;

        public static implicit operator GraphIssue(string message)
        {
            return new GraphIssue { Message = message };
        }

        public override string ToString()
        {
            return (IsError ? "[错误] " : "[警告] ") + Message;
        }
    }

    /// <summary>导出预览里的一组（= 一个分组框 = 一个分镜）</summary>
    public class GroupPreviewItem
    {
        public string GroupId;
        public bool IsStart;
        public string HeaderGuid;
        public int TextCount;
        public string FirstLine;
        public string NextGroupId;

        /// <summary>形如「相信他 → Group_2」，面板里直接显示</summary>
        public readonly List<string> ChoiceLines = new List<string>();
    }

    /// <summary>检查 / 导出结果：面板显示用它，导出流程也把它当出参</summary>
    public class GraphCheckReport
    {
        public readonly List<GraphIssue> Issues = new List<GraphIssue>();
        public readonly List<GroupPreviewItem> Groups = new List<GroupPreviewItem>();

        public string StartGroupId;

        /// <summary>没有错误（警告不算失败）</summary>
        public bool IsSuccess;

        public int ErrorCount
        {
            get
            {
                int count = 0;
                foreach (var issue in Issues)
                {
                    if (issue.IsError)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int WarningCount => Issues.Count - ErrorCount;
        public bool HasErrors => ErrorCount > 0;

        public int TotalChoiceCount
        {
            get
            {
                int count = 0;
                foreach (var group in Groups)
                {
                    count += group.ChoiceLines.Count;
                }
                return count;
            }
        }
    }
}

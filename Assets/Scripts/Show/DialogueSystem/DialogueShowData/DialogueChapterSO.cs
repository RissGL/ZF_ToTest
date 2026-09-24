using System.Collections.Generic;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    [CreateAssetMenu(menuName = "Dialogue/DialogueChapter", fileName = "Chapter_01")]
    public class DialogueChapterSO : ScriptableObject
    {
        [Label("章节 id")]
        public string chapterId;

        [Label("起始组 id")]
        public string startGroupId;

        [Label("组列表（按执行顺序）")]
        public List<DialogueShowGroupData> groups = new List<DialogueShowGroupData>();
    }
}
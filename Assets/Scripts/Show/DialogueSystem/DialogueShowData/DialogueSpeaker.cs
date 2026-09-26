using UnityEngine;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 对话角色（SO）：名字 / id / 各种表情立绘。
    /// 取表情统一走 GetFace()：没配置的表情会自动回退到默认表情。
    ///
    /// ★ 表情分两类（见 DialogueCharExpressionEnum）：
    ///   上半段是常规情绪（平静 / 高兴 / 难过…），下半段是**漫画夸张表情**
    ///   （惊愕 / 黑化 / 冒汗 / 闪亮 / Q 版…），后者用来做"突然切一格不同画风"的爆点，
    ///   通常配合换模板（groupId 的 templateId）或换气泡样式一起用。
    ///
    /// ★ 没配的图会自动回退到「平静」，所以可以先只填默认表情跑通流程，再慢慢补图。
    /// </summary>
    [CreateAssetMenu(menuName = "Dialogue/DialogueSpeaker")]
    public class DialogueSpeaker : ScriptableObject
    {
        [Header("身份")]
        [Label("名字（气泡上的显示名；不再当气泡 key 用，位置由槽决定）")]
        public string name;

        [Label("id")]
        public int id;

        // ===================== 常规情绪 =====================

        [Header("常规情绪")]
        [Label("平静（默认：其它表情没配时都回退到它）")]
        [SerializeField] internal Sprite defaultFace;

        [Label("高兴")]
        [SerializeField] internal Sprite happyFace;

        [Label("难过")]
        [SerializeField] internal Sprite sadFace;

        [Label("生气")]
        [SerializeField] internal Sprite angryFace;

        [Label("惊讶")]
        [SerializeField] internal Sprite surpriseFace;

        [Label("害怕")]
        [SerializeField] internal Sprite fearFace;

        [Label("害羞 / 脸红")]
        [SerializeField] internal Sprite shyFace;

        [Label("为难 / 苦恼")]
        [SerializeField] internal Sprite worryFace;

        [Label("思考")]
        [SerializeField] internal Sprite thinkFace;

        [Label("认真（下决心的脸）")]
        [SerializeField] internal Sprite seriousFace;

        // ===================== 漫画夸张表情 =====================

        [Header("漫画夸张表情（爆点 / 突然切一格画风时用，建议配合换模板或换气泡样式）")]
        [Label("惊愕（冲击线 / 屏幕裂开）")]
        [SerializeField] internal Sprite shockFace;

        [Label("黑化（脸上一半阴影）")]
        [SerializeField] internal Sprite darkFace;

        [Label("冒汗（无语 / 尴尬）")]
        [SerializeField] internal Sprite sweatFace;

        [Label("闪亮（星星眼 / 期待）")]
        [SerializeField] internal Sprite sparkleFace;

        [Label("眩晕（混乱 / 转圈）")]
        [SerializeField] internal Sprite dizzyFace;

        [Label("慌乱（惨叫 / 白目）")]
        [SerializeField] internal Sprite panicFace;

        [Label("暴怒（怒符 / 爆青筋）")]
        [SerializeField] internal Sprite rageFace;

        [Label("漫画夸张（Q 版 / 变形）")]
        [SerializeField] internal Sprite comicFace;

        // ===================== 对外 get =====================

        /// <summary>
        /// 按表情枚举取立绘：该表情没配（引用为空）→ 返回默认表情；默认表情也没配 → 返回 null。
        /// </summary>
        public Sprite GetFace(DialogueCharExpressionEnum expression)
        {
            Sprite face = Pick(expression);
            return face != null ? face : defaultFace;
        }

        /// <summary>默认表情（兜底用）</summary>
        public Sprite GetDefaultFace() => defaultFace;

        /// <summary>该表情是否有独立配置（没配就是回退默认）</summary>
        public bool HasFace(DialogueCharExpressionEnum expression) => Pick(expression) != null;

        /// <summary>表情 → 图（不兜底）；映射表在 <see cref="DialogueExpressionTable"/> 里，加表情不用改这里</summary>
        private Sprite Pick(DialogueCharExpressionEnum expression)
        {
            return DialogueExpressionTable.GetFace(this, expression);
        }
    }
}

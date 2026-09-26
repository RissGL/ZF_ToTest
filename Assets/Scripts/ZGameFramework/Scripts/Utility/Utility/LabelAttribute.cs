using UnityEngine;

namespace ZGameFramework.Utility
{
    /// <summary>
    /// Inspector 里给字段显示一个自定义标签。
    ///
    /// lines &gt; 1 时按多行文本画（标签在上、输入框在下）——
    /// **不要**再配 [TextArea]：一个字段只能有一个 PropertyDrawer，
    /// 这个绘制器会把 TextArea 的顶掉，高度按一行算、内容按多行画，结果和下一个字段叠在一起。
    /// </summary>
    public class LabelAttribute : PropertyAttribute
    {
        public string name;

        /// <summary>要几行。1 = 普通单行字段。</summary>
        public int lines;

        public LabelAttribute(string name, int lines = 1)
        {
            this.name = name;
            this.lines = Mathf.Max(1, lines);
        }
    }
}

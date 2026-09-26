using UnityEditor;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZGameFramework.EditorTools
{
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public sealed class LabelAttributeDrawer : PropertyDrawer
    {
        private const float LineGap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            LabelAttribute attribute = this.attribute as LabelAttribute;

            // 多行文本自己画。
            // 不能靠 [TextArea]：一个字段只能有一个 PropertyDrawer，这个绘制器会把它顶掉 ——
            // 于是高度按一行算、PropertyField 却按多行画，溢出到下一个字段上（标签和文字叠在一起）。
            if (IsMultiline(attribute, property))
            {
                float lineHeight = EditorGUIUtility.singleLineHeight;

                Rect labelRect = new Rect(position.x, position.y, position.width, lineHeight);
                Rect fieldRect = new Rect(position.x, position.y + lineHeight + LineGap, position.width,
                    lineHeight * attribute.lines);

                EditorGUI.LabelField(labelRect, BuildLabel(label));

                EditorGUI.BeginChangeCheck();
                string text = EditorGUI.TextArea(fieldRect, property.stringValue);

                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = text;
                }

                return;
            }

            EditorGUI.PropertyField(position, property, BuildLabel(label), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            LabelAttribute attribute = this.attribute as LabelAttribute;

            if (IsMultiline(attribute, property))
            {
                // 标签一行 + 输入框 lines 行
                return EditorGUIUtility.singleLineHeight * (attribute.lines + 1) + LineGap;
            }

            return EditorGUI.GetPropertyHeight(property, BuildLabel(label), true);
        }

        private static bool IsMultiline(LabelAttribute attribute, SerializedProperty property)
        {
            return attribute != null
                   && attribute.lines > 1
                   && property.propertyType == SerializedPropertyType.String;
        }

        private GUIContent BuildLabel(GUIContent fallback)
        {
            var attribute = this.attribute as LabelAttribute;

            if (attribute == null || string.IsNullOrEmpty(attribute.name))
            {
                return fallback;
            }

            return new GUIContent(attribute.name, fallback != null ? fallback.tooltip : null);
        }
    }
}

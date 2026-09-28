using UnityEditor;
using UnityEngine;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 让 DialogueGraphData.NodeData 在 Inspector 里一眼能认出来是哪个节点。
    ///
    /// 默认情况下，SO 的 Nodes 列表里每个元素只有一串 guid（看着就是一坨乱码），
    /// 这里把保存时写下的 displayName（「组 id / 角色名：台词…」）画在每个元素的头部，
    /// 名称里还带着组 id，所以"这个节点属于哪一组"在资产里也是看得见的。
    /// </summary>
    [CustomPropertyDrawer(typeof(DialogueGraphData.NodeData))]
    public class DialogueNodeDataDrawer : PropertyDrawer
    {
        private const string NameField = "displayName";
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // ---- 头部：Element N ｜ 节点名 ----
            var nameProperty = property.FindPropertyRelative(NameField);
            string shown = nameProperty != null && !string.IsNullOrEmpty(nameProperty.stringValue)
                ? nameProperty.stringValue
                : "（还没名字：保存一次图就会写上）";

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(headerRect, label.text, shown);

            // ---- 其余字段照默认画（displayName 已经在头部显示过，跳过）----
            float y = headerRect.yMax + Gap;
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name == NameField)
                {
                    continue;
                }

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), iterator, true);
                y += height + Gap;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + Gap;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name == NameField)
                {
                    continue;
                }

                height += EditorGUI.GetPropertyHeight(iterator, true) + Gap;
            }

            return height;
        }
    }
}

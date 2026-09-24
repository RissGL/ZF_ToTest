using UnityEditor;
using UnityEngine;
using ZGameFramework.Utility;

namespace ZGameFramework.EditorTools
{
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public sealed class LabelAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, BuildLabel(label), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, BuildLabel(label), true);
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

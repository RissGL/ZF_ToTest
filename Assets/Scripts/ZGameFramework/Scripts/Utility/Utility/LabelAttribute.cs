using UnityEngine;

namespace ZGameFramework.Utility
{
    public class LabelAttribute : PropertyAttribute
    {
        public string name;
        public LabelAttribute(string name)
        {
            this.name = name;
        }
    }
}
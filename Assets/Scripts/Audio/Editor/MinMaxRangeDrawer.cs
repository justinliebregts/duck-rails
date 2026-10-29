using UnityEditor;
using UnityEngine;

namespace Game.Audio.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    public sealed class MinMaxRangeDrawer : PropertyDrawer
    {
        const float FieldWidth = 48f;
        const float Gap = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.LabelField(position, label.text, "Use MinMaxRange with Vector2.");
                return;
            }
            var range = (MinMaxRangeAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var value = property.vector2Value;
            float min = Mathf.Min(value.x, value.y), max = Mathf.Max(value.x, value.y);
            var minRect = new Rect(position.x, position.y, FieldWidth, position.height);
            var maxRect = new Rect(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            var sliderRect = new Rect(minRect.xMax + Gap, position.y, maxRect.x - minRect.xMax - Gap * 2, position.height);

            EditorGUI.BeginChangeCheck();
            min = EditorGUI.FloatField(minRect, min);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, range.Min, range.Max);
            max = EditorGUI.FloatField(maxRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                min = Mathf.Clamp(min, range.Min, range.Max);
                max = Mathf.Clamp(max, min, range.Max);
                property.vector2Value = new Vector2((float)System.Math.Round(min, 3), (float)System.Math.Round(max, 3));
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}

using Herghys.GameObjectScriptAssigner.Attribute;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.EditorScripts.Drawer
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
                return EditorGUI.GetPropertyHeight(property, label, true);
            else
                return -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
                EditorGUI.PropertyField(position, property, label, true);
        }

        private bool ShouldShow(SerializedProperty property)
        {
            ShowIfAttribute showIf = (ShowIfAttribute)attribute;

            object targetObject = GetTargetObject(property);
            if (targetObject == null)
                return true;

            FieldInfo field = targetObject.GetType().GetField(showIf.ConditionName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                return true;

            object value = field.GetValue(targetObject);
            if (value is bool boolValue)
                return boolValue == showIf.ExpectedValue;

            return true;
        }

        private object GetTargetObject(SerializedProperty property)
        {
            if (property == null)
                return null;

            string path = property.propertyPath.Replace(".Array.data[", "[");
            object obj = property.serializedObject.targetObject;
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                string element = elements[i];

                if (element.Contains("["))
                {
                    int bracketIndex = element.IndexOf("[", StringComparison.Ordinal);
                    string elementName = element.Substring(0, bracketIndex);
                    int endBracketIndex = element.IndexOf("]", bracketIndex, StringComparison.Ordinal);
                    string indexStr = element.Substring(bracketIndex + 1, endBracketIndex - bracketIndex - 1);
                    int index = Convert.ToInt32(indexStr);

                    obj = GetIndexedValue(obj, elementName, index);
                }
                else
                {
                    obj = GetMemberValue(obj, element);
                }

                if (obj == null)
                    return null;
            }

            return obj;
        }

        private object GetMemberValue(object source, string name)
        {
            if (source == null) return null;

            Type type = source.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(source);

            PropertyInfo prop =
                type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(source, null);

            return null;
        }

        private object GetIndexedValue(object source, string name, int index)
        {
            object list = GetMemberValue(source, name);
            if (list is System.Collections.IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                for (int i = 0; i <= index; i++)
                {
                    if (!enumerator.MoveNext())
                        return null;
                }

                return enumerator.Current;
            }

            return null;
        }
    }
}
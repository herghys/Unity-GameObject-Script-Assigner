using Herghys.GameObjectScriptAssigner.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.EditorScripts.Drawer
{

    [CustomEditor(typeof(InteractionScriptAssignerTemplate))]
    public class InteractionScriptAssignerTemplateEditor : Editor
    {
        private InteractionScriptAssignerTemplate template;
        private ReorderableList scriptsList;
        private readonly List<bool> foldoutStates = new();

        private void OnEnable()
        {
            template = (InteractionScriptAssignerTemplate)target;
            SerializedProperty scriptsProp = serializedObject.FindProperty("InteractionScripts");

            scriptsList = new ReorderableList(serializedObject, scriptsProp, true, true, true, true);

            scriptsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Interaction Script Templates", EditorStyles.boldLabel);
            };

            scriptsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                EnsureFoldoutState(index);
                SerializedProperty element = scriptsProp.GetArrayElementAtIndex(index);
                DrawScriptTemplate(rect, element, index);
            };

            scriptsList.elementHeightCallback = index =>
            {
                EnsureFoldoutState(index);
                SerializedProperty element = scriptsProp.GetArrayElementAtIndex(index);

                SerializedProperty useCustomProp = element.FindPropertyRelative("UseCustomScript");
                SerializedProperty customScriptsProp = element.FindPropertyRelative("TargetCustomScripts");

                float height = EditorGUIUtility.singleLineHeight + 6f; // base foldout
                if (!foldoutStates[index])
                    return height;

                // Space for popup + checkbox
                height += EditorGUIUtility.singleLineHeight * 2f + 10f;

                // Space for HelpBox if duplicate
                SerializedProperty interactionProp = element.FindPropertyRelative("Interaction");
                if (template.InteractionScripts
                    .Where((_, i) => i != index)
                    .Any(s => s.Interaction == interactionProp.stringValue && !string.IsNullOrEmpty(interactionProp.stringValue)))
                {
                    height += 40f;
                }

                if (useCustomProp.boolValue)
                    height += EditorGUI.GetPropertyHeight(customScriptsProp, true) + 6f;

                return height;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty availableProp = serializedObject.FindProperty("AvailableAction");
            EditorGUILayout.PropertyField(availableProp);

            if (template.AvailableAction == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an AvailableAction asset to enable interaction selection.",
                    MessageType.Warning
                );
                serializedObject.ApplyModifiedProperties();
                return; // OK to return here — layout is stable because no further GUILayout calls exist
            }

            EditorGUILayout.Space(10);
            scriptsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptTemplate(Rect rect, SerializedProperty element, int index)
        {
            EnsureFoldoutState(index);

            SerializedProperty interactionProp = element.FindPropertyRelative("Interaction");
            SerializedProperty useCustomProp = element.FindPropertyRelative("UseCustomScript");
            SerializedProperty customScriptsProp = element.FindPropertyRelative("TargetCustomScripts");

            string headerTitle = string.IsNullOrEmpty(interactionProp.stringValue)
                ? $"Template {index + 1}"
                : interactionProp.stringValue;

            // Foldout header
            Rect foldRect = new Rect(rect.x + 10, rect.y + 2, rect.width - 10, EditorGUIUtility.singleLineHeight);
            foldoutStates[index] = EditorGUI.Foldout(foldRect, foldoutStates[index], headerTitle, true);

            if (!foldoutStates[index])
                return;

            var availableList = template.AvailableAction != null
                ? template.AvailableAction.Interactions
                : new List<string>();

            string[] options = availableList.ToArray();

            float y = foldRect.y + EditorGUIUtility.singleLineHeight + 6f;
            float fieldWidth = rect.width - 25f;

            // Interaction popup
            Rect popupRect = new Rect(rect.x + 25, y, fieldWidth, EditorGUIUtility.singleLineHeight);
            int currentIndex = Mathf.Max(0, System.Array.IndexOf(options, interactionProp.stringValue));
            int newIndex = EditorGUI.Popup(popupRect, "Interaction", currentIndex, options);

            string newInteraction = (options.Length > 0 && newIndex >= 0 && newIndex < options.Length)
                ? options[newIndex]
                : string.Empty;

            bool duplicate = template.InteractionScripts
                .Where((_, i) => i != index)
                .Any(s => s.Interaction == newInteraction && !string.IsNullOrEmpty(newInteraction));

            y += EditorGUIUtility.singleLineHeight + 4f;

            if (duplicate)
            {
                Rect warnRect = new Rect(rect.x + 25, y, fieldWidth, 35f);
                EditorGUI.HelpBox(warnRect, $"Interaction '{newInteraction}' is already assigned!", MessageType.Warning);
                y += 40f;
            }
            else
            {
                interactionProp.stringValue = newInteraction;
            }

            // Use custom script toggle
            Rect toggleRect = new Rect(rect.x + 25, y, fieldWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(toggleRect, useCustomProp, new GUIContent("Use Custom Script"));
            y += EditorGUIUtility.singleLineHeight + 6f;

            if (useCustomProp.boolValue)
            {
                Rect customRect = new Rect(rect.x + 25, y, fieldWidth, EditorGUI.GetPropertyHeight(customScriptsProp, true));
                EditorGUI.PropertyField(customRect, customScriptsProp, new GUIContent("Target Custom Scripts"), true);
            }
        }

        private void EnsureFoldoutState(int index)
        {
            while (foldoutStates.Count <= index)
                foldoutStates.Add(true);
        }
    }
}
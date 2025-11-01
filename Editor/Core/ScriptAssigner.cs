using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.Core
{
    public class ScriptAssigner : EditorWindow
    {
        private InteractionScriptAssignerTemplate scriptTemplateAsset;
        private readonly List<GameObjectTargetReferences> targetObjects = new();
        private Vector2 scrollPos;

        [MenuItem("Tools/Herghys/Script Assigner/Script Assigner")]
        public static void OpenWindow()
        {
            GetWindow<ScriptAssigner>("Game Object Script Assigner");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Script References", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("by: Herghys", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Drag GameObjects here and select the Script Template asset to apply.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            // Template selector
            scriptTemplateAsset = (InteractionScriptAssignerTemplate)EditorGUILayout.ObjectField(
                "Script Template Asset",
                scriptTemplateAsset,
                typeof(InteractionScriptAssignerTemplate),
                false);

            EditorGUILayout.Space(8);
            HandleDragAndDrop();
            EditorGUILayout.Space(10);
            DrawTargetList();
        }

        private void HandleDragAndDrop()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 70, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "⬇️ Drag GameObjects Here ⬇️", EditorStyles.helpBox);

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go && !targetObjects.Exists(t => t.TargetGameObject == go))
                            {
                                targetObjects.Add(new GameObjectTargetReferences
                                {
                                    TargetGameObject = go,
                                    SelectedMask = 0
                                });
                            }
                        }
                    }

                    evt.Use();
                    break;
            }
        }

        private void DrawTargetList()
        {
            if (targetObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("No GameObjects added yet.", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            var availableActions = scriptTemplateAsset != null && scriptTemplateAsset.AvailableAction != null
                ? scriptTemplateAsset.AvailableAction.Interactions
                : new List<string>();

            for (int i = 0; i < targetObjects.Count; i++)
            {
                var entry = targetObjects[i];
                EditorGUILayout.BeginVertical("box");

                // Row with GameObject and Remove button
                EditorGUILayout.BeginHorizontal();
                entry.TargetGameObject =
                    (GameObject)EditorGUILayout.ObjectField(entry.TargetGameObject, typeof(GameObject), true);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    targetObjects.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (availableActions.Count > 0)
                {
                    EditorGUILayout.LabelField("Interactions", EditorStyles.boldLabel);

                    // Build dynamic mask names
                    string[] names = availableActions.ToArray();
                    int newMask = EditorGUILayout.MaskField("Select Interactions", entry.SelectedMask, names);

                    // Detect change
                    if (newMask != entry.SelectedMask)
                    {
                        entry.SelectedMask = newMask;
                        entry.SelectedInteractions = names
                            .Where((name, index) => (entry.SelectedMask & (1 << index)) != 0)
                            .ToList();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No available interactions defined in the template asset.",
                        MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(scriptTemplateAsset == null || targetObjects.Count == 0))
            {
                if (GUILayout.Button("Apply Template to GameObjects", GUILayout.Height(30)))
                {
                    ApplyScriptsToGameObjects();
                }
            }
        }

        private void ApplyScriptsToGameObjects()
        {
            if (scriptTemplateAsset == null)
            {
                Debug.LogWarning("No ScriptTemplate asset assigned.");
                return;
            }

            if (targetObjects.Count == 0)
            {
                Debug.LogWarning("No GameObjects assigned.");
                return;
            }

            Undo.SetCurrentGroupName("Batch Add Scripts");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < targetObjects.Count; i++)
                {
                    var target = targetObjects[i];
                    if (target?.TargetGameObject == null)
                        continue;

                    float progress = (float)i / targetObjects.Count;
                    EditorUtility.DisplayProgressBar("Processing GameObjects",
                        $"Checking {target.TargetGameObject.name}", progress);

                    // --- Renderer & Mesh Read/Write check ---
                    if (target.TargetGameObject.TryGetComponent(out Renderer renderer))
                    {
                        Mesh mesh = null;

                        if (renderer is MeshRenderer meshRenderer)
                        {
                            MeshFilter mf = meshRenderer.GetComponent<MeshFilter>();
                            if (mf != null) mesh = mf.sharedMesh;
                        }
                        else if (renderer is SkinnedMeshRenderer skinnedRenderer)
                        {
                            mesh = skinnedRenderer.sharedMesh;
                        }

                        if (mesh != null)
                        {
                            string meshPath = AssetDatabase.GetAssetPath(mesh);
                            var importer = AssetImporter.GetAtPath(meshPath) as ModelImporter;

                            if (importer != null && !importer.isReadable)
                            {
                                Debug.LogWarning(
                                    $"Mesh '{mesh.name}' on '{target.TargetGameObject.name}' is not readable. Enabling Read/Write...");
                                importer.isReadable = true;
                                importer.SaveAndReimport();
                            }
                        }
                    }

                    // --- Interaction and Script Application ---
                    foreach (var template in scriptTemplateAsset.InteractionScripts)
                    {
                        if (!template.UseCustomScript ||
                            template.TargetCustomScripts == null ||
                            template.TargetCustomScripts.Count == 0)
                            continue;

                        string interactionName = template.Interaction?.ToString();
                        if (string.IsNullOrEmpty(interactionName))
                            continue;

                        if (!target.SelectedInteractions.Contains(interactionName))
                            continue;

                        foreach (MonoScript script in template.TargetCustomScripts)
                        {
                            if (script == null) continue;

                            Type componentType = script.GetClass();
                            if (componentType == null || !componentType.IsSubclassOf(typeof(MonoBehaviour)))
                                continue;

                            if (target.TargetGameObject.GetComponent(componentType) == null)
                            {
                                EditorUtility.DisplayProgressBar("Processing GameObjects",
                                    $"Adding {script.name} to {target.TargetGameObject.name}", progress);

                                Undo.AddComponent(target.TargetGameObject, componentType);
                                Debug.Log($"Added {componentType.Name} to {target.TargetGameObject.name}");
                            }
                            else
                            {
                                Debug.LogWarning(
                                    $"{target.TargetGameObject.name} already has {componentType.Name}, skipped.");
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
    }
}

[Serializable]
public class GameObjectTargetReferences
{
    public GameObject TargetGameObject;
    public int SelectedMask;
    public List<string> SelectedInteractions = new();
}
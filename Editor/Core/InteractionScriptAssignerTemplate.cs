using Herghys.GameObjectScriptAssigner.Attribute;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.Core
{
    [CreateAssetMenu(fileName = "CustomScriptData", menuName = "Editor/Custom Script Template")]
    public class InteractionScriptAssignerTemplate : ScriptableObject
    {
        public AvailableAction AvailableAction;
        public List<ScriptTemplate> InteractionScripts;
    }

    [Serializable]
    public class ScriptTemplate
    {
        public string Interaction;
        public bool UseCustomScript;

        [ShowIf("UseCustomScript", true)] public List<MonoScript> TargetCustomScripts;
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.Core
{
    [CreateAssetMenu(fileName = "AvailableInteraction",  menuName = "Editor/Available Interaction")]
    public class AvailableAction : ScriptableObject
    {
        public List<string> Interactions = new();
    }
}
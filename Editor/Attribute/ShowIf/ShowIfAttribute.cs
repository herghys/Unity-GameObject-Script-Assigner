using System;
using UnityEngine;

namespace Herghys.GameObjectScriptAssigner.Attribute
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionName { get; }
        public bool ExpectedValue { get; }

        public ShowIfAttribute(string conditionName, bool expectedValue = true)
        {
            ConditionName = conditionName;
            ExpectedValue = expectedValue;
        }
    }
}
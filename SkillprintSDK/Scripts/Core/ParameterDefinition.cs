using UnityEngine;
using System;

namespace Skillprint.SDK
{
    [Serializable]
    public class ParameterDefinition
    {
        [Tooltip("Unique identifier for this parameter. Used by Skillprint API.")]
        public string parameterName;

        [Tooltip("Description of what this game variable controls.")]
        public string description;

        [Tooltip("Explanation of how the SDK is expected to modify this variable.")]
        public string howSDKChangesIt;

        [Tooltip("The data type of the parameter.")]
        public ParameterType type;

        // Range for numeric types
        [Tooltip("Minimum value (for Float or Integer types).")]
        public float minValue;
        [Tooltip("Maximum value (for Float or Integer types).")]
        public float maxValue;

        // Optional: Default value, if needed for initialization or reset
        [Tooltip("Default value (optional).")]
        public string defaultValue; // Store as string, parse based on type

        // For SDK internal use, not directly set by dev in editor
        [NonSerialized] public Action<object> UpdateAction; // Action to call to update the game variable

        public bool IsValid(object value)
        {
            switch (type)
            {
                case ParameterType.Float:
                    if (value is float f) return f >= minValue && f <= maxValue;
                    break;
                case ParameterType.Integer:
                    if (value is int i) return (float)i >= minValue && (float)i <= maxValue;
                    break;
                case ParameterType.Boolean:
                    return value is bool;
            }
            return false;
        }

        public object ConvertValue(object rawValue)
        {
            try
            {
                switch (type)
                {
                    case ParameterType.Float:
                        return System.Convert.ToSingle(rawValue);
                    case ParameterType.Integer:
                        return System.Convert.ToInt32(rawValue);
                    case ParameterType.Boolean:
                        return System.Convert.ToBoolean(rawValue);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillprintSDK] Error converting value for parameter {parameterName}: {ex.Message}");
            }
            return null;
        }
    }
}
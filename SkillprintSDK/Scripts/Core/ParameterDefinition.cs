using UnityEngine;
using System;
using System.Globalization;

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

        /// <summary>
        /// Returns <paramref name="value"/> pulled into [minValue, maxValue] for numeric types.
        /// Booleans and values of the wrong type are returned unchanged.
        /// </summary>
        public object Clamp(object value)
        {
            switch (type)
            {
                case ParameterType.Float:
                    if (value is float f) return Mathf.Clamp(f, minValue, maxValue);
                    break;
                case ParameterType.Integer:
                    if (value is int i) return Mathf.Clamp(i, Mathf.CeilToInt(minValue), Mathf.FloorToInt(maxValue));
                    break;
            }
            return value;
        }

        /// <summary>
        /// Converts a value from the API to this parameter's type, or returns null when
        /// it can't be converted. Null stays null rather than becoming 0 / false.
        /// </summary>
        public object ConvertValue(object rawValue)
        {
            if (rawValue == null)
                return null;

            try
            {
                // Strings from the API use '.' for decimals whatever the player's locale.
                switch (type)
                {
                    case ParameterType.Float:
                        return System.Convert.ToSingle(rawValue, CultureInfo.InvariantCulture);
                    case ParameterType.Integer:
                        return System.Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                    case ParameterType.Boolean:
                        return System.Convert.ToBoolean(rawValue, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                // A warning, not an error: the update is skipped and the game carries on.
                Debug.LogWarning($"[SkillprintSDK] Could not convert value '{rawValue}' for parameter {parameterName}: {ex.Message}");
            }
            return null;
        }
    }
}
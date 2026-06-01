using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Skillprint.SDK.API
{
    // --- Request Models ---

    [Serializable]
    public class StartSessionRequest
    {
        public string sessionId;
        public string game;
        public string targetMood;
        public List<ParameterInfo> gameParameters;
    }

    [Serializable]
    public class ParameterInfo
    {
        public string name;
        public string type;
        public string description;
        public string adjustmentGuide; // Maps from ParameterDefinition.howSDKChangesIt
        public string minValue; // Sent as string, can be null
        public string maxValue; // Sent as string, can be null
    }

    // Screenshot data will be sent via MultipartFormDataContent, so no specific request model here.

    // --- Response Models ---

    [Serializable]
    public class BaseResponse // A generic response, adapt if API returns specific success/error structures
    {
        public bool success;
        public string message;
        // public string session_id; // If StartSession returns it in body
    }

    [Serializable]
    public class PollResultsResponse
    {
        public string gameplayTips;
        public string state;
        public List<ParameterUpdateResult> parameterUpdates;
    }

    [Serializable]
    public class ParameterUpdateResult
    {
        public string parameterName;
        public object newValue;

        // Additional fields that might come from API for debugging
        [SerializeField]
        private string rawJson; // For debugging purposes

        /// <summary>
        /// Attempts to extract the actual value from the API response.
        /// The API sometimes sends the value in different formats or locations.
        /// </summary>
        public object GetParsedValue()
        {
            // First try the direct newValue
            if (newValue != null && !IsEmptyValue(newValue))
            {
                return newValue;
            }

            // If newValue is null or empty, try to find a field with the same name as parameterName
            // This handles cases where API sends: {"parameterName": "fruitSpawnRate", "fruitSpawnRate": 0.75, "newValue": 0.75}
            if (!string.IsNullOrEmpty(parameterName))
            {
                try
                {
                    // Use reflection to check if there's a field with the parameter name
                    Type thisType = this.GetType();
                    FieldInfo field = thisType.GetField(parameterName);
                    if (field != null)
                    {
                        object fieldValue = field.GetValue(this);
                        if (fieldValue != null && !IsEmptyValue(fieldValue))
                        {
                            return fieldValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[SkillprintSDK] Reflection failed for parameter {parameterName}: {ex.Message}"
                    );
                }
            }

            return null;
        }

        private bool IsEmptyValue(object value)
        {
            if (value == null)
                return true;
            if (value is string str)
                return string.IsNullOrEmpty(str);
            return false;
        }

        /// <summary>
        /// Converts the parsed value to the specified target type
        /// </summary>
        public T ConvertToType<T>()
        {
            object value = GetParsedValue();
            if (value == null)
                return default(T);

            try
            {
                if (value is T directCast)
                {
                    return directCast;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SkillprintSDK] Failed to convert value '{value}' to type {typeof(T)}: {ex.Message}"
                );
                return default(T);
            }
        }

        /// <summary>
        /// Safely converts value based on ParameterDefinition type constraints
        /// </summary>
        public object ConvertToParameterType(ParameterDefinition paramDef)
        {
            object rawValue = GetParsedValue();
            if (rawValue == null)
                return null;

            try
            {
                object convertedValue = null;

                switch (paramDef.type)
                {
                    case ParameterType.Float:
                        convertedValue = Convert.ToSingle(rawValue);
                        break;
                    case ParameterType.Integer:
                        convertedValue = Convert.ToInt32(rawValue);
                        break;
                    case ParameterType.Boolean:
                        convertedValue = Convert.ToBoolean(rawValue);
                        break;
                    default:
                        convertedValue = rawValue;
                        break;
                }

                // Validate the converted value
                if (paramDef.IsValid(convertedValue))
                {
                    return convertedValue;
                }
                else
                {
                    Debug.LogWarning(
                        $"[SkillprintSDK] Converted value {convertedValue} for parameter {parameterName} is outside valid range [{paramDef.minValue}, {paramDef.maxValue}]"
                    );

                    // Clamp numeric values to valid range
                    if (paramDef.type == ParameterType.Float && convertedValue is float f)
                    {
                        return Mathf.Clamp(f, paramDef.minValue, paramDef.maxValue);
                    }
                    else if (paramDef.type == ParameterType.Integer && convertedValue is int i)
                    {
                        return Mathf.Clamp(i, (int)paramDef.minValue, (int)paramDef.maxValue);
                    }

                    return convertedValue; // Return as-is for booleans or other types
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SkillprintSDK] Error converting parameter {parameterName}: {ex.Message}"
                );
                return null;
            }
        }
    }

    /// <summary>
    /// Enhanced parser for handling dynamic parameter updates from API responses
    /// </summary>
    public static class ParameterUpdateParser
    {
        /// <summary>
        /// Parse parameter updates from JSON response, handling various API response formats
        /// </summary>
        public static List<ParameterUpdateResult> ParseFromJson(string jsonResponse)
        {
            List<ParameterUpdateResult> results = new List<ParameterUpdateResult>();

            try
            {
                // First try to parse as PollResultsResponse
                // PollResultsResponse response = JsonUtility.FromJson<PollResultsResponse>(
                //     jsonResponse
                // );
                // if (response?.parameterUpdates != null)
                // {
                //     return EnhanceParameterUpdates(response.parameterUpdates, jsonResponse);
                // }

                // If that fails, try parsing as direct array wrapped in an object
                if (jsonResponse.Trim().StartsWith("["))
                {
                    string wrappedJson = $"{{\"parameterUpdates\":{jsonResponse}}}";
                    PollResultsResponse wrappedResponse = JsonUtility.FromJson<PollResultsResponse>(
                        wrappedJson
                    );
                    if (wrappedResponse?.parameterUpdates != null)
                    {
                        return EnhanceParameterUpdates(
                            wrappedResponse.parameterUpdates,
                            jsonResponse
                        );
                    }
                }

                // Last resort: try to extract parameter updates from the raw JSON manually
                return ParseParameterUpdatesManually(jsonResponse);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillprintSDK] Failed to parse parameter updates: {ex.Message}");
                Debug.LogError($"[SkillprintSDK] Raw JSON: {jsonResponse}");
            }

            return results;
        }

        private static List<ParameterUpdateResult> EnhanceParameterUpdates(
            List<ParameterUpdateResult> updates,
            string originalJson
        )
        {
            // Add debugging information and enhance parsing
            foreach (var update in updates)
            {
                if (update != null)
                {
                    // Store original JSON for debugging
                    var field = typeof(ParameterUpdateResult).GetField(
                        "rawJson",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    field?.SetValue(update, originalJson);

                    Debug.Log(
                        $"[SkillprintSDK] [DEBUG] Enhanced update - Name: {update.parameterName}, Raw Value: {update.newValue}, Parsed Value: {update.GetParsedValue()}"
                    );
                }
            }

            return updates;
        }

        private static List<ParameterUpdateResult> ParseParameterUpdatesManually(
            string jsonResponse
        )
        {
            List<ParameterUpdateResult> results = new List<ParameterUpdateResult>();

            try
            {
                // Look for parameterUpdates array in the JSON
                int paramUpdatesStart = jsonResponse.IndexOf("\"parameterUpdates\":");
                if (paramUpdatesStart == -1)
                    return results;

                int arrayStart = jsonResponse.IndexOf("[", paramUpdatesStart);
                if (arrayStart == -1)
                    return results;

                int arrayEnd = FindMatchingBracket(jsonResponse, arrayStart);
                if (arrayEnd == -1)
                    return results;

                string arrayJson = jsonResponse.Substring(arrayStart, arrayEnd - arrayStart + 1);
                Debug.Log(
                    $"[SkillprintSDK] Manually extracted parameter updates array: {arrayJson}"
                );

                // Try to parse individual objects
                results = ParseParameterArray(arrayJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillprintSDK] Manual parsing failed: {ex.Message}");
            }

            return results;
        }

        private static int FindMatchingBracket(string json, int startIndex)
        {
            int bracketCount = 0;
            for (int i = startIndex; i < json.Length; i++)
            {
                if (json[i] == '[')
                    bracketCount++;
                else if (json[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                        return i;
                }
            }
            return -1;
        }

        private static List<ParameterUpdateResult> ParseParameterArray(string arrayJson)
        {
            List<ParameterUpdateResult> results = new List<ParameterUpdateResult>();

            // Simple regex-like parsing for each object in the array
            string[] objects = SplitJsonArray(arrayJson);

            foreach (string objJson in objects)
            {
                if (string.IsNullOrWhiteSpace(objJson))
                    continue;

                try
                {
                    ParameterUpdateResult update = ParseSingleParameterUpdate(objJson);
                    if (update != null)
                    {
                        results.Add(update);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[SkillprintSDK] Failed to parse individual parameter update: {objJson}. Error: {ex.Message}"
                    );
                }
            }

            return results;
        }

        private static string[] SplitJsonArray(string arrayJson)
        {
            List<string> objects = new List<string>();
            arrayJson = arrayJson.Trim();

            if (!arrayJson.StartsWith("[") || !arrayJson.EndsWith("]"))
                return objects.ToArray();

            string content = arrayJson.Substring(1, arrayJson.Length - 2);
            int braceCount = 0;
            int start = 0;

            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '{')
                    braceCount++;
                else if (content[i] == '}')
                    braceCount--;
                else if (content[i] == ',' && braceCount == 0)
                {
                    objects.Add(content.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            if (start < content.Length)
            {
                objects.Add(content.Substring(start).Trim());
            }

            return objects.ToArray();
        }

        private static ParameterUpdateResult ParseSingleParameterUpdate(string objJson)
        {
            // Clean up the JSON object string
            objJson = objJson.Trim();
            if (!objJson.StartsWith("{"))
                objJson = "{" + objJson;
            if (!objJson.EndsWith("}"))
                objJson = objJson + "}";

            Debug.Log($"[SkillprintSDK] Parsing single parameter update: {objJson}");

            // Try JsonUtility first
            try
            {
                ParameterUpdateResult result = JsonUtility.FromJson<ParameterUpdateResult>(objJson);
                // ****** ADD THIS DEBUG LOG ******
                if (result != null)
                {
                    Debug.Log(
                        $"[SkillprintSDK] JsonUtility result - Name: {result.parameterName}, NewValue: {result.newValue ?? "null"}, NewValue Type: {result.newValue?.GetType().Name ?? "null"}"
                    );
                }
                else
                {
                    Debug.LogWarning("[SkillprintSDK] JsonUtility returned null result.");
                }
                // ****** END ADDED DEBUG LOG ******
                // if (result != null && !string.IsNullOrEmpty(result.parameterName))
                // {
                //     return result;
                // }
                if (
                    result != null
                    && !string.IsNullOrEmpty(result.parameterName)
                    && result.newValue != null
                ) // MODIFIED: Check newValue too
                {
                    // Debug.Log("[SkillprintSDK] JsonUtility parsing successful for item (including newValue).");
                    return result;
                }
                // If we reach here, either result is null, parameterName is empty, OR NEWVALUE IS NULL
                Debug.LogWarning(
                    $"[SkillprintSDK] JsonUtility parsing produced incomplete result (newValue is null or parameterName missing) for '{objJson}'. Falling back to manual parsing."
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SkillprintSDK] JsonUtility parsing failed, trying manual: {ex.Message}"
                );
            }

            // Manual parsing as fallback
            return ParseParameterUpdateManually(objJson);
        }

        private static ParameterUpdateResult ParseParameterUpdateManually(string objJson)
        {
            ParameterUpdateResult result = new ParameterUpdateResult();

            // Extract parameterName
            string parameterName = ExtractJsonValue(objJson, "parameterName");
            if (string.IsNullOrEmpty(parameterName))
                return null;

            result.parameterName = parameterName.Trim('"');

            // Extract newValue
            string newValueStr = ExtractJsonValue(objJson, "newValue");
            if (!string.IsNullOrEmpty(newValueStr))
            {
                result.newValue = ParseJsonValue(newValueStr);
            }
            else
            {
                // Try to find a field with the same name as the parameter
                string paramValueStr = ExtractJsonValue(objJson, result.parameterName);
                if (!string.IsNullOrEmpty(paramValueStr))
                {
                    result.newValue = ParseJsonValue(paramValueStr);
                }
            }

            return result;
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string searchPattern = $"\"{key}\":";
            int keyIndex = json.IndexOf(searchPattern);
            if (keyIndex == -1)
                return null;

            int valueStart = keyIndex + searchPattern.Length;

            // Skip whitespace
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length)
                return null;

            // Find the end of the value
            int valueEnd = valueStart;
            bool inString = json[valueStart] == '"';

            if (inString)
            {
                valueEnd = valueStart + 1;
                while (valueEnd < json.Length && json[valueEnd] != '"')
                {
                    if (json[valueEnd] == '\\')
                        valueEnd++; // Skip escaped characters
                    valueEnd++;
                }
                valueEnd++; // Include the closing quote
            }
            else
            {
                while (valueEnd < json.Length && json[valueEnd] != ',' && json[valueEnd] != '}')
                    valueEnd++;
            }

            return json.Substring(valueStart, valueEnd - valueStart).Trim();
        }

        private static object ParseJsonValue(string valueStr)
        {
            valueStr = valueStr.Trim();

            // Remove quotes if it's a string
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // Try to parse as number
            if (float.TryParse(valueStr, out float floatVal))
            {
                // If it's a whole number, return as int
                if (floatVal == (int)floatVal)
                    return (int)floatVal;
                return floatVal;
            }

            // Try to parse as boolean
            if (bool.TryParse(valueStr, out bool boolVal))
            {
                return boolVal;
            }

            // Return as string if all else fails
            return valueStr;
        }
    }

    // --- User Profile & Progression Models ---

    [System.Serializable]
    public class UserProfileResponse
    {
        public List<UserProfileResult> results;
    }

    [System.Serializable]
    public class UserProfileResult
    {
        public int id;
        public int totalSessions;
        public string totalTimePlayed;
        public float avgFlowScore;
        public float flowConfidence;
        public List<FlowScoreEntry> flowScoreHistory;
    }

    [System.Serializable]
    public class FlowScoreEntry
    {
        public float score;
        public string timestamp;
        public float confidence;
        public string targetMood;
    }

    [System.Serializable]
    public class SkillProgressionResponse
    {
        public List<SkillProgressionItem> yearlySummary;
    }

    [System.Serializable]
    public class SkillProgressionItem
    {
        public string skill;
        public string mood;
    }
}

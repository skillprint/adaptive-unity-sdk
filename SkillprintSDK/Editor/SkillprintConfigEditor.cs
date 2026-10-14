using Skillprint.SDK; // Ensure this using directive is present
using UnityEditor;
using UnityEngine;

namespace Skillprint.SDK.Editor
{
    [CustomEditor(typeof(SkillprintConfig))]
    public class SkillprintConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SkillprintConfig config = (SkillprintConfig)target;

            EditorGUILayout.LabelField("Skillprint SDK Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetEnvironment"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("authMode"));
            if (config.authMode == SkillprintAuthMode.TokenProvider)
            {
                EditorGUILayout.HelpBox(
                    "No API key ships in the build. Your backend mints SDK tokens "
                        + "(POST /partners/api/sdk/token/) and the game supplies them with "
                        + "SkillprintManager.Instance.SetTokenProvider(...) before starting a session. "
                        + "Leave the Partner API Key fields below empty.",
                    MessageType.Info
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "PartnerApiKey mode embeds your organization's API key in the build, where it can be "
                        + "extracted. Use it for development; switch to TokenProvider for shipped games.",
                    MessageType.Warning
                );
            }
            EditorGUILayout.Space();

            // --- Game Configuration ---
            EditorGUILayout.LabelField("Game Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gameName"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // --- Production Settings ---
            EditorGUILayout.LabelField("Production Environment", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("productionPartnerApiKey"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("productionApiBaseUrl"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // --- Staging Settings ---
            EditorGUILayout.LabelField("Staging Environment", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("stagingPartnerApiKey"),
                new GUIContent(
                    "Partner API Key (Staging)",
                    "Leave blank to use Production API Key for Staging."
                )
            );
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stagingApiBaseUrl"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("SDK Behavior", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("screenshotIntervalSeconds")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("screenshotPostIntervalSeconds")
            );
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("pollResultsIntervalSeconds")
            );
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Game Parameters", EditorStyles.boldLabel);
            SerializedProperty gameParametersProp = serializedObject.FindProperty("gameParameters");
            EditorGUILayout.PropertyField(gameParametersProp, true);

            // Validation checks
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Configuration Preview:", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                $"Target Env: {config.targetEnvironment}\nAuth Mode: {config.authMode}\nActive API Key: {MaskKey(config.ActivePartnerApiKey)}\nActive Base URL: {config.ActiveApiBaseUrl}",
                MessageType.None
            );

            // Validation checks
            if (string.IsNullOrWhiteSpace(config.gameName))
            {
                EditorGUILayout.HelpBox("Game name is required.", MessageType.Error);
            }
            if (config.RequiresPartnerApiKey && string.IsNullOrWhiteSpace(config.ActivePartnerApiKey))
            {
                EditorGUILayout.HelpBox(
                    "The active Partner API Key is required for the selected environment.",
                    MessageType.Error
                );
            }
            if (!config.RequiresPartnerApiKey
                && (!string.IsNullOrWhiteSpace(config.productionPartnerApiKey)
                    || !string.IsNullOrWhiteSpace(config.stagingPartnerApiKey)))
            {
                EditorGUILayout.HelpBox(
                    "Auth Mode is TokenProvider, so the Partner API Key is unused — but it is still "
                        + "serialized into your build. Clear both key fields.",
                    MessageType.Warning
                );
            }
            if (string.IsNullOrWhiteSpace(config.ActiveApiBaseUrl))
            {
                EditorGUILayout.HelpBox(
                    "The active API Base URL is required for the selected environment.",
                    MessageType.Error
                );
            }
            if (config.screenshotIntervalSeconds <= 0)
            {
                EditorGUILayout.HelpBox(
                    "Screenshot Interval must be positive.",
                    MessageType.Warning
                );
            }
            if (config.screenshotPostIntervalSeconds <= config.screenshotIntervalSeconds)
            {
                EditorGUILayout.HelpBox(
                    "Screenshot Post Interval should generally be greater than Screenshot Interval.",
                    MessageType.Warning
                );
            }
            if (config.pollResultsIntervalSeconds <= 0)
            {
                EditorGUILayout.HelpBox(
                    "Poll Results Interval must be positive.",
                    MessageType.Warning
                );
            }

            // TODO add more detailed per-parameter validation here if needed

            serializedObject.ApplyModifiedProperties();
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "(none)";
            }
            int dot = key.IndexOf('.');
            string prefix = dot > 0 ? key.Substring(0, dot) : key.Substring(0, System.Math.Min(4, key.Length));
            return prefix + ".••••";
        }
    }
}

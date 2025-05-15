using UnityEngine;
using UnityEditor;
using Skillprint.SDK; // Ensure this using directive is present

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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stagingPartnerApiKey"), new GUIContent("Partner API Key (Staging)", "Leave blank to use Production API Key for Staging."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stagingApiBaseUrl"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();


            EditorGUILayout.LabelField("SDK Behavior", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("screenshotIntervalSeconds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("screenshotPostIntervalSeconds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pollResultsIntervalSeconds"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Game Parameters", EditorStyles.boldLabel);
            SerializedProperty gameParametersProp = serializedObject.FindProperty("gameParameters");
            EditorGUILayout.PropertyField(gameParametersProp, true);

            // Validation checks
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Configuration Preview:", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox($"Target Env: {config.targetEnvironment}\nActive API Key: {config.ActivePartnerApiKey}\nActive Base URL: {config.ActiveApiBaseUrl}", MessageType.None);


            // Validation checks
            if (string.IsNullOrWhiteSpace(config.ActivePartnerApiKey))
            {
                EditorGUILayout.HelpBox("The active Partner API Key is required for the selected environment.", MessageType.Error);
            }
            if (string.IsNullOrWhiteSpace(config.ActiveApiBaseUrl))
            {
                EditorGUILayout.HelpBox("The active API Base URL is required for the selected environment.", MessageType.Error);
            }
            if (config.screenshotIntervalSeconds <= 0)
            {
                EditorGUILayout.HelpBox("Screenshot Interval must be positive.", MessageType.Warning);
            }
            if (config.screenshotPostIntervalSeconds <= config.screenshotIntervalSeconds)
            {
                EditorGUILayout.HelpBox("Screenshot Post Interval should generally be greater than Screenshot Interval.", MessageType.Warning);
            }
            if (config.pollResultsIntervalSeconds <= 0)
            {
                EditorGUILayout.HelpBox("Poll Results Interval must be positive.", MessageType.Warning);
            }

            // TODO add more detailed per-parameter validation here if needed

            serializedObject.ApplyModifiedProperties();
        }
    }
}
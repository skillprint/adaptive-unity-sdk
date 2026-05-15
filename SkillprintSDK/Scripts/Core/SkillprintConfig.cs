using System.Collections.Generic;
using UnityEngine;

namespace Skillprint.SDK
{
    public enum ApiEnvironment
    {
        Production,
        Staging,
    }

    [CreateAssetMenu(fileName = "SkillprintConfig", menuName = "Skillprint/SDK Configuration")]
    public class SkillprintConfig : ScriptableObject
    {
        [Header("Game Configuration")]
        [Tooltip("The name of your game as registered in the Skillprint API.")]
        public string gameName;

        [Header("Environment Configuration")]
        [Tooltip("Select the API environment the SDK should target.")]
        public ApiEnvironment targetEnvironment = ApiEnvironment.Production;

        [Header("Production API Configuration")]
        [Tooltip("Your Skillprint Partner API Key for the PRODUCTION environment.")]
        public string productionPartnerApiKey;

        [Tooltip("Base URL for the Skillprint PRODUCTION API. e.g., https://api.skillprint.co")]
        public string productionApiBaseUrl = "https://api.skillprint.co";

        [Header("Staging API Configuration")]
        [Tooltip("Your Skillprint Partner API Key for the STAGING environment (if different).")]
        public string stagingPartnerApiKey;

        [Tooltip(
            "Base URL for the Skillprint STAGING API. e.g., https://api.staging.skillprint.co"
        )]
        public string stagingApiBaseUrl = "https://api.staging.skillprint.co";

        [Header("Gameplay Parameters")]
        [Tooltip("List of game parameters the SDK can modify.")]
        public List<ParameterDefinition> gameParameters = new List<ParameterDefinition>();

        [Header("SDK Behavior")]
        [Tooltip("Maximum width for screenshots in pixels. Images wider than this are downscaled before upload. 0 = no limit.")]
        public int screenshotMaxWidth = 960;

        [Tooltip("JPEG compression quality (1-100). Lower values produce smaller files.")]
        [Range(1, 100)]
        public int screenshotJpegQuality = 60;

        [Tooltip("Interval in seconds for taking screenshots.")]
        public float screenshotIntervalSeconds = 2.0f;

        [Tooltip("Interval in seconds for posting screenshots to the API.")]
        public float screenshotPostIntervalSeconds = 5.0f;

        [Tooltip("Interval in seconds for polling parameter results from the API.")]
        public float pollResultsIntervalSeconds = 5.0f;

        [Tooltip("Enable verbose logging for debugging SDK integration.")]
        public bool enableDebugLogging = false;

        // Helper properties to get active settings
        public string ActivePartnerApiKey
        {
            get
            {
                switch (targetEnvironment)
                {
                    case ApiEnvironment.Production:
                        return productionPartnerApiKey;
                    case ApiEnvironment.Staging:
                        // Use staging key if provided, otherwise fallback to production key (common scenario)
                        return string.IsNullOrEmpty(stagingPartnerApiKey)
                            ? productionPartnerApiKey
                            : stagingPartnerApiKey;
                    default:
                        return productionPartnerApiKey;
                }
            }
        }

        public string ActiveApiBaseUrl
        {
            get
            {
                switch (targetEnvironment)
                {
                    case ApiEnvironment.Production:
                        return productionApiBaseUrl;
                    case ApiEnvironment.Staging:
                        return stagingApiBaseUrl;
                    default:
                        return productionApiBaseUrl;
                }
            }
        }
    }
}

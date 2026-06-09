using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Skillprint.SDK.API
{
    /// <summary>
    /// Utility class for extracting URL parameters in WebGL builds
    /// </summary>
    public static class WebGLUrlParameterExtractor
    {
        /// <summary>
        /// Gets a URL parameter value by name (WebGL only)
        /// </summary>
        /// <param name="parameterName">The name of the URL parameter</param>
        /// <returns>The parameter value or null if not found</returns>
        public static string GetUrlParameter(string parameterName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                Uri uri = new Uri(Application.absoluteURL);
                var queryParams = ParseQueryString(uri.Query);
                return queryParams.ContainsKey(parameterName)
                    ? queryParams[parameterName]
                    : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SkillprintSDK] Failed to get URL parameter '{parameterName}': {ex.Message}"
                );
                return null;
            }
#else
            Debug.LogWarning(
                $"[SkillprintSDK] GetUrlParameter is only supported in WebGL builds. Parameter '{parameterName}' requested."
            );
            return null;
#endif
        }

        /// <summary>
        /// Parses a query string into a dictionary of key-value pairs
        /// </summary>
        /// <param name="queryString">The query string to parse (including leading '?')</param>
        /// <returns>Dictionary containing the parsed parameters</returns>
        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(queryString))
                return result;

            // Remove leading '?' if present
            if (queryString.StartsWith("?"))
                queryString = queryString.Substring(1);

            // Split by '&' to get individual parameters
            string[] pairs = queryString.Split('&');

            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                    continue;

                // Split by '=' to get key and value
                string[] keyValue = pair.Split('=');
                if (keyValue.Length >= 1)
                {
                    string key = Uri.UnescapeDataString(keyValue[0]);
                    string value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : "";

                    // Store the last occurrence if duplicate keys exist
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the current page URL (WebGL only)
        /// </summary>
        /// <returns>The current URL or null if not available</returns>
        public static string GetCurrentUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return Application.absoluteURL;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillprintSDK] Failed to get current URL: {ex.Message}");
                return null;
            }
#else
            Debug.LogWarning("[SkillprintSDK] GetCurrentUrl is only supported in WebGL builds.");
            return null;
#endif
        }

        /// <summary>
        /// Extracts Skillprint-specific parameters from URL
        /// </summary>
        /// <returns>A tuple containing (targetMood, playerId, userToken)</returns>
        public static (string targetMood, string playerId, string userToken) GetSkillprintUrlParameters()
        {
            string targetMood = GetUrlParameter("mood") ?? GetUrlParameter("targetMood");
            string playerId =
                GetUrlParameter("playerId")
                ?? GetUrlParameter("player_id")
                ?? GetUrlParameter("userId");
            string userToken =
                GetUrlParameter("userToken")
                ?? GetUrlParameter("user_token");

            // Set this variables in the PlayerPrefs for later use
            if (!string.IsNullOrEmpty(targetMood))
            {
                PlayerPrefs.SetString("SkillprintMood", targetMood);
            }
            if (!string.IsNullOrEmpty(playerId))
            {
                PlayerPrefs.SetString("SkillprintPlayerId", playerId);
            }
            if (!string.IsNullOrEmpty(userToken))
            {
                PlayerPrefs.SetString("SkillprintUserToken", userToken);
            }

            return (targetMood, playerId, userToken);
        }

        /// <summary>
        /// Checks if the current platform supports URL parameter extraction
        /// </summary>
        public static bool IsUrlParameterSupported()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Enhanced session starter that handles URL parameters for WebGL
    /// </summary>
    public static class SkillprintSessionHelper
    {
        /// <summary>
        /// Starts a Skillprint session with automatic URL parameter detection for WebGL
        /// </summary>
        /// <param name="manager">The SkillprintManager instance</param>
        /// <param name="fallbackMood">Mood to use if not found in URL (default: "focus")</param>
        /// <param name="fallbackPlayerId">Player ID to use if not found in URL (optional)</param>
        /// <param name="overrideMood">Mood to use regardless of URL parameters (optional)</param>
        /// <param name="overridePlayerId">Player ID to use regardless of URL parameters (optional)</param>
        public static void StartSessionWithUrlParams(
            SkillprintManager manager,
            string fallbackMood = "focus",
            string fallbackPlayerId = null,
            string overrideMood = null,
            string overridePlayerId = null
        )
        {
            if (manager == null)
            {
                Debug.LogError("[SkillprintSDK] SkillprintManager is null. Cannot start session.");
                return;
            }

            string targetMood = overrideMood;
            string playerId = overridePlayerId;
            string userToken = null;

            // Only try to get URL parameters if not overridden and on WebGL
            if (string.IsNullOrEmpty(targetMood) || string.IsNullOrEmpty(playerId))
            {
                if (WebGLUrlParameterExtractor.IsUrlParameterSupported())
                {
                    var urlParams = WebGLUrlParameterExtractor.GetSkillprintUrlParameters();

                    if (string.IsNullOrEmpty(targetMood))
                    {
                        targetMood = !string.IsNullOrEmpty(urlParams.targetMood)
                            ? urlParams.targetMood
                            : fallbackMood;
                    }

                    if (string.IsNullOrEmpty(playerId))
                    {
                        playerId = !string.IsNullOrEmpty(urlParams.playerId)
                            ? urlParams.playerId
                            : fallbackPlayerId;
                    }

                    userToken = urlParams.userToken;

                    if (!string.IsNullOrEmpty(userToken))
                    {
                        manager.CurrentUserToken = userToken;
                    }

                    manager.Log(
                        $"WebGL URL Parameters - Mood: '{targetMood}', Player ID: '{playerId}', User Token: '{(string.IsNullOrEmpty(userToken) ? "none" : "[REDACTED]")}'",
                        SkillprintManager.LogLevel.Info
                    );
                }
                else
                {
                    // Not on WebGL, use fallback values
                    if (string.IsNullOrEmpty(targetMood))
                        targetMood = fallbackMood;
                    if (string.IsNullOrEmpty(playerId))
                        playerId = fallbackPlayerId;

                    manager.Log(
                        $"Non-WebGL Platform - Using fallback values. Mood: '{targetMood}', Player ID: '{playerId}'",
                        SkillprintManager.LogLevel.Info
                    );
                }
            }

            // Validate mood
            if (string.IsNullOrEmpty(targetMood))
            {
                manager.Log(
                    "No target mood specified and no fallback provided. Using 'focus' as default.",
                    SkillprintManager.LogLevel.Warning
                );
                targetMood = "focus";
            }

            if (!Enum.IsDefined(typeof(Mood), targetMood))
            {
                string validMoods = string.Join(", ", Enum.GetNames(typeof(Mood)));
                manager.Log(
                    $"Invalid mood '{targetMood}'. Valid moods: {validMoods}. Using 'focus' as fallback.",
                    SkillprintManager.LogLevel.Warning
                );
                targetMood = "focus";
            }

            manager.Log(
                $"Starting Skillprint session with Mood: '{targetMood}', Player ID: '{playerId ?? "none"}'",
                SkillprintManager.LogLevel.Info
            );
            manager.StartGameSession(targetMood, playerId);
        }
    }
}

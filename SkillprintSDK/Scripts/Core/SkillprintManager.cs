using System; // For Guid
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Skillprint.SDK.API; // For LINQ operations on parameters
using UnityEngine;

namespace Skillprint.SDK
{
    public class SkillprintManager : MonoBehaviour
    {
        public static SkillprintManager Instance { get; private set; }

        [Tooltip("Assign your SkillprintConfig ScriptableObject here.")]
        public SkillprintConfig config;

        private string _currentSessionId;
        private bool _isSessionActive = false;
        private SkillprintAPIClient _apiClient;
        private ScreenshotUtility _screenshotUtility;

        private List<Texture2D> _screenshotQueue = new List<Texture2D>();
        private Coroutine _screenshotCaptureCoroutine;
        private Coroutine _screenshotPostCoroutine;
        private Coroutine _pollResultsCoroutine;

        private Dictionary<string, ParameterDefinition> _registeredParameters =
            new Dictionary<string, ParameterDefinition>();

        public string GetCurrentSessionId()
        {
            return _currentSessionId;
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Make SDK persistent across scenes
                InitializeSDK();
            }
            else
            {
                Debug.LogWarning(
                    "[SkillprintSDK] Another instance of SkillprintManager already exists. Destroying this one."
                );
                Destroy(gameObject);
            }
        }

        private void InitializeSDK()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[SkillprintSDK] SkillprintConfig not assigned to SkillprintManager. SDK will not function."
                );
                enabled = false; // Disable this component
                return;
            }

            // Use active properties from config
            if (string.IsNullOrWhiteSpace(config.ActivePartnerApiKey))
            {
                Log(
                    $"Partner API Key for {config.targetEnvironment} environment is not set in SkillprintConfig. SDK will not function.",
                    LogLevel.Error
                );
                enabled = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(config.ActiveApiBaseUrl))
            {
                Log(
                    $"API Base URL for {config.targetEnvironment} environment is not set in SkillprintConfig. SDK will not function.",
                    LogLevel.Error
                );
                enabled = false;
                return;
            }

            _apiClient = new SkillprintAPIClient(
                config.ActiveApiBaseUrl,
                config.ActivePartnerApiKey,
                Log
            );
            _screenshotUtility = new ScreenshotUtility(Log);

            // Prepare registered parameters from config
            foreach (var paramDef in config.gameParameters)
            {
                if (!_registeredParameters.ContainsKey(paramDef.parameterName))
                {
                    // The actual Action will be set by the game developer via RegisterParameterModifier
                    _registeredParameters.Add(paramDef.parameterName, paramDef);
                }
                else
                {
                    Log(
                        $"Duplicate parameter name found in config: {paramDef.parameterName}. Using first occurrence.",
                        LogLevel.Warning
                    );
                }
            }
            Log(
                $"Skillprint SDK Initialized for {config.targetEnvironment} environment (URL: {config.ActiveApiBaseUrl})."
            );
        }

        /// <summary>
        /// Game developers must call this for each parameter defined in SkillprintConfig
        /// to link it to their actual game logic.
        /// </summary>
        /// <typeparam name="T">The type of the parameter (float, int, bool).</typeparam>
        /// <param name="parameterName">The name of the parameter (must match config).</param>
        /// <param name="updateAction">The Action to call when this parameter needs to be updated.</param>
        public void RegisterParameterModifier<T>(string parameterName, Action<T> updateAction)
        {
            if (_registeredParameters.TryGetValue(parameterName, out ParameterDefinition paramDef))
            {
                // Type check
                bool typeMatch = false;
                if (typeof(T) == typeof(float) && paramDef.type == ParameterType.Float)
                    typeMatch = true;
                else if (typeof(T) == typeof(int) && paramDef.type == ParameterType.Integer)
                    typeMatch = true;
                else if (typeof(T) == typeof(bool) && paramDef.type == ParameterType.Boolean)
                    typeMatch = true;

                if (!typeMatch)
                {
                    Log(
                        $"Type mismatch for parameter '{parameterName}'. Expected {paramDef.type}, but got {typeof(T)}. Modifier not registered.",
                        LogLevel.Error
                    );
                    return;
                }

                paramDef.UpdateAction = (value) =>
                {
                    if (value is T typedValue)
                    {
                        updateAction(typedValue);
                    }
                    else
                    {
                        // This case should ideally be caught by ConvertValue, but good to have a fallback
                        Log(
                            $"Type conversion failed for parameter '{parameterName}' during update. Expected {typeof(T)}, got {value?.GetType()}.",
                            LogLevel.Error
                        );
                    }
                };
                Log($"Parameter '{parameterName}' modifier registered successfully.");
            }
            else
            {
                Log(
                    $"Attempted to register modifier for undefined parameter: '{parameterName}'. Ensure it's in SkillprintConfig.",
                    LogLevel.Warning
                );
            }
        }

        /// <summary>
        /// Starts a new Skillprint game session.
        /// </summary>
        /// <param name="customPlayerId">Optional: A custom player identifier if your game uses one.</param>
        public void StartGameSession(string targetMood, string customPlayerId = null)
        {
            if (_isSessionActive)
            {
                Log("Session already active. Call StopGameSession first.", LogLevel.Warning);
                return;
            }
            if (
                config == null
                || string.IsNullOrWhiteSpace(config.ActivePartnerApiKey)
                || string.IsNullOrWhiteSpace(config.ActiveApiBaseUrl)
            )
            {
                Log(
                    "SDK not configured properly (check environment settings). Cannot start session.",
                    LogLevel.Error
                );
                return;
            }

            _currentSessionId = Guid.NewGuid().ToString();
            _isSessionActive = true;
            Log(
                $"Starting game session for {config.targetEnvironment} environment.",
                LogLevel.Info
            );

            // Construct parameter info to send to Skillprint
            var parameterInfos = config
                .gameParameters.Select(p => new API.ParameterInfo
                {
                    name = p.parameterName,
                    type = p.type.ToString(),
                    description = p.description,
                    // Include range if applicable
                    minValue =
                        (p.type == ParameterType.Float || p.type == ParameterType.Integer)
                            ? p.minValue.ToString()
                            : null,
                    maxValue =
                        (p.type == ParameterType.Float || p.type == ParameterType.Integer)
                            ? p.maxValue.ToString()
                            : null,
                })
                .ToList();

            StartCoroutine(
                _apiClient.StartSession(
                    _currentSessionId,
                    targetMood,
                    customPlayerId,
                    config.gameName,
                    parameterInfos,
                    (success, response) =>
                    {
                        if (success)
                        {
                            Log(
                                $"Skillprint session started: {_currentSessionId}. Response: {response}"
                            );
                            // Start SDK processes
                            _screenshotCaptureCoroutine = StartCoroutine(ScreenshotCaptureLoop());
                            _screenshotPostCoroutine = StartCoroutine(ScreenshotPostLoop());
                            _pollResultsCoroutine = StartCoroutine(PollResultsLoop());
                        }
                        else
                        {
                            Log($"Failed to start Skillprint session: {response}", LogLevel.Error);
                            _isSessionActive = false;
                            _currentSessionId = null;
                        }
                    }
                )
            );
        }

        /// <summary>
        /// Stops the current Skillprint game session.
        /// </summary>
        public void StopGameSession()
        {
            if (!_isSessionActive)
            {
                Log("No active session to stop.", LogLevel.Warning);
                return;
            }

            Log($"Stopping Skillprint session: {_currentSessionId}");
            _isSessionActive = false;

            if (_screenshotCaptureCoroutine != null)
                StopCoroutine(_screenshotCaptureCoroutine);
            if (_screenshotPostCoroutine != null)
                StopCoroutine(_screenshotPostCoroutine);
            if (_pollResultsCoroutine != null)
                StopCoroutine(_pollResultsCoroutine);

            _screenshotQueue.ForEach(Destroy); // Clean up any remaining textures
            _screenshotQueue.Clear();

            // Optionally, call an API endpoint to notify Skillprint the session has ended
            // StartCoroutine(_apiClient.EndSession(_currentSessionId, (success, response) => { ... }));

            _currentSessionId = null;
            Log("Skillprint session stopped.");
        }

        private IEnumerator ScreenshotCaptureLoop()
        {
            while (_isSessionActive)
            {
                yield return new WaitForSeconds(config.screenshotIntervalSeconds);
                if (!_isSessionActive)
                    break; // Check again after wait

                yield return _screenshotUtility.CaptureScreenshot(
                    (texture) =>
                    {
                        if (texture != null)
                        {
                            // Keep queue size manageable if posting is slow, or implement a more robust queue
                            if (_screenshotQueue.Count < 50) // Max 50 pending screenshots
                            {
                                _screenshotQueue.Add(texture);
                                Log($"Screenshot captured. Queue size: {_screenshotQueue.Count}");
                            }
                            else
                            {
                                Log(
                                    "Screenshot queue full. Discarding new screenshot.",
                                    LogLevel.Warning
                                );
                                Destroy(texture);
                            }
                        }
                    }
                );
            }
        }

        private IEnumerator ScreenshotPostLoop()
        {
            while (_isSessionActive)
            {
                yield return new WaitForSeconds(config.screenshotPostIntervalSeconds);
                if (!_isSessionActive || _screenshotQueue.Count == 0)
                    continue;

                List<Texture2D> batchToPost = new List<Texture2D>();
                int batchSize = Mathf.CeilToInt(
                    config.screenshotPostIntervalSeconds / config.screenshotIntervalSeconds
                );
                batchSize = Mathf.Max(1, batchSize); // Ensure at least 1

                // Take up to batchSize screenshots from the queue
                // Or just take all available if fewer than batchSize
                int count = Mathf.Min(_screenshotQueue.Count, batchSize * 2); // Send a bit more if available
                for (int i = 0; i < count && _screenshotQueue.Count > 0; i++)
                {
                    batchToPost.Add(_screenshotQueue[0]);
                    _screenshotQueue.RemoveAt(0);
                }

                if (batchToPost.Count > 0)
                {
                    Log($"Posting {batchToPost.Count} screenshots...");
                    StartCoroutine(
                        _apiClient.PostScreenshots(
                            _currentSessionId,
                            batchToPost,
                            false, // TODO send true when session is finalized, otherwise false
                            (success, response) =>
                            {
                                if (success)
                                {
                                    Log(
                                        $"Successfully posted {batchToPost.Count} screenshots. Response: {response}"
                                    );
                                }
                                else
                                {
                                    Log($"Failed to post screenshots: {response}", LogLevel.Error);
                                    // Potentially re-add to queue or handle error, for now, they are "lost"
                                }
                                // Clean up textures that were attempted to be posted
                                batchToPost.ForEach(Destroy);
                            }
                        )
                    );
                }
            }
        }

        private IEnumerator PollResultsLoop()
        {
            while (_isSessionActive)
            {
                yield return new WaitForSeconds(config.pollResultsIntervalSeconds);
                if (!_isSessionActive)
                    break;

                StartCoroutine(
                    _apiClient.PollParameterResults(
                        _currentSessionId,
                        (success, apiResults) =>
                        {
                            if (success && apiResults != null && apiResults.Count > 0)
                            {
                                Log($"Received {apiResults.Count} parameter updates from API.");
                                ApplyParameterUpdates(apiResults);
                            }
                            else if (!success)
                            {
                                Log(
                                    $"Failed to poll results or no new results. Message: {apiResults?.ToString() ?? "No message"}",
                                    LogLevel.Warning
                                );
                            }
                        }
                    )
                );
            }
        }

        private void ApplyParameterUpdates(List<API.ParameterUpdateResult> updates)
        {
            foreach (var update in updates)
            {
                Log(
                    $"[DEBUG] Received update - Name: {update.parameterName}, Value: '{update.newValue}', Type: {(update.newValue == null ? "null" : update.newValue.GetType().ToString())}",
                    LogLevel.Info
                );
                if (
                    _registeredParameters.TryGetValue(
                        update.parameterName,
                        out ParameterDefinition paramDef
                    )
                )
                {
                    if (paramDef.UpdateAction == null)
                    {
                        Log(
                            $"Parameter '{update.parameterName}' received from API but has no registered modifier. Skipping.",
                            LogLevel.Warning
                        );
                        continue;
                    }

                    object convertedValue = paramDef.ConvertValue(update.newValue);

                    if (convertedValue != null && paramDef.IsValid(convertedValue))
                    {
                        try
                        {
                            Log(
                                $"Applying update: {paramDef.parameterName} = {convertedValue} (Type: {paramDef.type})"
                            );
                            paramDef.UpdateAction(convertedValue);
                        }
                        catch (Exception e)
                        {
                            Log(
                                $"Error applying update for {paramDef.parameterName}: {e.Message}",
                                LogLevel.Error
                            );
                        }
                    }
                    else
                    {
                        Log(
                            $"Invalid value or type for parameter {paramDef.parameterName}: '{update.newValue}'. Expected type: {paramDef.type}, Range: {paramDef.minValue}-{paramDef.maxValue}. Skipping.",
                            LogLevel.Warning
                        );
                    }
                }
                else
                {
                    Log(
                        $"Received update for unknown parameter: {update.parameterName}. Skipping.",
                        LogLevel.Warning
                    );
                }
            }
        }

        // Centralized logging
        public enum LogLevel
        {
            Info,
            Warning,
            Error,
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (!config.enableDebugLogging && level == LogLevel.Info)
                return;

            switch (level)
            {
                case LogLevel.Info:
                    Debug.Log($"[SkillprintSDK] {message}");
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning($"[SkillprintSDK] {message}");
                    break;
                case LogLevel.Error:
                    Debug.LogError($"[SkillprintSDK] {message}");
                    break;
            }
        }

        void OnDestroy()
        {
            if (_isSessionActive)
            {
                StopGameSession(); // Attempt to clean up if destroyed unexpectedly
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public SkillprintConfig GetConfig()
        {
            return config;
        }

        // WEBGL specific methods for when game is compiled to Web

        // <summary>
        /// Starts a Skillprint session with automatic URL parameter detection for WebGL builds
        /// </summary>
        /// <param name="fallbackMood">Mood to use if not found in URL parameters (default: "focus")</param>
        /// <param name="fallbackPlayerId">Player ID to use if not found in URL parameters (optional)</param>
        public void StartGameSessionFromUrl(
            string fallbackMood = "relax",
            string fallbackPlayerId = null
        )
        {
            SkillprintSessionHelper.StartSessionWithUrlParams(this, fallbackMood, fallbackPlayerId);
        }

        /// <summary>
        /// Starts a Skillprint session with URL parameter detection and override options
        /// </summary>
        /// <param name="fallbackMood">Mood to use if not found in URL</param>
        /// <param name="fallbackPlayerId">Player ID to use if not found in URL</param>
        /// <param name="overrideMood">Force this mood regardless of URL parameters</param>
        /// <param name="overridePlayerId">Force this player ID regardless of URL parameters</param>
        public void StartGameSessionWithOverrides(
            string fallbackMood = "relax",
            string fallbackPlayerId = null,
            string overrideMood = null,
            string overridePlayerId = null
        )
        {
            SkillprintSessionHelper.StartSessionWithUrlParams(
                this,
                fallbackMood,
                fallbackPlayerId,
                overrideMood,
                overridePlayerId
            );
        }

        /// <summary>
        /// Gets URL parameters for debugging purposes
        /// </summary>
        /// <returns>A string describing current URL parameters</returns>
        public string GetUrlParametersInfo()
        {
            if (!WebGLUrlParameterExtractor.IsUrlParameterSupported())
            {
                return "URL parameters not supported on this platform (not WebGL)";
            }

            string currentUrl = WebGLUrlParameterExtractor.GetCurrentUrl();
            var urlparams = WebGLUrlParameterExtractor.GetSkillprintUrlParameters();

            return $"Current URL: {currentUrl}\nMood Parameter: '{urlparams.targetMood ?? "not found"}'\nPlayer ID Parameter: '{urlparams.playerId ?? "not found"}'";
        }
    }
}

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
        private string _currentUserToken = null;
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
            // If the parameter doesn't exist in _registeredParameters (i.e. not in
            // the ScriptableObject config), auto-create a ParameterDefinition so
            // it is still sent to the backend for auto-provisioning.
            if (!_registeredParameters.TryGetValue(parameterName, out ParameterDefinition paramDef))
            {
                ParameterType inferredType = ParameterType.Float;
                float defaultMin = 0f;
                float defaultMax = 1f;

                if (typeof(T) == typeof(float))
                {
                    inferredType = ParameterType.Float;
                    defaultMin = 0f;
                    defaultMax = 1f;
                }
                else if (typeof(T) == typeof(int))
                {
                    inferredType = ParameterType.Integer;
                    defaultMin = 0f;
                    defaultMax = 100f;
                }
                else if (typeof(T) == typeof(bool))
                {
                    inferredType = ParameterType.Boolean;
                    defaultMin = 0f;
                    defaultMax = 1f;
                }

                paramDef = new ParameterDefinition
                {
                    parameterName = parameterName,
                    description = $"Game parameter '{parameterName}' (auto-detected {inferredType})",
                    type = inferredType,
                    minValue = defaultMin,
                    maxValue = defaultMax,
                };

                _registeredParameters.Add(parameterName, paramDef);
                Log(
                    $"Parameter '{parameterName}' was not in SkillprintConfig. Auto-created with type={inferredType}. " +
                    $"Consider adding it to the config for proper min/max/description.",
                    LogLevel.Warning
                );
            }

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
                    Log(
                        $"Type conversion failed for parameter '{parameterName}' during update. Expected {typeof(T)}, got {value?.GetType()}.",
                        LogLevel.Error
                    );
                }
            };
            Log($"Parameter '{parameterName}' modifier registered successfully.");
        }

        /// <summary>
        /// Registers a parameter modifier with a custom description.
        /// Use this overload when registering parameters at runtime that are not
        /// defined in the SkillprintConfig ScriptableObject.
        /// </summary>
        /// <typeparam name="T">The type of the parameter (float, int, bool).</typeparam>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="updateAction">The Action to call when this parameter needs to be updated.</param>
        /// <param name="description">A description of what this game variable controls.</param>
        /// <param name="howItWorks">Optional: Explanation of how the parameter affects gameplay.</param>
        public void RegisterParameterModifier<T>(
            string parameterName,
            Action<T> updateAction,
            string description,
            string howItWorks = null)
        {
            // Register using the base overload (which auto-creates if needed)
            RegisterParameterModifier<T>(parameterName, updateAction);

            // Enrich the definition with the provided description/guide
            if (_registeredParameters.TryGetValue(parameterName, out ParameterDefinition paramDef))
            {
                if (!string.IsNullOrEmpty(description))
                    paramDef.description = description;
                if (!string.IsNullOrEmpty(howItWorks))
                    paramDef.howSDKChangesIt = howItWorks;
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

            // Construct parameter info from _registeredParameters, which is the
            // union of config.gameParameters AND any runtime-registered params.
            // This ensures the backend always receives the full parameter set.
            var parameterInfos = _registeredParameters.Values
                .Select(p => new API.ParameterInfo
                {
                    name = p.parameterName,
                    type = p.type.ToString(),
                    description = p.description,
                    adjustmentGuide = p.howSDKChangesIt,
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
            Log(
                $"Sending {parameterInfos.Count} game parameter(s) to API: " +
                string.Join(", ", parameterInfos.Select(p => p.name)),
                LogLevel.Warning
            );

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
        /// Sends any remaining screenshots and the is_last_chunk=true signal
        /// so the backend closes the session and triggers final scoring.
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

            // Send remaining screenshots (if any) with is_last_chunk=true
            // to signal the backend to close the session and trigger final scoring.
            string closingSessionId = _currentSessionId;
            List<Texture2D> finalBatch = new List<Texture2D>(_screenshotQueue);
            _screenshotQueue.Clear();

            StartCoroutine(
                _apiClient.PostScreenshots(
                    closingSessionId,
                    finalBatch,
                    true, // is_last_chunk = true signals session end
                    (success, response) =>
                    {
                        if (success)
                        {
                            Log(
                                $"Session close signal sent successfully. Response: {response}"
                            );
                        }
                        else
                        {
                            Log(
                                $"Failed to send session close signal: {response}",
                                LogLevel.Error
                            );
                        }
                        // Clean up textures after the request completes
                        finalBatch.ForEach(Destroy);
                    },
                    config.screenshotJpegQuality
                )
            );

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
                    },
                    config.screenshotMaxWidth
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
                            },
                            config.screenshotJpegQuality
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
                // Use GetParsedValue() which handles null, empty, and
                // alternative JSON formats — update.newValue alone is
                // unreliable because JsonUtility can't deserialize `object`.
                object parsedValue = update.GetParsedValue();

                Log(
                    $"[DEBUG] Received update - Name: {update.parameterName}, " +
                    $"RawValue: '{update.newValue}', ParsedValue: '{parsedValue}', " +
                    $"Type: {(parsedValue == null ? "null" : parsedValue.GetType().ToString())}",
                    LogLevel.Info
                );

                if (parsedValue == null)
                {
                    Log(
                        $"Parameter '{update.parameterName}' received null value from API. Skipping.",
                        LogLevel.Warning
                    );
                    continue;
                }

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

                    object convertedValue = paramDef.ConvertValue(parsedValue);

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
                            $"Invalid value or type for parameter {paramDef.parameterName}: '{parsedValue}'. " +
                            $"Converted: '{convertedValue}'. Expected type: {paramDef.type}, " +
                            $"Range: {paramDef.minValue}-{paramDef.maxValue}. Skipping.",
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
        /// Retrieves the user profile from the Skillprint API.
        /// </summary>
        /// <param name="customPlayerId">The partner's player ID.</param>
        /// <param name="callback">Callback returns success status and the parsed UserProfileResponse (or null on failure).</param>
        public void GetUserProfile(string customPlayerId, Action<bool, API.UserProfileResponse> callback)
        {
            StartCoroutine(GetUserProfileCoroutine(customPlayerId, callback));
        }

        private IEnumerator GetUserProfileCoroutine(string customPlayerId, Action<bool, API.UserProfileResponse> callback)
        {
            string token = _currentUserToken;
            if (string.IsNullOrEmpty(token))
            {
                bool tokenComplete = false;
                yield return _apiClient.CreateOrGetUserToken(customPlayerId, (success, result) =>
                {
                    if (success)
                    {
                        token = result;
                        _currentUserToken = result; // cache it
                    }
                    tokenComplete = true;
                });

                while (!tokenComplete)
                {
                    yield return null;
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                Log("Failed to obtain user token for profile retrieval.", LogLevel.Error);
                callback(false, null);
                yield break;
            }

            yield return _apiClient.GetUserProfile(token, (success, jsonResponse) =>
            {
                if (success)
                {
                    try
                    {
                        API.UserProfileResponse response = JsonUtility.FromJson<API.UserProfileResponse>(jsonResponse);
                        callback(true, response);
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to parse UserProfileResponse: {e.Message}", LogLevel.Error);
                        callback(false, null);
                    }
                }
                else
                {
                    callback(false, null);
                }
            });
        }

        /// <summary>
        /// Retrieves the skill progression from the Skillprint API.
        /// </summary>
        /// <param name="customPlayerId">The partner's player ID.</param>
        /// <param name="callback">Callback returns success status and the parsed SkillProgressionResponse (or null on failure).</param>
        public void GetSkillProgression(string customPlayerId, Action<bool, API.SkillProgressionResponse> callback)
        {
            StartCoroutine(GetSkillProgressionCoroutine(customPlayerId, callback));
        }

        private IEnumerator GetSkillProgressionCoroutine(string customPlayerId, Action<bool, API.SkillProgressionResponse> callback)
        {
            string token = _currentUserToken;
            if (string.IsNullOrEmpty(token))
            {
                bool tokenComplete = false;
                yield return _apiClient.CreateOrGetUserToken(customPlayerId, (success, result) =>
                {
                    if (success)
                    {
                        token = result;
                        _currentUserToken = result; // cache it
                    }
                    tokenComplete = true;
                });

                while (!tokenComplete)
                {
                    yield return null;
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                Log("Failed to obtain user token for skill progression retrieval.", LogLevel.Error);
                callback(false, null);
                yield break;
            }

            yield return _apiClient.GetSkillProgression(token, (success, jsonResponse) =>
            {
                if (success)
                {
                    try
                    {
                        API.SkillProgressionResponse response = JsonUtility.FromJson<API.SkillProgressionResponse>(jsonResponse);
                        callback(true, response);
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to parse SkillProgressionResponse: {e.Message}", LogLevel.Error);
                        callback(false, null);
                    }
                }
                else
                {
                    callback(false, null);
                }
            });
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

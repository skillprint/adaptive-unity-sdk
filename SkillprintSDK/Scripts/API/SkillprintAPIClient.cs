using System; // For Action
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text; // For Encoding
using UnityEngine;
using UnityEngine.Networking; // For UnityWebRequest

namespace Skillprint.SDK.API
{
    public class SkillprintAPIClient
    {
        private string _baseUrl;
        private string _partnerApiKey;
        private Action<string, SkillprintManager.LogLevel> _logger;

        // API Endpoints
        private const string START_SESSION_ENDPOINT = "/games/api/sessions/";
        private const string UPLOAD_SCREENSHOTS_ENDPOINT = "/games/api/record-session/{sessionId}/";
        private const string POLL_RESULTS_ENDPOINT = "/games/api/sessions/{sessionId}/";
        private const string ADD_TELEMETRY_ENDPOINT = "/games/api/sessions/telemetry";
        private const string CREATE_USER_ENDPOINT = "/partners/api/users/add/";
        private const string GET_USER_TOKEN_ENDPOINT = "/partners/api/users/auth/token/";
        private const string GET_USER_PROFILE_ENDPOINT = "/scoring/api/profiles/";
        private const string GET_SKILL_PROGRESSION_ENDPOINT = "/scoring/api/skill-progression/";

        // Request configuration
        private const int REQUEST_TIMEOUT_SECONDS = 10;
        private const int UPLOAD_TIMEOUT_SECONDS = 30;
        private const int MAX_UPLOAD_RETRIES = 2;
        private const float RETRY_BASE_DELAY_SECONDS = 1.0f;

        public SkillprintAPIClient(
            string baseUrl,
            string partnerApiKey,
            Action<string, SkillprintManager.LogLevel> logger
        )
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _partnerApiKey = partnerApiKey;
            _logger = logger;
        }

        public IEnumerator StartSession(
            string sessionId,
            string targetMood,
            string customPlayerId,
            string gameName,
            List<ParameterInfo> gameParameters,
            Action<bool, string> callback
        )
        {
            // TODO: Create or obtain user token using customPlayerId
            string url = _baseUrl + START_SESSION_ENDPOINT;
            _logger?.Invoke($"Starting session: POST {url}", SkillprintManager.LogLevel.Info);
            _logger?.Invoke(
                $"Starting session: MOOD {targetMood}",
                SkillprintManager.LogLevel.Warning
            );
            if (
                string.IsNullOrEmpty(targetMood)
                || !Enum.GetNames(typeof(Mood)).Contains(targetMood)
            )
            {
                string validMoods = string.Join(", ", Enum.GetNames(typeof(Mood)));
                string errorMessage =
                    $"Invalid targetMood: '{targetMood}'. Valid moods are: {validMoods}.";
                _logger?.Invoke(errorMessage, SkillprintManager.LogLevel.Error);
                callback(false, errorMessage);
                yield break; // Stop further execution
            }

            // Create or get user token if customPlayerId is provided
            string userToken = null;
            if (!string.IsNullOrEmpty(customPlayerId))
            {
                bool tokenRetrieved = false;
                string tokenResult = null;

                yield return CreateOrGetUserToken(
                    customPlayerId,
                    (success, result) =>
                    {
                        tokenRetrieved = true;
                        if (success)
                        {
                            userToken = result;
                            _logger?.Invoke(
                                $"User token obtained successfully for player: {customPlayerId}",
                                SkillprintManager.LogLevel.Info
                            );
                        }
                        else
                        {
                            tokenResult = result;
                            _logger?.Invoke(
                                $"Failed to obtain user token: {result}",
                                SkillprintManager.LogLevel.Error
                            );
                        }
                    }
                );

                // Wait for token retrieval to complete
                while (!tokenRetrieved)
                {
                    yield return null;
                }

                // If token retrieval failed, we can still continue with session creation
                // but log the issue
                if (string.IsNullOrEmpty(userToken))
                {
                    _logger?.Invoke(
                        $"Proceeding with session creation without user token. Error: {tokenResult}",
                        SkillprintManager.LogLevel.Warning
                    );
                }
            }

            StartSessionRequest requestData = new StartSessionRequest
            {
                sessionId = sessionId,
                game = gameName,
                targetMood = targetMood,
                gameParameters = gameParameters
            };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                // If userToken is available, set it in the Authorization header
                if (!string.IsNullOrEmpty(userToken))
                {
                    webRequest.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                }

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"StartSession successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    // Assuming API returns a simple success message or session ID. Parse if needed.
                    // BaseResponse response = JsonUtility.FromJson<BaseResponse>(webRequest.downloadHandler.text);
                    // callback(response.success, response.message);
                    callback(true, webRequest.downloadHandler.text); // Simplified
                }
                else
                {
                    _logger?.Invoke(
                        $"StartSession Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        public IEnumerator PostScreenshots(
            string sessionId,
            List<Texture2D> screenshots,
            bool isLastChunk, // isLastChunk parameter must be sent when closing the session.
            Action<bool, string> callback,
            int jpegQuality = 75 // Default to Unity's default quality for backward compatibility
        )
        {
            // TODO: Avoid sending duplicated screenshots in the same batch or from the previous one. This is to avoid processing gameplay when user is in a menu or still.
            string url =
                $"{_baseUrl}{UPLOAD_SCREENSHOTS_ENDPOINT.Replace("{sessionId}", sessionId)}";
            _logger?.Invoke(
                $"Posting {screenshots.Count} screenshots (isLastChunk: {isLastChunk}): POST {url}",
                SkillprintManager.LogLevel.Info
            );

            // If isLastChunk is false, screenshots should not be empty.
            // If isLastChunk is true, it might be permissible to send no screenshots (e.g. signaling end).
            if (screenshots.Count == 0 && !isLastChunk)
            {
                string errorMsg =
                    "No screenshots provided, and 'is_last_chunk' is false. API likely requires files in this case.";
                _logger?.Invoke(errorMsg, SkillprintManager.LogLevel.Warning);
                callback(false, errorMsg);
                yield break;
            }

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

            // Add the is_last_chunk field (as text/plain)
            // API expects boolean true/false as string.
            formData.Add(
                new MultipartFormDataSection("is_last_chunk", isLastChunk.ToString().ToLower())
            );

            for (int i = 0; i < screenshots.Count; i++)
            {
                if (screenshots[i] == null)
                {
                    _logger?.Invoke(
                        $"Screenshot at index {i} is null, skipping.",
                        SkillprintManager.LogLevel.Warning
                    );
                    continue;
                }
                byte[] jpgData = screenshots[i].EncodeToJPG(jpegQuality);
                if (jpgData == null || jpgData.Length == 0)
                {
                    _logger?.Invoke(
                        $"Failed to encode screenshot {i} to JPG or data is empty.",
                        SkillprintManager.LogLevel.Warning
                    );
                    continue;
                }

                // MODIFIED: Use "screenshot0", "screenshot1", etc. as field names
                string formFieldName = $"screenshot{i}";
                // The filename for Content-Disposition, can be simple.
                string filenameForUpload = $"screenshot_{i}.jpg";

                formData.Add(
                    new MultipartFormFileSection(
                        formFieldName,
                        jpgData,
                        filenameForUpload,
                        "image/jpeg"
                    )
                );
            }

            // After adding is_last_chunk, if there are no actual image files added (e.g. all were null or failed to encode)
            // AND it's not the last chunk, then it's an issue.
            // formData will have 1 element if only is_last_chunk was added.
            if (formData.Count <= 1 && !isLastChunk && screenshots.Count > 0) // screenshots.Count > 0 means we intended to send images
            {
                _logger?.Invoke(
                    "All provided screenshots were invalid (null or failed to encode), and 'is_last_chunk' is false.",
                    SkillprintManager.LogLevel.Warning
                );
                callback(false, "No valid images to post, and not the last chunk.");
                yield break;
            }
            // If formData.Count == 1 and isLastChunk is TRUE, it's fine (sending only is_last_chunk=true)

            _logger?.Invoke(
                $"Form Data prepared. Number of parts: {formData.Count}",
                SkillprintManager.LogLevel.Info
            );

            // Retry loop for screenshot uploads — network blips shouldn't lose gameplay data
            int attempt = 0;
            bool succeeded = false;
            string lastError = null;

            while (attempt <= MAX_UPLOAD_RETRIES && !succeeded)
            {
                if (attempt > 0)
                {
                    float delay = RETRY_BASE_DELAY_SECONDS * Mathf.Pow(2, attempt - 1);
                    _logger?.Invoke(
                        $"Retrying screenshot upload (attempt {attempt + 1}/{MAX_UPLOAD_RETRIES + 1}) after {delay}s...",
                        SkillprintManager.LogLevel.Warning
                    );
                    yield return new WaitForSeconds(delay);

                    // Rebuild form data for retry (UnityWebRequest can't be reused)
                    formData = new List<IMultipartFormSection>();
                    formData.Add(
                        new MultipartFormDataSection("is_last_chunk", isLastChunk.ToString().ToLower())
                    );
                    for (int j = 0; j < screenshots.Count; j++)
                    {
                        if (screenshots[j] == null) continue;
                        byte[] retryJpgData = screenshots[j].EncodeToJPG();
                        if (retryJpgData == null || retryJpgData.Length == 0) continue;
                        formData.Add(
                            new MultipartFormFileSection(
                                $"screenshot{j}", retryJpgData, $"screenshot_{j}.jpg", "image/jpeg"
                            )
                        );
                    }
                }

                using (UnityWebRequest webRequest = UnityWebRequest.Post(url, formData))
                {
                    webRequest.timeout = UPLOAD_TIMEOUT_SECONDS;
                    webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                    // UnityWebRequest.Post usually sets the Content-Type for multipart/form-data automatically,
                    // including the boundary.

                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        _logger?.Invoke(
                            $"PostScreenshots successful. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Info
                        );
                        succeeded = true;
                        callback(true, webRequest.downloadHandler.text);
                    }
                    else
                    {
                        lastError = webRequest.error + " | " + webRequest.downloadHandler.text;
                        _logger?.Invoke(
                            $"PostScreenshots Error (attempt {attempt + 1}): {webRequest.error}. Response Code: {webRequest.responseCode}. Details: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                    }
                }

                attempt++;
            }

            if (!succeeded)
            {
                _logger?.Invoke(
                    $"PostScreenshots failed after {MAX_UPLOAD_RETRIES + 1} attempts. Last error: {lastError}",
                    SkillprintManager.LogLevel.Error
                );
                callback(false, lastError);
            }
        }

        /// <summary>
        /// Appends a discrete telemetry event to an active session. Mirrors
        /// skillprint-js-sdk's logTelemetryEvent: session_id and game_slug
        /// go in the query string (not the body), and the event object is
        /// wrapped in an outer {"event": ...} envelope -- confirmed against
        /// marketplace's games/tests/test_sessions.py, which posts
        /// {"event": {"a": 1, "timestamp": ...}} and asserts
        /// session.telemetry becomes [{"a": 1, "timestamp": ...}]. Sending
        /// the event unwrapped would store just its "event" field's string
        /// value as the whole telemetry entry.
        /// </summary>
        /// <param name="eventJson">
        /// The inner event object JSON (e.g. {"event":"LEVEL_START","level":3}),
        /// built by TelemetryEventJsonBuilder.Build.
        /// </param>
        public IEnumerator LogTelemetryEvent(
            string sessionId,
            string gameSlug,
            string eventJson,
            Action<bool, string> callback
        )
        {
            string url =
                $"{_baseUrl}{ADD_TELEMETRY_ENDPOINT}"
                + $"?session_id={UnityWebRequest.EscapeURL(sessionId)}"
                + $"&game_slug={UnityWebRequest.EscapeURL(gameSlug)}";
            string jsonBody = "{\"event\":" + eventJson + "}";
            _logger?.Invoke(
                $"Logging telemetry event: POST {url} body={jsonBody}",
                SkillprintManager.LogLevel.Info
            );

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"LogTelemetryEvent successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"LogTelemetryEvent Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Warning
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        public IEnumerator PollParameterResults(
            string sessionId,
            Action<bool, List<ParameterUpdateResult>> callback
        )
        {
            string url = $"{_baseUrl}{POLL_RESULTS_ENDPOINT.Replace("{sessionId}", sessionId)}";
            _logger?.Invoke($"Polling results: GET {url}", SkillprintManager.LogLevel.Info);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    _logger?.Invoke(
                        $"PollResults successful. Response: {responseText}",
                        SkillprintManager.LogLevel.Info
                    );
                    try
                    {
                        // Use the enhanced parser to handle various response formats
                        List<ParameterUpdateResult> parameterUpdates =
                            ParameterUpdateParser.ParseFromJson(responseText);

                        if (parameterUpdates != null && parameterUpdates.Count > 0)
                        {
                            _logger?.Invoke(
                                $"Successfully parsed {parameterUpdates.Count} parameter updates",
                                SkillprintManager.LogLevel.Info
                            );
                            _logger?.Invoke(
                                $"{parameterUpdates}",
                                SkillprintManager.LogLevel.Warning
                            );

                            // Log each parameter update for debugging
                            foreach (var update in parameterUpdates)
                            {
                                object parsedValue = update.GetParsedValue();
                                _logger?.Invoke(
                                    $"Parameter Update: {update.parameterName} = {parsedValue} (Type: {parsedValue?.GetType().Name ?? "null"})",
                                    SkillprintManager.LogLevel.Info
                                );
                            }

                            callback(true, parameterUpdates);
                        }
                        else
                        {
                            _logger?.Invoke(
                                "No parameter updates found in response",
                                SkillprintManager.LogLevel.Warning
                            );
                            callback(true, new List<ParameterUpdateResult>()); // Return empty list, not null
                        }
                    }
                    catch (Exception e)
                    {
                        _logger?.Invoke(
                            $"PollResults JSON parsing error: {e.Message}. Response: {responseText}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, null);
                    }
                }
                else
                {
                    _logger?.Invoke(
                        $"PollResults Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, null);
                }
            }
        }

        /// <summary>
        /// Creates a new user with the given internal ID, or gets an existing user's token
        /// </summary>
        /// <param name="customPlayerId">The partner's internal player ID</param>
        /// <param name="callback">Callback with success status and user token (or error message)</param>
        /// <returns></returns>
        public IEnumerator CreateOrGetUserToken(
            string customPlayerId,
            Action<bool, string> callback
        )
        {
            if (string.IsNullOrEmpty(customPlayerId))
            {
                callback(false, "Custom player ID cannot be null or empty");
                yield break;
            }

            // First, try to get an existing user token
            bool tokenAttemptComplete = false;
            bool tokenSuccess = false;
            string tokenResult = null;

            yield return GetUserToken(
                customPlayerId,
                (success, result) =>
                {
                    tokenAttemptComplete = true;
                    tokenSuccess = success;
                    tokenResult = result;
                }
            );

            // Wait for the token attempt to complete
            while (!tokenAttemptComplete)
            {
                yield return null;
            }

            if (tokenSuccess)
            {
                // User exists, token retrieved successfully
                callback(true, tokenResult);
            }
            else
            {
                // User doesn't exist or token retrieval failed, try to create user
                bool createAttemptComplete = false;
                bool createSuccess = false;
                string createResult = null;

                yield return CreateUser(
                    customPlayerId,
                    (success, result) =>
                    {
                        createAttemptComplete = true;
                        createSuccess = success;
                        createResult = result;
                    }
                );

                // Wait for the create attempt to complete
                while (!createAttemptComplete)
                {
                    yield return null;
                }

                if (createSuccess)
                {
                    // User created successfully, now get token
                    bool newTokenAttemptComplete = false;
                    bool newTokenSuccess = false;
                    string newTokenResult = null;

                    yield return GetUserToken(
                        customPlayerId,
                        (success, result) =>
                        {
                            newTokenAttemptComplete = true;
                            newTokenSuccess = success;
                            newTokenResult = result;
                        }
                    );

                    // Wait for the new token attempt to complete
                    while (!newTokenAttemptComplete)
                    {
                        yield return null;
                    }

                    if (newTokenSuccess)
                    {
                        callback(true, newTokenResult);
                    }
                    else
                    {
                        callback(false, $"User created but failed to get token: {newTokenResult}");
                    }
                }
                else
                {
                    callback(false, $"Failed to create user: {createResult}");
                }
            }
        }

        /// <summary>
        /// Creates a new user with the given internal ID
        /// </summary>
        private IEnumerator CreateUser(string internalId, Action<bool, string> callback)
        {
            string url = _baseUrl + CREATE_USER_ENDPOINT;
            _logger?.Invoke(
                $"Creating user: POST {url} with internalId: {internalId}",
                SkillprintManager.LogLevel.Info
            );

            CreateUserRequest requestData = new CreateUserRequest { internalId = internalId };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"CreateUser successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"CreateUser Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        /// <summary>
        /// Gets an authentication token for an existing user
        /// </summary>
        private IEnumerator GetUserToken(string internalId, Action<bool, string> callback)
        {
            string url = _baseUrl + GET_USER_TOKEN_ENDPOINT;
            _logger?.Invoke(
                $"Getting user token: POST {url} with internalId: {internalId}",
                SkillprintManager.LogLevel.Info
            );

            GetUserTokenRequest requestData = new GetUserTokenRequest { internalId = internalId };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"GetUserToken successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );

                    try
                    {
                        // Parse the response to extract the token
                        GetUserTokenResponse tokenResponse =
                            JsonUtility.FromJson<GetUserTokenResponse>(
                                webRequest.downloadHandler.text
                            );
                        if (!string.IsNullOrEmpty(tokenResponse.token))
                        {
                            callback(true, tokenResponse.token);
                        }
                        else
                        {
                            callback(false, "Token not found in response");
                        }
                    }
                    catch (System.Exception e)
                    {
                        _logger?.Invoke(
                            $"Failed to parse token response: {e.Message}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, "Failed to parse token response");
                    }
                }
                else
                {
                    _logger?.Invoke(
                        $"GetUserToken Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        /// <summary>
        /// Retrieves the user profile information.
        /// </summary>
        public IEnumerator GetUserProfile(
            string userToken,
            Action<bool, string> callback
        )
        {
            string url = _baseUrl + GET_USER_PROFILE_ENDPOINT;
            _logger?.Invoke($"Getting user profile: GET {url}", SkillprintManager.LogLevel.Info);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                if (!string.IsNullOrEmpty(userToken))
                {
                    webRequest.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                }
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"GetUserProfile successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"GetUserProfile Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        /// <summary>
        /// Retrieves the skill progression information.
        /// </summary>
        public IEnumerator GetSkillProgression(
            string userToken,
            Action<bool, string> callback
        )
        {
            string url = _baseUrl + GET_SKILL_PROGRESSION_ENDPOINT;
            _logger?.Invoke($"Getting skill progression: GET {url}", SkillprintManager.LogLevel.Info);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                if (!string.IsNullOrEmpty(userToken))
                {
                    webRequest.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                }
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"GetSkillProgression successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"GetSkillProgression Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        /// <summary>
        /// Retrieves the mood visualization information.
        /// </summary>
        public IEnumerator GetMoodVisualization(
            string userToken,
            Action<bool, string> callback
        )
        {
            string url = _baseUrl + "/scoring/api/mood-visualization/";
            _logger?.Invoke($"Getting mood visualization: GET {url}", SkillprintManager.LogLevel.Info);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = REQUEST_TIMEOUT_SECONDS;
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
                if (!string.IsNullOrEmpty(userToken))
                {
                    webRequest.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                }
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke(
                        $"GetMoodVisualization successful. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Info
                    );
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"GetMoodVisualization Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
                    );
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        [System.Serializable]
        public class CreateUserRequest
        {
            public string internalId;
        }

        [System.Serializable]
        public class GetUserTokenRequest
        {
            public string internalId;
        }

        [System.Serializable]
        public class GetUserTokenResponse
        {
            public string token;
            public string expiry;
            // User data can be added here if needed
        }
    }
}

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
        private readonly SkillprintAuth _auth;
        private Action<string, SkillprintManager.LogLevel> _logger;

        // API Endpoints
        private const string START_SESSION_ENDPOINT = "/games/api/sessions/";
        private const string UPLOAD_SCREENSHOTS_ENDPOINT = "/games/api/record-session/{sessionId}/";
        private const string POLL_RESULTS_ENDPOINT = "/games/api/sessions/{sessionId}/";
        private const string CREATE_USER_ENDPOINT = "/partners/api/users/add/";
        private const string GET_USER_TOKEN_ENDPOINT = "/partners/api/users/auth/token/";
        private const string GET_USER_PROFILE_ENDPOINT = "/scoring/api/profiles/";
        private const string GET_SKILL_PROGRESSION_ENDPOINT = "/scoring/api/skill-progression/";
        private const string GET_MOOD_VISUALIZATION_ENDPOINT = "/scoring/api/mood-visualization/";

        // Request configuration
        private const int REQUEST_TIMEOUT_SECONDS = 10;
        private const int UPLOAD_TIMEOUT_SECONDS = 30;
        private const int MAX_UPLOAD_RETRIES = 2;
        private const float RETRY_BASE_DELAY_SECONDS = 1.0f;

        private const string TOKEN_MODE_NO_PARTNER_ENDPOINTS =
            "Not available in TokenProvider auth mode: your backend provisions the player when it mints the SDK token.";

        /// <summary>
        /// Legacy constructor: authenticates every request with the partner API key.
        /// </summary>
        public SkillprintAPIClient(
            string baseUrl,
            string partnerApiKey,
            Action<string, SkillprintManager.LogLevel> logger
        )
            : this(baseUrl, new SkillprintAuth(SkillprintAuthMode.PartnerApiKey, partnerApiKey, logger), logger)
        {
        }

        public SkillprintAPIClient(
            string baseUrl,
            SkillprintAuth auth,
            Action<string, SkillprintManager.LogLevel> logger
        )
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _auth = auth;
            _logger = logger;
        }

        /// <summary>The credential this client authenticates with.</summary>
        public SkillprintAuth Auth => _auth;

        // ── Request plumbing ────────────────────────────────────────────────

        /// <summary>
        /// Ensures a credential is ready, builds and sends the request with it, and — in
        /// token mode — refreshes the token and retries once on a 401. <paramref name="onResponse"/>
        /// receives the finished request (disposed afterwards); <paramref name="onAuthError"/>
        /// is called instead when no credential could be obtained.
        /// </summary>
        private IEnumerator SendAuthorized(
            Func<UnityWebRequest> build,
            Action<UnityWebRequest> onResponse,
            Action<string> onAuthError
        )
        {
            bool ready = false;
            string authError = null;
            yield return _auth.EnsureReady(false, (ok, err) =>
            {
                ready = ok;
                authError = err;
            });
            if (!ready)
            {
                onAuthError(authError);
                yield break;
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                using (UnityWebRequest request = build())
                {
                    _auth.Apply(request);
                    yield return request.SendWebRequest();

                    bool unauthorized = request.responseCode == 401;
                    if (unauthorized && _auth.UsesToken && _auth.HasProvider && attempt == 0)
                    {
                        _logger?.Invoke(
                            "SDK token rejected (401); refreshing and retrying once.",
                            SkillprintManager.LogLevel.Warning
                        );
                        _auth.Invalidate();
                        bool refreshed = false;
                        string refreshError = null;
                        yield return _auth.EnsureReady(true, (ok, err) =>
                        {
                            refreshed = ok;
                            refreshError = err;
                        });
                        if (!refreshed)
                        {
                            onAuthError(refreshError);
                            yield break;
                        }
                        continue;
                    }

                    onResponse(request);
                    yield break;
                }
            }
        }

        private static UnityWebRequest JsonRequest(string url, string method, string json)
        {
            var request = new UnityWebRequest(url, method);
            request.timeout = REQUEST_TIMEOUT_SECONDS;
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            return request;
        }

        private static string ErrorText(UnityWebRequest request)
        {
            return request.error + " | " + (request.downloadHandler != null ? request.downloadHandler.text : "");
        }

        // ── Sessions ────────────────────────────────────────────────────────

        public IEnumerator StartSession(
            string sessionId,
            string targetMood,
            string targetSkill,
            string optimizationTarget,
            string customPlayerId,
            string gameName,
            List<ParameterInfo> gameParameters,
            Action<bool, string> callback
        )
        {
            string url = _baseUrl + START_SESSION_ENDPOINT;
            _logger?.Invoke($"Starting session: POST {url}", SkillprintManager.LogLevel.Info);
            _logger?.Invoke(
                $"Starting session: MOOD {targetMood}, SKILL {(string.IsNullOrEmpty(targetSkill) ? "none" : targetSkill)}, OPTIMIZE FOR {optimizationTarget}",
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

            // In token mode the player is whoever the SDK token was minted for, so there is
            // no user token to look up; customPlayerId only tells the provider who is playing.
            string userToken = null;
            if (_auth.UsesToken)
            {
                _auth.SetPlayer(customPlayerId);
            }
            else if (!string.IsNullOrEmpty(customPlayerId))
            {
                string tokenResult = null;

                yield return CreateOrGetUserToken(
                    customPlayerId,
                    (success, result) =>
                    {
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
                targetSkill = targetSkill,
                optimizationTarget = string.IsNullOrEmpty(optimizationTarget) ? "MOOD" : optimizationTarget,
                gameParameters = gameParameters
            };
            string jsonData = JsonUtility.ToJson(requestData);
            if (string.IsNullOrEmpty(targetSkill))
            {
                // JsonUtility writes null strings as "", which fails the backend's slug validation.
                jsonData = jsonData.Replace("\"targetSkill\":\"\",", "");
            }

            yield return SendAuthorized(
                () =>
                {
                    var request = JsonRequest(url, "POST", jsonData);
                    if (!string.IsNullOrEmpty(userToken))
                    {
                        request.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                    }
                    return request;
                },
                webRequest =>
                {
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        _logger?.Invoke(
                            $"StartSession successful. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Info
                        );
                        callback(true, webRequest.downloadHandler.text);
                    }
                    else
                    {
                        _logger?.Invoke(
                            $"StartSession Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, ErrorText(webRequest));
                    }
                },
                authError => callback(false, authError)
            );
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

            // Encode once; every attempt reuses the same JPEG bytes.
            var encoded = new List<KeyValuePair<int, byte[]>>();
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
                encoded.Add(new KeyValuePair<int, byte[]>(i, jpgData));
            }

            // If there are no actual image files (all null or failed to encode) and it's not
            // the last chunk, there is nothing to send.
            if (encoded.Count == 0 && !isLastChunk && screenshots.Count > 0)
            {
                _logger?.Invoke(
                    "All provided screenshots were invalid (null or failed to encode), and 'is_last_chunk' is false.",
                    SkillprintManager.LogLevel.Warning
                );
                callback(false, "No valid images to post, and not the last chunk.");
                yield break;
            }

            Func<UnityWebRequest> build = () =>
            {
                // UnityWebRequest can't be reused, so the form is rebuilt for every attempt.
                var formData = new List<IMultipartFormSection>
                {
                    // API expects boolean true/false as string.
                    new MultipartFormDataSection("is_last_chunk", isLastChunk.ToString().ToLower())
                };
                foreach (var shot in encoded)
                {
                    // Field names "screenshot0", "screenshot1", ...
                    formData.Add(
                        new MultipartFormFileSection(
                            $"screenshot{shot.Key}",
                            shot.Value,
                            $"screenshot_{shot.Key}.jpg",
                            "image/jpeg"
                        )
                    );
                }
                // UnityWebRequest.Post sets the multipart Content-Type and boundary.
                var request = UnityWebRequest.Post(url, formData);
                request.timeout = UPLOAD_TIMEOUT_SECONDS;
                return request;
            };

            _logger?.Invoke(
                $"Form Data prepared. Number of images: {encoded.Count}",
                SkillprintManager.LogLevel.Info
            );

            // Retry loop for screenshot uploads — network blips shouldn't lose gameplay data
            int attempt = 0;
            bool succeeded = false;
            bool authFailed = false;
            string lastError = null;

            while (attempt <= MAX_UPLOAD_RETRIES && !succeeded && !authFailed)
            {
                if (attempt > 0)
                {
                    float delay = RETRY_BASE_DELAY_SECONDS * Mathf.Pow(2, attempt - 1);
                    _logger?.Invoke(
                        $"Retrying screenshot upload (attempt {attempt + 1}/{MAX_UPLOAD_RETRIES + 1}) after {delay}s...",
                        SkillprintManager.LogLevel.Warning
                    );
                    yield return new WaitForSeconds(delay);
                }

                int attemptNumber = attempt + 1;
                yield return SendAuthorized(
                    build,
                    webRequest =>
                    {
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
                            lastError = ErrorText(webRequest);
                            _logger?.Invoke(
                                $"PostScreenshots Error (attempt {attemptNumber}): {webRequest.error}. Response Code: {webRequest.responseCode}. Details: {webRequest.downloadHandler.text}",
                                SkillprintManager.LogLevel.Error
                            );
                        }
                    },
                    authError =>
                    {
                        // No credential: retrying the upload won't help.
                        authFailed = true;
                        lastError = authError;
                    }
                );

                attempt++;
            }

            if (!succeeded)
            {
                _logger?.Invoke(
                    $"PostScreenshots failed after {attempt} attempt(s). Last error: {lastError}",
                    SkillprintManager.LogLevel.Error
                );
                callback(false, lastError);
            }
        }

        /// <summary>
        /// Uploads a slice of play described in text instead of screenshots: a <paramref name="digest"/>
        /// the backend's observer reads in text mode (and that the controller uses to decide the next
        /// parameter values). Use it at natural boundaries such as the end of a wave or level, with the
        /// measured outcome of that slice. <paramref name="gameStatesJson"/> is an optional JSON array of
        /// state objects (e.g. <c>[{"score": 12}]</c>); a <c>score</c> in the last one updates the session score.
        /// </summary>
        public IEnumerator PostTextChunk(
            string sessionId,
            string digest,
            string gameStatesJson,
            int durationMs,
            Action<bool, string> callback
        )
        {
            string url =
                $"{_baseUrl}{UPLOAD_SCREENSHOTS_ENDPOINT.Replace("{sessionId}", sessionId)}";
            if (string.IsNullOrWhiteSpace(digest))
            {
                callback(false, "A text chunk needs a non-empty digest.");
                yield break;
            }

            var sb = new StringBuilder();
            sb.Append("{\"digest\":").Append(JsonText.Quote(digest));
            sb.Append(",\"is_last_chunk\":false");
            if (durationMs > 0)
            {
                sb.Append(",\"duration_ms\":").Append(durationMs);
            }
            if (!string.IsNullOrWhiteSpace(gameStatesJson))
            {
                sb.Append(",\"game_states\":").Append(gameStatesJson.Trim());
            }
            sb.Append("}");
            string json = sb.ToString();

            _logger?.Invoke(
                $"Posting text chunk ({digest.Length} chars): POST {url}",
                SkillprintManager.LogLevel.Info
            );

            yield return SendAuthorized(
                () => JsonRequest(url, "POST", json),
                webRequest =>
                {
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        callback(true, webRequest.downloadHandler.text);
                    }
                    else
                    {
                        _logger?.Invoke(
                            $"PostTextChunk Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, ErrorText(webRequest));
                    }
                },
                authError => callback(false, authError)
            );
        }

        public IEnumerator PollParameterResults(
            string sessionId,
            Action<bool, List<ParameterUpdateResult>> callback
        )
        {
            string url = $"{_baseUrl}{POLL_RESULTS_ENDPOINT.Replace("{sessionId}", sessionId)}";
            _logger?.Invoke($"Polling results: GET {url}", SkillprintManager.LogLevel.Info);

            yield return SendAuthorized(
                () => JsonRequest(url, "GET", null),
                webRequest =>
                {
                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        _logger?.Invoke(
                            $"PollResults Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, null);
                        return;
                    }

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
                },
                authError =>
                {
                    _logger?.Invoke($"PollResults skipped: {authError}", SkillprintManager.LogLevel.Error);
                    callback(false, null);
                }
            );
        }

        // ── Players (API-key mode only) ─────────────────────────────────────

        /// <summary>
        /// Creates a new user with the given internal ID, or gets an existing user's token.
        /// Only in <see cref="SkillprintAuthMode.PartnerApiKey"/> mode — these endpoints need
        /// the partner API key; in token mode the partner backend provisions players.
        /// </summary>
        /// <param name="customPlayerId">The partner's internal player ID</param>
        /// <param name="callback">Callback with success status and user token (or error message)</param>
        public IEnumerator CreateOrGetUserToken(
            string customPlayerId,
            Action<bool, string> callback
        )
        {
            if (_auth.UsesToken)
            {
                callback(false, TOKEN_MODE_NO_PARTNER_ENDPOINTS);
                yield break;
            }
            if (string.IsNullOrEmpty(customPlayerId))
            {
                callback(false, "Custom player ID cannot be null or empty");
                yield break;
            }

            // First, try to get an existing user token
            bool tokenSuccess = false;
            string tokenResult = null;
            yield return GetUserToken(
                customPlayerId,
                (success, result) =>
                {
                    tokenSuccess = success;
                    tokenResult = result;
                }
            );

            if (tokenSuccess)
            {
                // User exists, token retrieved successfully
                callback(true, tokenResult);
                yield break;
            }

            // User doesn't exist or token retrieval failed, try to create user
            bool createSuccess = false;
            string createResult = null;
            yield return CreateUser(
                customPlayerId,
                (success, result) =>
                {
                    createSuccess = success;
                    createResult = result;
                }
            );

            if (!createSuccess)
            {
                callback(false, $"Failed to create user: {createResult}");
                yield break;
            }

            // User created successfully, now get token
            bool newTokenSuccess = false;
            string newTokenResult = null;
            yield return GetUserToken(
                customPlayerId,
                (success, result) =>
                {
                    newTokenSuccess = success;
                    newTokenResult = result;
                }
            );

            if (newTokenSuccess)
            {
                callback(true, newTokenResult);
            }
            else
            {
                callback(false, $"User created but failed to get token: {newTokenResult}");
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

            string jsonData = JsonUtility.ToJson(new CreateUserRequest { internalId = internalId });

            yield return SendAuthorized(
                () => JsonRequest(url, "POST", jsonData),
                webRequest =>
                {
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
                        callback(false, ErrorText(webRequest));
                    }
                },
                authError => callback(false, authError)
            );
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

            string jsonData = JsonUtility.ToJson(new GetUserTokenRequest { internalId = internalId });

            yield return SendAuthorized(
                () => JsonRequest(url, "POST", jsonData),
                webRequest =>
                {
                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        _logger?.Invoke(
                            $"GetUserToken Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, ErrorText(webRequest));
                        return;
                    }

                    _logger?.Invoke(
                        "GetUserToken successful.",
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
                    catch (Exception e)
                    {
                        _logger?.Invoke(
                            $"Failed to parse token response: {e.Message}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, "Failed to parse token response");
                    }
                },
                authError => callback(false, authError)
            );
        }

        // ── Player reads ────────────────────────────────────────────────────

        /// <summary>
        /// GET a player-scoped endpoint. In API-key mode <paramref name="userToken"/> (a Knox
        /// token) identifies the player; in token mode the SDK token already does.
        /// </summary>
        private IEnumerator GetPlayerResource(
            string endpoint,
            string label,
            string userToken,
            Action<bool, string> callback
        )
        {
            string url = _baseUrl + endpoint;
            _logger?.Invoke($"Getting {label}: GET {url}", SkillprintManager.LogLevel.Info);

            yield return SendAuthorized(
                () =>
                {
                    var request = JsonRequest(url, "GET", null);
                    if (!_auth.UsesToken && !string.IsNullOrEmpty(userToken))
                    {
                        request.SetRequestHeader("X-Auth-Token", "Token " + userToken);
                    }
                    return request;
                },
                webRequest =>
                {
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        _logger?.Invoke(
                            $"Get {label} successful. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Info
                        );
                        callback(true, webRequest.downloadHandler.text);
                    }
                    else
                    {
                        _logger?.Invoke(
                            $"Get {label} Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}",
                            SkillprintManager.LogLevel.Error
                        );
                        callback(false, ErrorText(webRequest));
                    }
                },
                authError => callback(false, authError)
            );
        }

        /// <summary>
        /// Retrieves the user profile information.
        /// </summary>
        public IEnumerator GetUserProfile(string userToken, Action<bool, string> callback)
        {
            return GetPlayerResource(GET_USER_PROFILE_ENDPOINT, "user profile", userToken, callback);
        }

        /// <summary>
        /// Retrieves the skill progression information.
        /// </summary>
        public IEnumerator GetSkillProgression(string userToken, Action<bool, string> callback)
        {
            return GetPlayerResource(GET_SKILL_PROGRESSION_ENDPOINT, "skill progression", userToken, callback);
        }

        /// <summary>
        /// Retrieves the mood visualization information.
        /// </summary>
        public IEnumerator GetMoodVisualization(string userToken, Action<bool, string> callback)
        {
            return GetPlayerResource(GET_MOOD_VISUALIZATION_ENDPOINT, "mood visualization", userToken, callback);
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

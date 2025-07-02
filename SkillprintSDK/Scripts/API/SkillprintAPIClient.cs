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
            StartSessionRequest requestData = new StartSessionRequest
            {
                sessionId = sessionId,
                game = "fruit-ninja", // TODO: This game should be set dynamically, created if does not exist.
                // Here we must send the string representation of the mood. Not the enum.
                targetMood = targetMood,
                // game_parameters = gameParameters // TODO: This should be set dynamically, created if does not exist.
            };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);

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
            Action<bool, string> callback
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
                byte[] jpgData = screenshots[i].EncodeToJPG();
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

            using (UnityWebRequest webRequest = UnityWebRequest.Post(url, formData))
            {
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
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke(
                        $"PostScreenshots Error: {webRequest.error}. Response Code: {webRequest.responseCode}. Details: {webRequest.downloadHandler.text}",
                        SkillprintManager.LogLevel.Error
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
    }
}

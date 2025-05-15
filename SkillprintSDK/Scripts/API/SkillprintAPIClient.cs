using UnityEngine;
using UnityEngine.Networking; // For UnityWebRequest
using System.Collections;
using System.Collections.Generic;
using System.Text; // For Encoding
using System; // For Action

namespace Skillprint.SDK.API
{
    public class SkillprintAPIClient
    {
        private string _baseUrl;
        private string _partnerApiKey;
        private Action<string, SkillprintManager.LogLevel> _logger;

        // API Endpoints (make these configurable or constants)
        private const string START_SESSION_ENDPOINT = "/session/start";
        private const string UPLOAD_SCREENSHOTS_ENDPOINT = "/session/screenshots"; // Often session ID is part of path
        private const string POLL_RESULTS_ENDPOINT = "/session/{sessionId}/results"; // Use string.Format or interpolation

        public SkillprintAPIClient(string baseUrl, string partnerApiKey, Action<string, SkillprintManager.LogLevel> logger)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _partnerApiKey = partnerApiKey;
            _logger = logger;
        }

        public IEnumerator StartSession(string sessionId, string customPlayerId, List<ParameterInfo> gameParameters, Action<bool, string> callback)
        {
            string url = _baseUrl + START_SESSION_ENDPOINT;
            _logger?.Invoke($"Starting session: POST {url}", SkillprintManager.LogLevel.Info);

            StartSessionRequest requestData = new StartSessionRequest
            {
                partner_api_key = _partnerApiKey,
                session_uuid = sessionId,
                custom_player_id = customPlayerId,
                game_parameters = gameParameters
            };
            string jsonData = JsonUtility.ToJson(requestData);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("X-Partner-ApiKey", _partnerApiKey); // Or include in body as above

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke($"StartSession successful. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Info);
                    // Assuming API returns a simple success message or session ID. Parse if needed.
                    // BaseResponse response = JsonUtility.FromJson<BaseResponse>(webRequest.downloadHandler.text);
                    // callback(response.success, response.message);
                    callback(true, webRequest.downloadHandler.text); // Simplified
                }
                else
                {
                    _logger?.Invoke($"StartSession Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Error);
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }

        public IEnumerator PostScreenshots(string sessionId, List<Texture2D> screenshots, Action<bool, string> callback)
        {
            // Example: POST /session/{sessionId}/screenshots
            string url = $"{_baseUrl}{UPLOAD_SCREENSHOTS_ENDPOINT.Replace("{sessionId}", sessionId)}";
             _logger?.Invoke($"Posting {screenshots.Count} screenshots: POST {url}", SkillprintManager.LogLevel.Info);


            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection("session_id", sessionId)); // Or ensure session_id is in path

            for (int i = 0; i < screenshots.Count; i++)
            {
                if (screenshots[i] == null) continue;
                byte[] pngData = screenshots[i].EncodeToPNG(); // PNG is lossless, JPG is smaller but lossy
                if (pngData == null)
                {
                    _logger?.Invoke($"Failed to encode screenshot {i} to PNG.", SkillprintManager.LogLevel.Warning);
                    continue;
                }
                // filename "screenshot_timestamp_index.png"
                string filename = $"screenshot_{DateTime.UtcNow.Ticks}_{i}.png";
                formData.Add(new MultipartFormFileSection("images", pngData, filename, "image/png"));
            }

            if (formData.Count <= 1) // Only session_id, no images
            {
                _logger?.Invoke("No valid screenshots to post.", SkillprintManager.LogLevel.Warning);
                callback(false, "No images to post.");
                yield break;
            }


            using (UnityWebRequest webRequest = UnityWebRequest.Post(url, formData))
            {
                // UnityWebRequest.Post handles Content-Type for multipart forms.
                webRequest.SetRequestHeader("X-Partner-ApiKey", _partnerApiKey);

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke($"PostScreenshots successful. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Info);
                    callback(true, webRequest.downloadHandler.text);
                }
                else
                {
                    _logger?.Invoke($"PostScreenshots Error: {webRequest.error}. Response Code: {webRequest.responseCode}. Details: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Error);
                    callback(false, webRequest.error + " | " + webRequest.downloadHandler.text);
                }
            }
        }


        public IEnumerator PollParameterResults(string sessionId, Action<bool, List<ParameterUpdateResult>> callback)
        {
            string url = _baseUrl + POLL_RESULTS_ENDPOINT.Replace("{sessionId}", sessionId);
            _logger?.Invoke($"Polling results: GET {url}", SkillprintManager.LogLevel.Info);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("X-Partner-ApiKey", _partnerApiKey);
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    _logger?.Invoke($"PollResults successful. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Info);
                    try
                    {
                        // JsonUtility has limitations with root arrays.
                        // If API returns {"parameter_updates": [...]}, this works:
                        PollResultsResponse response = JsonUtility.FromJson<PollResultsResponse>(webRequest.downloadHandler.text);
                        if (response != null)
                        {
                            callback(true, response.parameter_updates);
                        }
                        else // Fallback for direct array, or use a helper
                        {
                             _logger?.Invoke($"PollResults: Failed to parse response into PollResultsResponse. Text: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Warning);
                            // Try parsing as a direct list if API returns `[ { ... }, { ... } ]`
                            // This requires a wrapper object for JsonUtility or using a different JSON library (like Newtonsoft.Json)
                            string jsonArray = webRequest.downloadHandler.text;
                            if(jsonArray.StartsWith("[")) // Simple check for array
                            {
                                string wrappedJson = $"{{\"parameter_updates\":{jsonArray}}}";
                                PollResultsResponse wrappedResponse = JsonUtility.FromJson<PollResultsResponse>(wrappedJson);
                                if (wrappedResponse != null && wrappedResponse.parameter_updates != null)
                                {
                                    callback(true, wrappedResponse.parameter_updates);
                                    yield break;
                                }
                            }
                            callback(false, null); // Parsing failed
                        }
                    }
                    catch (Exception e)
                    {
                        _logger?.Invoke($"PollResults JSON parsing error: {e.Message}. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Error);
                        callback(false, null);
                    }
                }
                else
                {
                    _logger?.Invoke($"PollResults Error: {webRequest.error}. Response: {webRequest.downloadHandler.text}", SkillprintManager.LogLevel.Error);
                    callback(false, null);
                }
            }
        }
    }
}
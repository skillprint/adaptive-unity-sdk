using System;
using System.Collections.Generic;

namespace Skillprint.SDK.API
{
    // --- Request Models ---

    [Serializable]
    public class StartSessionRequest
    {
        public string partner_api_key;
        public string session_uuid;
        public string custom_player_id; // Optional
        public List<ParameterInfo> game_parameters;
    }

    [Serializable]
    public class ParameterInfo
    {
        public string name;
        public string type;
        public string description;
        public string minValue; // Sent as string, can be null
        public string maxValue; // Sent as string, can be null
    }

    // Screenshot data will be sent via MultipartFormDataContent, so no specific request model here.

    // --- Response Models ---

    [Serializable]
    public class BaseResponse // A generic response, adapt if API returns specific success/error structures
    {
        public bool success;
        public string message;
        // public string session_id; // If StartSession returns it in body
    }

    [Serializable]
    public class PollResultsResponse
    {
        public bool success; // Or determine success by HTTP status
        public string message;
        public List<ParameterUpdateResult> parameter_updates;
    }

    [Serializable]
    public class ParameterUpdateResult
    {
        public string parameterName; // Should match the name defined in SkillprintConfig
        public object newValue;      // The API should send a value that can be parsed to the correct type
                                     // JsonUtility handles basic types well (float, int, bool, string)
    }
}
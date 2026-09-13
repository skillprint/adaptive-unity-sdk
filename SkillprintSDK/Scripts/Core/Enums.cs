namespace Skillprint.SDK
{
    public enum ParameterType
    {
        Float,
        Integer,
        Boolean,
        // Add other types as needed, e.g., String, Vector2, Vector3
    }

    public enum Mood
    {
        relax,
        focus,
        creativity,
        collaborate,
        grit,
        joy,
        curiosity,
        empathy,
        awe,
    }

    /// <summary>
    /// Fixed discrete-telemetry event vocabulary, matching skillprint-js-sdk's
    /// GameEvent enum (src/constants.ts) so events logged with it are read the
    /// same way by the backend's adaptation scoring regardless of platform.
    /// LogEvent also accepts a plain string for a game-specific custom event
    /// name outside this fixed set -- see SkillprintManager.LogEvent.
    /// </summary>
    public enum GameEvent
    {
        LEVEL_START,
        LEVEL_COMPLETE,
        LEVEL_QUIT,
        LEVEL_FAILED,
        LEVEL_RESTART,
        HINT,
        GENERIC_POSITIVE,
        GENERIC_NEGATIVE,
    }
}

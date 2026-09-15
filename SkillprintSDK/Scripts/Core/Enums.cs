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
    ///
    /// SOURCE OF TRUTH (breadcrumb for whoever changes this next): this list
    /// is the union of GameSchema.POSITIVE_EVENTS and NEGATIVE_EVENTS in
    /// marketplace's api_backend/games/models.py -- that's what actually
    /// decides how each event reads as a scoring signal. If you add or
    /// remove a value here, update those two lists (and skillprint-js-sdk's
    /// and skillprint-cocos-sdk's copies of this same enum) in the same
    /// change; nothing enforces the three staying in sync automatically.
    ///
    /// marketplace also has a *different*, broader constant,
    /// GameSchema.UNIVERSAL_TELEMETRY_EVENTS (added independently, later) --
    /// it additionally includes GAME_START/GAME_END/GAME_PAUSE/GAME_RESUME/
    /// MATCH/UNMATCH, none of which are here, and is used for session/
    /// analytics classification (Layer 1 vs. Layer 2 telemetry), not
    /// scoring. The two vocabularies are related but intentionally not
    /// identical -- don't assume one subsumes the other.
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

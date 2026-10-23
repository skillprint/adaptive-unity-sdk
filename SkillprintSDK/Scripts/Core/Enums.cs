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

    /// <summary>What the backend optimizes a session for: the target mood, or a target skill.</summary>
    public enum OptimizationTarget
    {
        Mood,
        Skill,
    }
}

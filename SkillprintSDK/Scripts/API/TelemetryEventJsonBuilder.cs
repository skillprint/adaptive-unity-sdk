using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Skillprint.SDK.API
{
    /// <summary>
    /// Hand-rolled JSON object serialization for LogTelemetryEvent's payload.
    /// JsonUtility can't serialize a flat Dictionary&lt;string, object&gt; --
    /// it needs a concrete [Serializable] type known up front, not a dynamic
    /// shape -- which is the same reason ParameterUpdateParser (in
    /// APIDataModels.cs) already hand-rolls JSON parsing instead of relying
    /// on JsonUtility for API responses of unpredictable shape.
    /// Supports the value types a telemetry event actually carries: string,
    /// bool, int, long, float, double, and null.
    /// </summary>
    public static class TelemetryEventJsonBuilder
    {
        /// <summary>
        /// Builds the inner event object -- e.g. {"event":"LEVEL_START","level":3}
        /// -- that SkillprintAPIClient.LogTelemetryEvent wraps in the outer
        /// {"event": ...} envelope the backend expects.
        /// </summary>
        public static string Build(string eventName, IDictionary<string, object> data)
        {
            object effectiveEventValue = eventName;
            if (data != null && data.TryGetValue("event", out var overrideEvent))
            {
                // Matches skillprint-js-sdk's TelemetryEventRequest, where
                // Object.assign(this, data) runs after `this.event = event`
                // is set, so a data field literally named "event" silently
                // wins there. Preserved here for platform parity, and
                // resolved before serializing so the output never has two
                // "event" keys.
                effectiveEventValue = overrideEvent;
            }

            var sb = new StringBuilder();
            sb.Append('{');
            AppendKeyValue(sb, "event", effectiveEventValue, isFirst: true);

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    if (kvp.Key == "event")
                        continue; // already resolved above
                    AppendKeyValue(sb, kvp.Key, kvp.Value, isFirst: false);
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendKeyValue(StringBuilder sb, string key, object value, bool isFirst)
        {
            if (!isFirst)
                sb.Append(',');
            sb.Append('"').Append(EscapeString(key)).Append("\":").Append(SerializeValue(value));
        }

        private static string SerializeValue(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return "\"" + EscapeString(s) + "\"";
                case bool b:
                    return b ? "true" : "false";
                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);
                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);
                case float f:
                    return f.ToString(CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                default:
                    // Fall back to stringifying rather than throw --
                    // LogEvent must never interrupt gameplay (see
                    // SkillprintManager.LogEvent's try/catch).
                    return "\"" + EscapeString(value.ToString()) + "\"";
            }
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

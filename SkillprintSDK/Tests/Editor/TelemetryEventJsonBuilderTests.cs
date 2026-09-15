using System.Collections.Generic;
using NUnit.Framework;
using Skillprint.SDK.API;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for TelemetryEventJsonBuilder -- the hand-rolled JSON
    /// serialization LogEvent depends on (JsonUtility can't serialize a
    /// flat Dictionary&lt;string, object&gt;). Getting this wrong means
    /// telemetry silently arrives malformed or not at all, so coverage here
    /// is as critical as ParameterDefinition's for parameter updates.
    /// </summary>
    [TestFixture]
    public class TelemetryEventJsonBuilderTests
    {
        [Test]
        public void Build_EventNameOnly_NoData()
        {
            string json = TelemetryEventJsonBuilder.Build("LEVEL_START", null);
            Assert.AreEqual("{\"event\":\"LEVEL_START\"}", json);
        }

        [Test]
        public void Build_EventNameOnly_EmptyData()
        {
            string json = TelemetryEventJsonBuilder.Build(
                "LEVEL_START",
                new Dictionary<string, object>()
            );
            Assert.AreEqual("{\"event\":\"LEVEL_START\"}", json);
        }

        [Test]
        public void Build_IncludesStringField()
        {
            var data = new Dictionary<string, object> { { "color", "red" } };
            string json = TelemetryEventJsonBuilder.Build("TAP", data);
            Assert.AreEqual("{\"event\":\"TAP\",\"color\":\"red\"}", json);
        }

        [Test]
        public void Build_IncludesIntField()
        {
            var data = new Dictionary<string, object> { { "level", 3 } };
            string json = TelemetryEventJsonBuilder.Build("LEVEL_COMPLETE", data);
            Assert.AreEqual("{\"event\":\"LEVEL_COMPLETE\",\"level\":3}", json);
        }

        [Test]
        public void Build_IncludesFloatField_UsesInvariantCulture()
        {
            var data = new Dictionary<string, object> { { "score", 1.5f } };
            string json = TelemetryEventJsonBuilder.Build("LEVEL_COMPLETE", data);
            Assert.AreEqual("{\"event\":\"LEVEL_COMPLETE\",\"score\":1.5}", json);
        }

        [Test]
        public void Build_IncludesBoolField()
        {
            var data = new Dictionary<string, object> { { "success", true } };
            string json = TelemetryEventJsonBuilder.Build("TAP", data);
            Assert.AreEqual("{\"event\":\"TAP\",\"success\":true}", json);
        }

        [Test]
        public void Build_IncludesNullField()
        {
            var data = new Dictionary<string, object> { { "combo", null } };
            string json = TelemetryEventJsonBuilder.Build("TAP", data);
            Assert.AreEqual("{\"event\":\"TAP\",\"combo\":null}", json);
        }

        [Test]
        public void Build_EscapesQuotesAndBackslashesInStrings()
        {
            var data = new Dictionary<string, object> { { "note", "she said \"hi\" \\ bye" } };
            string json = TelemetryEventJsonBuilder.Build("TAP", data);
            Assert.AreEqual(
                "{\"event\":\"TAP\",\"note\":\"she said \\\"hi\\\" \\\\ bye\"}",
                json
            );
        }

        [Test]
        public void Build_MultipleFields_PreservesInsertionOrder()
        {
            var data = new Dictionary<string, object> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
            string json = TelemetryEventJsonBuilder.Build("X", data);
            Assert.AreEqual("{\"event\":\"X\",\"a\":1,\"b\":2,\"c\":3}", json);
        }

        [Test]
        public void Build_DataFieldNamedEvent_OverridesEventNameWithoutDuplicateKey()
        {
            // Matches skillprint-js-sdk's TelemetryEventRequest, where
            // Object.assign(this, data) runs after `this.event = event` is
            // set, so a data field literally named "event" silently wins.
            var data = new Dictionary<string, object> { { "event", "OVERRIDDEN" } };
            string json = TelemetryEventJsonBuilder.Build("LEVEL_START", data);
            Assert.AreEqual("{\"event\":\"OVERRIDDEN\"}", json);
            // Only one "event" key should ever appear.
            Assert.AreEqual(1, CountOccurrences(json, "\"event\":"));
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index)) != -1)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Skillprint.SDK.API;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for ParameterUpdateParser.ParseFromJson — the JSON deserialization
    /// layer that converts raw API responses into ParameterUpdateResult objects.
    /// Covers the multiple response formats the API can return.
    /// </summary>
    [TestFixture]
    public class ParameterUpdateParserTests
    {
        // ───────────── Direct JSON array format ──────────────
        // API returns: [{"parameterName":"speed","newValue":0.5}, ...]

        [Test]
        public void ParseFromJson_DirectArray_SingleParam()
        {
            string json = @"[{""parameterName"":""speed"",""newValue"":0.75}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("speed", results[0].parameterName);
            // newValue might be parsed by manual parser as float or int
            object parsed = results[0].GetParsedValue();
            Assert.IsNotNull(parsed);
        }

        [Test]
        public void ParseFromJson_DirectArray_MultipleParams()
        {
            string json = @"[
                {""parameterName"":""speed"",""newValue"":0.5},
                {""parameterName"":""gravity"",""newValue"":1.2},
                {""parameterName"":""enabled"",""newValue"":true}
            ]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("speed", results[0].parameterName);
            Assert.AreEqual("gravity", results[1].parameterName);
            Assert.AreEqual("enabled", results[2].parameterName);
        }

        // ───────────── Wrapped in parameterUpdates object ──────────────
        // API returns: {"parameterUpdates": [...], "gameplayTips": "...", "state": "..."}

        [Test]
        public void ParseFromJson_WrappedFormat_ExtractsUpdates()
        {
            string json = @"{
                ""gameplayTips"": ""Play faster"",
                ""state"": ""active"",
                ""parameterUpdates"": [
                    {""parameterName"":""creationSpeedModifier"",""newValue"":0.8}
                ]
            }";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("creationSpeedModifier", results[0].parameterName);
        }

        [Test]
        public void ParseFromJson_WrappedFormat_MultipleUpdates()
        {
            string json = @"{
                ""parameterUpdates"": [
                    {""parameterName"":""speed"",""newValue"":0.5},
                    {""parameterName"":""difficulty"",""newValue"":3}
                ]
            }";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
        }

        // ───────────── Edge cases ──────────────

        [Test]
        public void ParseFromJson_EmptyArray_ReturnsEmptyList()
        {
            string json = "[]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseFromJson_EmptyObject_ReturnsEmptyList()
        {
            string json = "{}";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseFromJson_NullParameterUpdates_ReturnsEmptyList()
        {
            string json = @"{""parameterUpdates"": null}";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseFromJson_InvalidJson_ReturnsEmptyList()
        {
            string json = "this is not json at all";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ParseFromJson_EmptyString_ReturnsEmptyList()
        {
            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson("");

            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count);
        }

        // ───────────── Value type parsing ──────────────

        [Test]
        public void ParseFromJson_FloatValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""speed"",""newValue"":0.75}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
            // Manual parser returns float or int depending on whether it's whole
            float floatVal = System.Convert.ToSingle(value);
            Assert.AreEqual(0.75f, floatVal, 0.001f);
        }

        [Test]
        public void ParseFromJson_IntegerValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""level"",""newValue"":5}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
            int intVal = System.Convert.ToInt32(value);
            Assert.AreEqual(5, intVal);
        }

        [Test]
        public void ParseFromJson_BooleanValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""enabled"",""newValue"":true}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
        }

        [Test]
        public void ParseFromJson_StringValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""mode"",""newValue"":""hard""}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
            Assert.AreEqual("hard", value.ToString());
        }

        [Test]
        public void ParseFromJson_NegativeValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""offset"",""newValue"":-3.5}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
            float floatVal = System.Convert.ToSingle(value);
            Assert.AreEqual(-3.5f, floatVal, 0.001f);
        }

        [Test]
        public void ParseFromJson_ZeroValue_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""speed"",""newValue"":0}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);
            Assert.AreEqual(1, results.Count);

            object value = results[0].GetParsedValue();
            Assert.IsNotNull(value);
        }

        // ───────── Whitespace / formatting tolerance ─────────

        [Test]
        public void ParseFromJson_WithExtraWhitespace_ParsesCorrectly()
        {
            string json = @"  [  { ""parameterName"" : ""speed"" , ""newValue"" : 0.5 }  ]  ";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("speed", results[0].parameterName);
        }

        [Test]
        public void ParseFromJson_MinifiedJson_ParsesCorrectly()
        {
            string json = @"[{""parameterName"":""x"",""newValue"":1},{""parameterName"":""y"",""newValue"":2}]";

            List<ParameterUpdateResult> results = ParameterUpdateParser.ParseFromJson(json);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("x", results[0].parameterName);
            Assert.AreEqual("y", results[1].parameterName);
        }
    }
}

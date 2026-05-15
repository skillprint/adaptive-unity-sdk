using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skillprint.SDK;
using Skillprint.SDK.API;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for ParameterInfo construction — verifies that the mapping
    /// from ParameterDefinition to ParameterInfo (the API payload shape)
    /// is correct, including the adjustmentGuide field.
    /// </summary>
    [TestFixture]
    public class ParameterInfoTests
    {
        [Test]
        public void ParameterInfo_AllFieldsMap()
        {
            var info = new ParameterInfo
            {
                name = "speed",
                type = "Float",
                description = "Controls game speed",
                adjustmentGuide = "Higher values make the game faster",
                minValue = "0",
                maxValue = "10",
            };

            Assert.AreEqual("speed", info.name);
            Assert.AreEqual("Float", info.type);
            Assert.AreEqual("Controls game speed", info.description);
            Assert.AreEqual("Higher values make the game faster", info.adjustmentGuide);
            Assert.AreEqual("0", info.minValue);
            Assert.AreEqual("10", info.maxValue);
        }

        [Test]
        public void ParameterInfo_NullableFields()
        {
            // Boolean parameters don't have min/max
            var info = new ParameterInfo
            {
                name = "enabled",
                type = "Boolean",
                description = "Toggle feature",
                adjustmentGuide = null,
                minValue = null,
                maxValue = null,
            };

            Assert.AreEqual("enabled", info.name);
            Assert.IsNull(info.adjustmentGuide);
            Assert.IsNull(info.minValue);
            Assert.IsNull(info.maxValue);
        }

        /// <summary>
        /// Simulates the LINQ projection in SkillprintManager.StartGameSession
        /// to verify the mapping from ParameterDefinition → ParameterInfo.
        /// </summary>
        [Test]
        public void ParameterDefinition_MapsToParameterInfo_Correctly()
        {
            var paramDefs = new Dictionary<string, ParameterDefinition>
            {
                {
                    "speed",
                    new ParameterDefinition
                    {
                        parameterName = "speed",
                        type = ParameterType.Float,
                        description = "Game speed multiplier",
                        howSDKChangesIt = "Increase to speed up block creation",
                        minValue = 0f,
                        maxValue = 2f,
                    }
                },
                {
                    "enabled",
                    new ParameterDefinition
                    {
                        parameterName = "enabled",
                        type = ParameterType.Boolean,
                        description = "Toggle bonus mode",
                        howSDKChangesIt = "",
                    }
                },
            };

            // Replicate the mapping from SkillprintManager.StartGameSession
            var parameterInfos = paramDefs.Values
                .Select(p => new ParameterInfo
                {
                    name = p.parameterName,
                    type = p.type.ToString(),
                    description = p.description,
                    adjustmentGuide = p.howSDKChangesIt,
                    minValue =
                        (p.type == ParameterType.Float || p.type == ParameterType.Integer)
                            ? p.minValue.ToString()
                            : null,
                    maxValue =
                        (p.type == ParameterType.Float || p.type == ParameterType.Integer)
                            ? p.maxValue.ToString()
                            : null,
                })
                .ToList();

            Assert.AreEqual(2, parameterInfos.Count);

            // Check float parameter
            var speedInfo = parameterInfos.First(p => p.name == "speed");
            Assert.AreEqual("Float", speedInfo.type);
            Assert.AreEqual("Game speed multiplier", speedInfo.description);
            Assert.AreEqual("Increase to speed up block creation", speedInfo.adjustmentGuide);
            Assert.AreEqual("0", speedInfo.minValue);
            Assert.AreEqual("2", speedInfo.maxValue);

            // Check boolean parameter — min/max should be null
            var enabledInfo = parameterInfos.First(p => p.name == "enabled");
            Assert.AreEqual("Boolean", enabledInfo.type);
            Assert.IsNull(enabledInfo.minValue);
            Assert.IsNull(enabledInfo.maxValue);
        }

        [Test]
        public void ParameterDefinition_IntegerType_MapsMinMaxAsStrings()
        {
            var def = new ParameterDefinition
            {
                parameterName = "level",
                type = ParameterType.Integer,
                description = "Game difficulty level",
                howSDKChangesIt = "Set higher for harder gameplay",
                minValue = 1,
                maxValue = 10,
            };

            var info = new ParameterInfo
            {
                name = def.parameterName,
                type = def.type.ToString(),
                description = def.description,
                adjustmentGuide = def.howSDKChangesIt,
                minValue = def.minValue.ToString(),
                maxValue = def.maxValue.ToString(),
            };

            Assert.AreEqual("Integer", info.type);
            Assert.AreEqual("1", info.minValue);
            Assert.AreEqual("10", info.maxValue);
            Assert.AreEqual("Set higher for harder gameplay", info.adjustmentGuide);
        }

        [Test]
        public void StartSessionRequest_SerializesCorrectly()
        {
            var request = new StartSessionRequest
            {
                sessionId = "test-session-123",
                game = "hextris",
                targetMood = "relax",
                gameParameters = new List<ParameterInfo>
                {
                    new ParameterInfo
                    {
                        name = "speed",
                        type = "Float",
                        description = "Speed control",
                        adjustmentGuide = "Higher is faster",
                        minValue = "0",
                        maxValue = "5",
                    },
                },
            };

            // Verify the structure
            Assert.AreEqual("test-session-123", request.sessionId);
            Assert.AreEqual("hextris", request.game);
            Assert.AreEqual("relax", request.targetMood);
            Assert.AreEqual(1, request.gameParameters.Count);
            Assert.AreEqual("speed", request.gameParameters[0].name);
            Assert.AreEqual("Higher is faster", request.gameParameters[0].adjustmentGuide);

            // Verify JsonUtility serialization includes adjustmentGuide
            string json = UnityEngine.JsonUtility.ToJson(request);
            Assert.IsTrue(json.Contains("adjustmentGuide"));
            Assert.IsTrue(json.Contains("Higher is faster"));
        }
    }
}

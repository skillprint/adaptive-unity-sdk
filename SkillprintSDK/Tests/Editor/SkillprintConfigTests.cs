using NUnit.Framework;
using Skillprint.SDK;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for SkillprintConfig — environment switching, API key resolution,
    /// and default configuration values.
    /// </summary>
    [TestFixture]
    public class SkillprintConfigTests
    {
        // ───────────── ActivePartnerApiKey ─────────────────

        [Test]
        public void ActivePartnerApiKey_Production_ReturnsProductionKey()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Production;
            config.productionPartnerApiKey = "prod-key-123";
            config.stagingPartnerApiKey = "staging-key-456";

            Assert.AreEqual("prod-key-123", config.ActivePartnerApiKey);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ActivePartnerApiKey_Staging_ReturnsStagingKey()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Staging;
            config.productionPartnerApiKey = "prod-key-123";
            config.stagingPartnerApiKey = "staging-key-456";

            Assert.AreEqual("staging-key-456", config.ActivePartnerApiKey);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ActivePartnerApiKey_Staging_FallsBackToProduction_WhenStagingEmpty()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Staging;
            config.productionPartnerApiKey = "prod-key-123";
            config.stagingPartnerApiKey = ""; // Empty → should fallback

            Assert.AreEqual("prod-key-123", config.ActivePartnerApiKey);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ActivePartnerApiKey_Staging_FallsBackToProduction_WhenStagingNull()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Staging;
            config.productionPartnerApiKey = "prod-key-123";
            config.stagingPartnerApiKey = null;

            Assert.AreEqual("prod-key-123", config.ActivePartnerApiKey);

            UnityEngine.Object.DestroyImmediate(config);
        }

        // ───────────── ActiveApiBaseUrl ─────────────────

        [Test]
        public void ActiveApiBaseUrl_Production_ReturnsProductionUrl()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Production;
            config.productionApiBaseUrl = "https://api.skillprint.co";
            config.stagingApiBaseUrl = "https://api.staging.skillprint.co";

            Assert.AreEqual("https://api.skillprint.co", config.ActiveApiBaseUrl);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ActiveApiBaseUrl_Staging_ReturnsStagingUrl()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            config.targetEnvironment = ApiEnvironment.Staging;
            config.productionApiBaseUrl = "https://api.skillprint.co";
            config.stagingApiBaseUrl = "https://api.staging.skillprint.co";

            Assert.AreEqual("https://api.staging.skillprint.co", config.ActiveApiBaseUrl);

            UnityEngine.Object.DestroyImmediate(config);
        }

        // ───────────── Default Values ─────────────────

        [Test]
        public void DefaultValues_AreCorrect()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();

            Assert.AreEqual(960, config.screenshotMaxWidth);
            Assert.AreEqual(60, config.screenshotJpegQuality);
            Assert.AreEqual(2.0f, config.screenshotIntervalSeconds, 0.001f);
            Assert.AreEqual(5.0f, config.screenshotPostIntervalSeconds, 0.001f);
            Assert.AreEqual(5.0f, config.pollResultsIntervalSeconds, 0.001f);
            Assert.IsFalse(config.enableDebugLogging);
            Assert.AreEqual(ApiEnvironment.Production, config.targetEnvironment);
            Assert.AreEqual("https://api.skillprint.co", config.productionApiBaseUrl);
            Assert.AreEqual("https://api.staging.skillprint.co", config.stagingApiBaseUrl);
            Assert.IsNotNull(config.gameParameters);
            Assert.AreEqual(0, config.gameParameters.Count);

            UnityEngine.Object.DestroyImmediate(config);
        }

        // ───────────── Screenshot config validation ─────────────────

        [Test]
        public void ScreenshotJpegQuality_IsWithinRange()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();

            // Default value should be within 1-100 range
            Assert.GreaterOrEqual(config.screenshotJpegQuality, 1);
            Assert.LessOrEqual(config.screenshotJpegQuality, 100);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ScreenshotMaxWidth_DefaultIsPositive()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();

            Assert.Greater(config.screenshotMaxWidth, 0);

            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}

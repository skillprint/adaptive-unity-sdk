using NUnit.Framework;
using UnityEngine;

namespace Skillprint.SDK.Tests
{
    [TestFixture]
    public class SkillprintManagerTests
    {
        private GameObject _testGo;
        private SkillprintManager _manager;

        [SetUp]
        public void SetUp()
        {
            _testGo = new GameObject("SkillprintManagerTestObject");
            _manager = _testGo.AddComponent<SkillprintManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
            {
                Object.DestroyImmediate(_testGo);
            }
        }

        [Test]
        public void InitialState_IsTransmissionPaused_IsFalse()
        {
            Assert.IsFalse(_manager.IsScreenshotTransmissionPaused);
        }

        [Test]
        public void PauseScreenshotTransmission_SetsStateToTrue()
        {
            _manager.PauseScreenshotTransmission();
            Assert.IsTrue(_manager.IsScreenshotTransmissionPaused);
        }

        [Test]
        public void ResumeScreenshotTransmission_SetsStateToFalse()
        {
            _manager.PauseScreenshotTransmission();
            Assert.IsTrue(_manager.IsScreenshotTransmissionPaused);

            _manager.ResumeScreenshotTransmission();
            Assert.IsFalse(_manager.IsScreenshotTransmissionPaused);
        }

        [Test]
        public void StartGameSession_ResetsPauseStateToFalse()
        {
            var config = ScriptableObject.CreateInstance<SkillprintConfig>();
            config.productionPartnerApiKey = "test-key";
            config.productionApiBaseUrl = "https://api.test.co";
            config.stagingPartnerApiKey = "test-key";
            config.stagingApiBaseUrl = "https://api.test.co";
            config.targetEnvironment = ApiEnvironment.Production;
            config.gameName = "TestGame";
            
            _manager.config = config;
            _manager.Initialize(config);

            _manager.PauseScreenshotTransmission();
            Assert.IsTrue(_manager.IsScreenshotTransmissionPaused);

            try
            {
                _manager.StartGameSession("relax");
            }
            catch (System.Exception)
            {
                // Suppress exceptions from network operations or coroutine execution in edit-mode tests
            }

            Assert.IsFalse(_manager.IsScreenshotTransmissionPaused);
            Object.DestroyImmediate(config);
        }

        private static SkillprintConfig MakeConfig()
        {
            var config = ScriptableObject.CreateInstance<SkillprintConfig>();
            config.productionPartnerApiKey = "test-key";
            config.productionApiBaseUrl = "https://api.test.co";
            config.stagingPartnerApiKey = "test-key";
            config.stagingApiBaseUrl = "https://api.test.co";
            config.targetEnvironment = ApiEnvironment.Production;
            config.gameName = "TestGame";
            return config;
        }

        [Test]
        public void ReportedScore_IsNullUntilReported()
        {
            Assert.IsNull(_manager.ReportedScore);
        }

        [Test]
        public void ReportScore_KeepsTheLatestScore()
        {
            _manager.ReportScore(0);
            _manager.ReportScore(255);
            _manager.ReportScore(1177.5);
            Assert.AreEqual(1177.5, _manager.ReportedScore);
        }

        [Test]
        public void ReportScore_IgnoresNonFiniteScores()
        {
            var config = MakeConfig();
            _manager.config = config; // Log reads it for the warning

            _manager.ReportScore(42);
            _manager.ReportScore(double.NaN);
            _manager.ReportScore(double.PositiveInfinity);
            _manager.ReportScore(double.NegativeInfinity);

            Assert.AreEqual(42, _manager.ReportedScore);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void StartGameSession_ClearsTheReportedScore()
        {
            var config = MakeConfig();
            _manager.config = config;
            _manager.Initialize(config);
            _manager.ReportScore(255);

            try
            {
                _manager.StartGameSession("focus");
            }
            catch (System.Exception)
            {
                // Suppress exceptions from network operations or coroutine execution in edit-mode tests
            }

            Assert.IsNull(_manager.ReportedScore);
            Object.DestroyImmediate(config);
        }
    }
}

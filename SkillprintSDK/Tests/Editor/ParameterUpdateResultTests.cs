using NUnit.Framework;
using Skillprint.SDK.API;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for ParameterUpdateResult — GetParsedValue, ConvertToType,
    /// and the helper IsEmptyValue logic.
    /// </summary>
    [TestFixture]
    public class ParameterUpdateResultTests
    {
        // ───────────────── GetParsedValue ─────────────────

        [Test]
        public void GetParsedValue_WithNewValue_ReturnsNewValue()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = 1.5f,
            };
            Assert.AreEqual(1.5f, result.GetParsedValue());
        }

        [Test]
        public void GetParsedValue_WithStringNewValue_ReturnsString()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = "0.75",
            };
            Assert.AreEqual("0.75", result.GetParsedValue());
        }

        [Test]
        public void GetParsedValue_WithIntNewValue_ReturnsInt()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "level",
                newValue = 5,
            };
            Assert.AreEqual(5, result.GetParsedValue());
        }

        [Test]
        public void GetParsedValue_WithBoolNewValue_ReturnsBool()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "enabled",
                newValue = true,
            };
            Assert.AreEqual(true, result.GetParsedValue());
        }

        [Test]
        public void GetParsedValue_WithNullNewValue_ReturnsNull()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = null,
            };
            Assert.IsNull(result.GetParsedValue());
        }

        [Test]
        public void GetParsedValue_WithEmptyStringNewValue_ReturnsNull()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = "",
            };
            // Empty string is treated as "empty value" → falls through to null
            Assert.IsNull(result.GetParsedValue());
        }

        // ───────────────── ConvertToType ─────────────────

        [Test]
        public void ConvertToType_Float_FromFloat()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = 2.5f,
            };
            Assert.AreEqual(2.5f, result.ConvertToType<float>(), 0.0001f);
        }

        [Test]
        public void ConvertToType_Float_FromString()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = "1.25",
            };
            Assert.AreEqual(1.25f, result.ConvertToType<float>(), 0.0001f);
        }

        [Test]
        public void ConvertToType_Int_FromInt()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "level",
                newValue = 10,
            };
            Assert.AreEqual(10, result.ConvertToType<int>());
        }

        [Test]
        public void ConvertToType_Bool_FromBool()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "flag",
                newValue = false,
            };
            Assert.AreEqual(false, result.ConvertToType<bool>());
        }

        [Test]
        public void ConvertToType_NullValue_ReturnsDefault()
        {
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = null,
            };
            Assert.AreEqual(0f, result.ConvertToType<float>());
            Assert.AreEqual(0, result.ConvertToType<int>());
            Assert.AreEqual(false, result.ConvertToType<bool>());
        }

        // ───────────── ConvertToParameterType ──────────────

        [Test]
        public void ConvertToParameterType_Float_ConvertsAndClamps()
        {
            var paramDef = new ParameterDefinition
            {
                parameterName = "speed",
                type = ParameterType.Float,
                minValue = 0f,
                maxValue = 1f,
            };

            // Value in range
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = 0.5f,
            };
            object converted = result.ConvertToParameterType(paramDef);
            Assert.IsNotNull(converted);
            Assert.IsInstanceOf<float>(converted);
            Assert.AreEqual(0.5f, (float)converted, 0.0001f);
        }

        [Test]
        public void ConvertToParameterType_Float_ClampsOutOfRange()
        {
            var paramDef = new ParameterDefinition
            {
                parameterName = "speed",
                type = ParameterType.Float,
                minValue = 0f,
                maxValue = 1f,
            };

            // Value above max → should clamp to 1.0
            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = 5.0f,
            };
            object converted = result.ConvertToParameterType(paramDef);
            Assert.IsNotNull(converted);
            Assert.AreEqual(1.0f, (float)converted, 0.0001f);
        }

        [Test]
        public void ConvertToParameterType_Integer_ClampsOutOfRange()
        {
            var paramDef = new ParameterDefinition
            {
                parameterName = "level",
                type = ParameterType.Integer,
                minValue = 1,
                maxValue = 10,
            };

            var result = new ParameterUpdateResult
            {
                parameterName = "level",
                newValue = 50,
            };
            object converted = result.ConvertToParameterType(paramDef);
            Assert.IsNotNull(converted);
            Assert.AreEqual(10, (int)converted);
        }

        [Test]
        public void ConvertToParameterType_NullValue_ReturnsNull()
        {
            var paramDef = new ParameterDefinition
            {
                parameterName = "speed",
                type = ParameterType.Float,
                minValue = 0f,
                maxValue = 1f,
            };

            var result = new ParameterUpdateResult
            {
                parameterName = "speed",
                newValue = null,
            };
            Assert.IsNull(result.ConvertToParameterType(paramDef));
        }

        [Test]
        public void ConvertToParameterType_Boolean()
        {
            var paramDef = new ParameterDefinition
            {
                parameterName = "enabled",
                type = ParameterType.Boolean,
            };

            var result = new ParameterUpdateResult
            {
                parameterName = "enabled",
                newValue = true,
            };
            object converted = result.ConvertToParameterType(paramDef);
            Assert.IsNotNull(converted);
            Assert.AreEqual(true, (bool)converted);
        }
    }
}

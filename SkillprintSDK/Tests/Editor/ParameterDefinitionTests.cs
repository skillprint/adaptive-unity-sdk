using NUnit.Framework;
using Skillprint.SDK;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for ParameterDefinition — ConvertValue and IsValid.
    /// These are the core methods that gate whether API-driven parameter
    /// updates actually reach game code, so coverage here is critical.
    /// </summary>
    [TestFixture]
    public class ParameterDefinitionTests
    {
        // ───────────────────────── ConvertValue ─────────────────────────

        [Test]
        public void ConvertValue_Float_FromFloat()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue(1.5f);
            Assert.IsInstanceOf<float>(result);
            Assert.AreEqual(1.5f, (float)result, 0.0001f);
        }

        [Test]
        public void ConvertValue_Float_FromInt()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue(3);
            Assert.IsInstanceOf<float>(result);
            Assert.AreEqual(3.0f, (float)result, 0.0001f);
        }

        [Test]
        public void ConvertValue_Float_FromString()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue("0.75");
            Assert.IsInstanceOf<float>(result);
            Assert.AreEqual(0.75f, (float)result, 0.0001f);
        }

        [Test]
        public void ConvertValue_Float_FromDouble()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue(2.5);
            Assert.IsInstanceOf<float>(result);
            Assert.AreEqual(2.5f, (float)result, 0.0001f);
        }

        [Test]
        public void ConvertValue_Float_FromNull_ReturnsNull()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue(null);
            Assert.IsNull(result);
        }

        [Test]
        public void ConvertValue_Float_FromInvalidString_ReturnsNull()
        {
            var def = MakeFloat("speed");
            object result = def.ConvertValue("not_a_number");
            Assert.IsNull(result);
        }

        [Test]
        public void ConvertValue_Integer_FromInt()
        {
            var def = MakeInt("count");
            object result = def.ConvertValue(5);
            Assert.IsInstanceOf<int>(result);
            Assert.AreEqual(5, (int)result);
        }

        [Test]
        public void ConvertValue_Integer_FromFloat()
        {
            var def = MakeInt("count");
            object result = def.ConvertValue(5.9f);
            Assert.IsInstanceOf<int>(result);
            // Convert.ToInt32(5.9f) uses banker's rounding → 6
            Assert.AreEqual(6, (int)result);
        }

        [Test]
        public void ConvertValue_Integer_FromString()
        {
            var def = MakeInt("count");
            object result = def.ConvertValue("42");
            Assert.IsInstanceOf<int>(result);
            Assert.AreEqual(42, (int)result);
        }

        [Test]
        public void ConvertValue_Boolean_FromBool()
        {
            var def = MakeBool("enabled");
            object result = def.ConvertValue(true);
            Assert.IsInstanceOf<bool>(result);
            Assert.AreEqual(true, (bool)result);
        }

        [Test]
        public void ConvertValue_Boolean_FromString()
        {
            var def = MakeBool("enabled");
            object result = def.ConvertValue("true");
            Assert.IsInstanceOf<bool>(result);
            Assert.AreEqual(true, (bool)result);
        }

        [Test]
        public void ConvertValue_Boolean_FromInt()
        {
            var def = MakeBool("enabled");
            // Convert.ToBoolean(1) → true, Convert.ToBoolean(0) → false
            Assert.AreEqual(true, (bool)def.ConvertValue(1));
            Assert.AreEqual(false, (bool)def.ConvertValue(0));
        }

        // ───────────────────────── IsValid ─────────────────────────

        [Test]
        public void IsValid_Float_InRange_ReturnsTrue()
        {
            var def = MakeFloat("speed", 0f, 1f);
            Assert.IsTrue(def.IsValid(0.5f));
        }

        [Test]
        public void IsValid_Float_AtBoundaries_ReturnsTrue()
        {
            var def = MakeFloat("speed", 0f, 1f);
            Assert.IsTrue(def.IsValid(0f));
            Assert.IsTrue(def.IsValid(1f));
        }

        [Test]
        public void IsValid_Float_OutOfRange_ReturnsFalse()
        {
            var def = MakeFloat("speed", 0f, 1f);
            Assert.IsFalse(def.IsValid(1.5f));
            Assert.IsFalse(def.IsValid(-0.1f));
        }

        [Test]
        public void IsValid_Float_WrongType_ReturnsFalse()
        {
            var def = MakeFloat("speed", 0f, 1f);
            Assert.IsFalse(def.IsValid(1));       // int, not float
            Assert.IsFalse(def.IsValid("0.5"));    // string
            Assert.IsFalse(def.IsValid(null));
        }

        [Test]
        public void IsValid_Integer_InRange_ReturnsTrue()
        {
            var def = MakeInt("count", 0, 100);
            Assert.IsTrue(def.IsValid(50));
        }

        [Test]
        public void IsValid_Integer_OutOfRange_ReturnsFalse()
        {
            var def = MakeInt("count", 0, 100);
            Assert.IsFalse(def.IsValid(101));
            Assert.IsFalse(def.IsValid(-1));
        }

        [Test]
        public void IsValid_Integer_WrongType_ReturnsFalse()
        {
            var def = MakeInt("count", 0, 100);
            Assert.IsFalse(def.IsValid(50f));    // float, not int
            Assert.IsFalse(def.IsValid("50"));
        }

        [Test]
        public void IsValid_Boolean_ReturnsTrue()
        {
            var def = MakeBool("enabled");
            Assert.IsTrue(def.IsValid(true));
            Assert.IsTrue(def.IsValid(false));
        }

        [Test]
        public void IsValid_Boolean_WrongType_ReturnsFalse()
        {
            var def = MakeBool("enabled");
            Assert.IsFalse(def.IsValid(1));
            Assert.IsFalse(def.IsValid("true"));
        }

        // ──────────────── ConvertValue → IsValid round-trip ─────────────────

        [Test]
        public void RoundTrip_Float_StringInput_ConvertsAndValidates()
        {
            var def = MakeFloat("speed", 0f, 2f);
            object converted = def.ConvertValue("1.5");
            Assert.IsNotNull(converted);
            Assert.IsTrue(def.IsValid(converted));
        }

        [Test]
        public void RoundTrip_Integer_StringInput_ConvertsAndValidates()
        {
            var def = MakeInt("level", 1, 10);
            object converted = def.ConvertValue("5");
            Assert.IsNotNull(converted);
            Assert.IsTrue(def.IsValid(converted));
        }

        [Test]
        public void RoundTrip_Boolean_StringInput_ConvertsAndValidates()
        {
            var def = MakeBool("flag");
            object converted = def.ConvertValue("false");
            Assert.IsNotNull(converted);
            Assert.IsTrue(def.IsValid(converted));
        }

        // ───────────────────── Helpers ─────────────────────

        private static ParameterDefinition MakeFloat(string name, float min = 0f, float max = 10f)
        {
            return new ParameterDefinition
            {
                parameterName = name,
                type = ParameterType.Float,
                minValue = min,
                maxValue = max,
            };
        }

        private static ParameterDefinition MakeInt(string name, int min = 0, int max = 100)
        {
            return new ParameterDefinition
            {
                parameterName = name,
                type = ParameterType.Integer,
                minValue = min,
                maxValue = max,
            };
        }

        private static ParameterDefinition MakeBool(string name)
        {
            return new ParameterDefinition
            {
                parameterName = name,
                type = ParameterType.Boolean,
            };
        }
    }
}

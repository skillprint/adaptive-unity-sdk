using System.Globalization;
using NUnit.Framework;
using Skillprint.SDK.API;

namespace Skillprint.SDK.Tests
{
    [TestFixture]
    public class JsonTextTests
    {
        [Test]
        public void Quote_PlainText_IsWrappedInQuotes()
        {
            Assert.AreEqual("\"wave 3 done\"", JsonText.Quote("wave 3 done"));
        }

        [Test]
        public void Quote_EscapesQuotesBackslashesAndNewlines()
        {
            string quoted = JsonText.Quote("a \"b\" \\ c\nd\te");
            Assert.AreEqual("\"a \\\"b\\\" \\\\ c\\nd\\te\"", quoted);
        }

        [Test]
        public void Quote_ControlCharacter_IsUnicodeEscaped()
        {
            Assert.AreEqual("\"x\\u0001y\"", JsonText.Quote("x\u0001y"));
        }

        [Test]
        public void Quote_Null_IsJsonNull()
        {
            Assert.AreEqual("null", JsonText.Quote(null));
        }

        [Test]
        public void Number_WholeAndFractionalValues()
        {
            Assert.AreEqual("0", JsonText.Number(0));
            Assert.AreEqual("1177", JsonText.Number(1177));
            Assert.AreEqual("-3", JsonText.Number(-3));
            Assert.AreEqual("12.5", JsonText.Number(12.5));
        }

        [Test]
        public void Number_UsesADotWhateverTheCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("12.5", JsonText.Number(12.5));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void Number_NonFiniteIsJsonNull()
        {
            Assert.AreEqual("null", JsonText.Number(double.NaN));
            Assert.AreEqual("null", JsonText.Number(double.PositiveInfinity));
        }
    }
}

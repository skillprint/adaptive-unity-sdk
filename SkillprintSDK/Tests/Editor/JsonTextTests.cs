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
    }
}

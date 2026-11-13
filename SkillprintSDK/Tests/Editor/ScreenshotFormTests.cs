using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Skillprint.SDK.API;
using UnityEngine.Networking;

namespace Skillprint.SDK.Tests
{
    [TestFixture]
    public class ScreenshotFormTests
    {
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

        private static List<string> Names(List<IMultipartFormSection> form) =>
            form.Select(section => section.sectionName).ToList();

        private static string Text(List<IMultipartFormSection> form, string name) =>
            Encoding.UTF8.GetString(form.Single(section => section.sectionName == name).sectionData);

        [Test]
        public void Screenshots_AreNumberedWithoutGaps()
        {
            var form = SkillprintAPIClient.BuildScreenshotForm(new[] { Jpeg, Jpeg, Jpeg }, false);
            CollectionAssert.AreEqual(
                new[] { "is_last_chunk", "screenshot0", "screenshot1", "screenshot2" },
                Names(form)
            );
            Assert.AreEqual("false", Text(form, "is_last_chunk"));
        }

        [Test]
        public void GameState_RidesOnTheLastScreenshot()
        {
            var form = SkillprintAPIClient.BuildScreenshotForm(
                new[] { Jpeg, Jpeg },
                true,
                "{\"score\":255}"
            );
            CollectionAssert.AreEqual(
                new[] { "is_last_chunk", "screenshot0", "screenshot1", "game_state1" },
                Names(form)
            );
            Assert.AreEqual("{\"score\":255}", Text(form, "game_state1"));
            Assert.AreEqual("true", Text(form, "is_last_chunk"));
        }

        [Test]
        public void NoGameState_WhenNoneIsGiven()
        {
            Assert.IsFalse(
                Names(SkillprintAPIClient.BuildScreenshotForm(new[] { Jpeg }, false, null))
                    .Any(name => name.StartsWith("game_state"))
            );
            Assert.IsFalse(
                Names(SkillprintAPIClient.BuildScreenshotForm(new[] { Jpeg }, false, "  "))
                    .Any(name => name.StartsWith("game_state"))
            );
        }

        [Test]
        public void NoGameState_WithoutAScreenshotToCarryIt()
        {
            // A closing upload with no frames: the backend pairs a state with a screenshot.
            var form = SkillprintAPIClient.BuildScreenshotForm(new byte[0][], true, "{\"score\":9}");
            CollectionAssert.AreEqual(new[] { "is_last_chunk" }, Names(form));
        }
    }
}

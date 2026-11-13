using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class TestServerSettingsTests
    {
        [TestMethod]
        public void ParseSettings_reads_keys_and_ignores_comments_and_blanks()
        {
            string text = "# comment\r\nurl=http://h:8096\r\nuser=bob\r\npassword=p w\r\n\r\ngarbage\r\n";
            var d = TestServerSettings.ParseSettings(text);
            Assert.AreEqual("http://h:8096", d["url"]);
            Assert.AreEqual("bob", d["user"]);
            Assert.AreEqual("p w", d["password"]);
            Assert.AreEqual(3, d.Count);
        }

        [TestMethod]
        public void ParseSettings_trims_key_whitespace()
        {
            var d = TestServerSettings.ParseSettings("  URL = http://h  ");
            Assert.AreEqual("http://h", d["url"]);
        }

        [TestMethod]
        public void IsConfigured_requires_all_three_values()
        {
            var s = new TestServerSettings();
            s.Url = "http://h";
            s.User = "u";
            s.Password = "";
            Assert.IsFalse(s.IsConfigured);
            s.Password = "p";
            Assert.IsTrue(s.IsConfigured);
        }

        [TestMethod]
        public void Load_uses_the_local_defaults_when_the_environment_is_unset()
        {
            if (Environment.GetEnvironmentVariable("JELLYFIN_TEST_URL") != null) Assert.Inconclusive("environment already configured");
            var s = TestServerSettings.Load();
            Assert.AreEqual("http://192.168.0.108:8096", s.Url);
            Assert.AreEqual("vincenzo", s.User);
        }
    }
}

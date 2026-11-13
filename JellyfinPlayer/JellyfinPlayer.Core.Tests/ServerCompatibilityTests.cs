using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class ServerCompatibilityTests
    {
        [TestMethod]
        public void Older_servers_use_the_legacy_auth_spellings()
        {
            Assert.IsTrue(ServerCompatibility.IsLegacy("10.8.13"));
            Assert.IsTrue(ServerCompatibility.IsLegacy("10.10.7"));
            Assert.IsTrue(ServerCompatibility.IsLegacy("11.4.0"));
        }

        [TestMethod]
        public void Current_servers_use_the_modern_spellings()
        {
            Assert.IsFalse(ServerCompatibility.IsLegacy("12.2.0"));
            Assert.IsFalse(ServerCompatibility.IsLegacy("12"));
            Assert.IsFalse(ServerCompatibility.IsLegacy("13.0.1"));
        }

        [TestMethod]
        public void An_unknown_version_counts_as_modern()
        {
            Assert.IsFalse(ServerCompatibility.IsLegacy(null));
            Assert.IsFalse(ServerCompatibility.IsLegacy(""));
            Assert.IsFalse(ServerCompatibility.IsLegacy("dev"));
            Assert.IsFalse(ServerCompatibility.IsLegacy("v10.8.13"));
        }

        [TestMethod]
        public void Only_the_major_number_decides()
        {
            int major;
            Assert.IsTrue(ServerCompatibility.TryMajor("10.8.13", out major));
            Assert.AreEqual(10, major);
            Assert.IsTrue(ServerCompatibility.TryMajor("12", out major));
            Assert.AreEqual(12, major);
        }

        [TestMethod]
        public void Legacy_servers_get_the_api_key_spelling()
        {
            var urls = new JellyfinUrls("http://server:8096", "token") { LegacyAuth = true };
            Assert.IsTrue(urls.PrimaryImage("item", 300).Contains("api_key=token"));
            Assert.IsFalse(urls.PrimaryImage("item", 300).Contains("ApiKey="));
        }

        [TestMethod]
        public void Modern_servers_keep_the_api_key_capitalisation()
        {
            var urls = new JellyfinUrls("http://server:8096", "token");
            Assert.IsTrue(urls.PrimaryImage("item", 300).Contains("ApiKey=token"));
            Assert.IsFalse(urls.PrimaryImage("item", 300).Contains("api_key="));
        }
    }
}

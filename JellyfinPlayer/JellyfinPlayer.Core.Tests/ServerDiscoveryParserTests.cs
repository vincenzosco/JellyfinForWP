using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class ServerDiscoveryParserTests
    {
        [TestMethod]
        public void Parses_a_valid_response()
        {
            DiscoveredServerDto server;
            Assert.IsTrue(ServerDiscoveryParser.TryParse("{\"Address\":\"http://192.168.0.108:8096\",\"Id\":\"abc\",\"Name\":\"Home\"}", out server));
            Assert.IsNotNull(server);
            Assert.AreEqual("http://192.168.0.108:8096", server.Address);
            Assert.AreEqual("abc", server.Id);
            Assert.AreEqual("Home", server.Name);
        }

        [TestMethod]
        public void Ignores_unknown_keys()
        {
            DiscoveredServerDto server;
            Assert.IsTrue(ServerDiscoveryParser.TryParse("{\"Address\":\"http://h:8096\",\"Id\":\"i\",\"Name\":\"n\",\"Extra\":1}", out server));
            Assert.AreEqual("http://h:8096", server.Address);
        }

        [TestMethod]
        public void Rejects_a_response_without_an_address()
        {
            DiscoveredServerDto server;
            Assert.IsFalse(ServerDiscoveryParser.TryParse("{\"Id\":\"i\",\"Name\":\"n\"}", out server));
            Assert.IsNull(server);
        }

        [TestMethod]
        public void Rejects_malformed_json_without_throwing()
        {
            DiscoveredServerDto server;
            Assert.IsFalse(ServerDiscoveryParser.TryParse("not json", out server));
            Assert.IsNull(server);
        }

        [TestMethod]
        public void Rejects_empty_input()
        {
            DiscoveredServerDto server;
            Assert.IsFalse(ServerDiscoveryParser.TryParse("", out server));
            Assert.IsNull(server);
        }

        [TestMethod]
        public void Exposes_the_udp_probe_constants()
        {
            Assert.AreEqual(7359, ServerDiscoveryParser.DiscoveryPort);
            Assert.AreEqual("who is JellyfinServer?", ServerDiscoveryParser.ProbePayload);
        }
    }
}

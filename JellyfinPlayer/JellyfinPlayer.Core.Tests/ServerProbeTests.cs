using System;
using System.Collections.Generic;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class ServerProbeTests
    {
        private static DiscoveredServerDto Candidate(string address)
        {
            return new DiscoveredServerDto { Address = address, Id = "id-" + address, Name = "Home" };
        }

        private static Func<string, JellyfinApiClient> Prober(IDictionary<string, FakeTransport> clients)
        {
            return baseUrl => new JellyfinApiClient(clients[baseUrl], new JellyfinUrls(baseUrl), "dev");
        }

        // A web page at the address is not a Jellyfin server: skip it.
        [TestMethod]
        public void An_address_that_answers_with_html_is_not_a_server()
        {
            var probe = new FakeTransport();
            probe.Handler = url => "<html>nginx</html>";
            var clients = new Dictionary<string, FakeTransport> { { "http://127.0.0.1:9", probe } };

            var found = ServerProbe.FindFirstJellyfinAsync(new[] { Candidate("http://127.0.0.1:9") }, Prober(clients)).GetAwaiter().GetResult();

            Assert.IsNull(found, "an HTML answer must not validate as Jellyfin");
            Assert.IsTrue(probe.LastUrl.Contains("/System/Info/Public"), probe.LastUrl);
        }

        [TestMethod]
        public void Returns_the_first_validated_candidate()
        {
            var bad = new FakeTransport();
            bad.Handler = url => "<html>nginx</html>";
            var good = new FakeTransport();
            good.Handler = url => "{\"Id\":\"abc\",\"ServerName\":\"Home\"}";
            var clients = new Dictionary<string, FakeTransport>
            {
                { "http://192.168.0.50:8096", bad }, { "http://192.168.0.108:8096", good }
            };

            var found = ServerProbe.FindFirstJellyfinAsync(
                new[] { Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096") }, Prober(clients)).GetAwaiter().GetResult();

            Assert.IsNotNull(found);
            Assert.AreEqual("http://192.168.0.108:8096", found.Address);
        }

        // Server info without an Id is not a Jellyfin server reply.
        [TestMethod]
        public void An_answer_without_an_id_is_not_a_server()
        {
            var anonymous = new FakeTransport();
            anonymous.Handler = url => "{\"ServerName\":\"Router\"}";
            var clients = new Dictionary<string, FakeTransport> { { "http://192.168.0.50:8096", anonymous } };

            var found = ServerProbe.FindFirstJellyfinAsync(new[] { Candidate("http://192.168.0.50:8096") }, Prober(clients)).GetAwaiter().GetResult();

            Assert.IsNull(found);
        }

        [TestMethod]
        public void A_failing_candidate_does_not_stop_the_probe()
        {
            var dead = new FakeTransport();
            dead.ErrorToThrow = new JellyfinException("Could not reach the server", 0, JellyfinErrorKind.Unreachable);
            var good = new FakeTransport();
            good.Handler = url => "{\"Id\":\"abc\"}";
            var clients = new Dictionary<string, FakeTransport>
            {
                { "http://192.168.0.50:8096", dead }, { "http://192.168.0.108:8096", good }
            };

            var found = ServerProbe.FindFirstJellyfinAsync(
                new[] { Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096") }, Prober(clients)).GetAwaiter().GetResult();

            Assert.IsNotNull(found);
            Assert.AreEqual("http://192.168.0.108:8096", found.Address);
        }

        [TestMethod]
        public void No_candidate_means_no_server()
        {
            var clients = new Dictionary<string, FakeTransport>();
            Assert.IsNull(ServerProbe.FindFirstJellyfinAsync(new List<DiscoveredServerDto>(), Prober(clients)).GetAwaiter().GetResult());
        }
    }
}

using System.Net.Http;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class AuthTests
    {
        private JellyfinApiClient MakeClient(FakeTransport fake)
        {
            return new JellyfinApiClient(fake, new JellyfinUrls("http://host:8096"), "dev1");
        }

        [TestMethod]
        public void GetServerInfo_parses()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"ServerName\":\"Home\",\"Version\":\"10.9.10\",\"Id\":\"abc\"}";
            var info = MakeClient(fake).GetServerInfoAsync().Result;
            Assert.AreEqual("Home", info.ServerName);
        }

        [TestMethod]
        public void Authenticate_sets_token_user_and_device_header()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"User\":{\"Id\":\"u1\",\"Name\":\"bob\"},\"AccessToken\":\"T\",\"ServerId\":\"s1\"}";
            var client = MakeClient(fake);
            var r = client.AuthenticateAsync("bob", "pw").Result;
            Assert.AreEqual("T", client.AccessToken);
            Assert.AreEqual("u1", client.UserId);
            Assert.AreEqual("s1", client.ServerId);
            Assert.IsTrue(fake.LastHeaders["Authorization"].Contains("DeviceId=\"dev1\""), fake.LastHeaders["Authorization"]);
            Assert.IsFalse(fake.LastHeaders.ContainsKey("X-Emby-Authorization"), "the legacy header must not be sent: Jellyfin 12 rejects it");
            Assert.IsTrue(fake.LastJson.Contains("\"Pw\":\"pw\""), fake.LastJson);
        }

        [TestMethod]
        public void Authenticate_sends_password_for_old_and_new_servers()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"AccessToken\":\"T\"}";
            MakeClient(fake).AuthenticateAsync("bob", "pw").Wait();
            Assert.IsTrue(fake.LastJson.Contains("\"Pw\":\"pw\""), fake.LastJson);
            Assert.IsTrue(fake.LastJson.Contains("\"Password\":\"pw\""), fake.LastJson);
        }

        [TestMethod]
        public void Authenticate_with_401_throws_401()
        {
            var fake = new FakeTransport();
            fake.ErrorToThrow = new JellyfinException("nope", 401);
            var client = MakeClient(fake);
            try
            {
                client.AuthenticateAsync("bob", "x").GetAwaiter().GetResult();
                Assert.Fail("expected JellyfinException");
            }
            catch (JellyfinException ex)
            {
                Assert.AreEqual(401, ex.StatusCode);
            }
        }

        [TestMethod]
        public void Transport_maps_timeout()
        {
            var mapped = HttpTransport.MapException(new TaskCanceledException());
            Assert.IsTrue(mapped.Message.ToLower().Contains("timeout"), mapped.Message);
        }

        [TestMethod]
        public void Transport_maps_network_error()
        {
            var mapped = HttpTransport.MapException(new HttpRequestException("boom"));
            Assert.IsTrue(mapped.Message.Contains("Could not reach"), mapped.Message);
        }

        [TestMethod]
        public void GetUser_returns_the_name()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Id\":\"u1\",\"Name\":\"bob\"}";
            var user = MakeClient(fake).GetUserAsync("u1").Result;
            Assert.AreEqual("bob", user.Name);
        }
    }
}

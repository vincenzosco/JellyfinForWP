using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class PlaybackApiTests
    {
        private JellyfinApiClient MakeClient(FakeTransport fake)
        {
            var c = new JellyfinApiClient(fake, new JellyfinUrls("http://host:8096", "T"), "d");
            c.AccessToken = "T";
            return c;
        }

        [TestMethod]
        public void GetPlaybackInfo_posts_device_profile_and_returns_source()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"MediaSources\":[{\"Id\":\"m1\",\"TranscodingUrl\":\"http://h/x.m3u8\",\"Container\":\"ts\"}],\"PlaySessionId\":\"ps1\"}";
            var r = MakeClient(fake).GetPlaybackInfoAsync("u1", "i1", 0L).Result;
            Assert.AreEqual("http://h/x.m3u8", r.MediaSources[0].TranscodingUrl);
            Assert.AreEqual("ps1", r.PlaySessionId);
            Assert.IsTrue(fake.LastJson.Contains("\"Protocol\":\"http\""), fake.LastJson);
        }

        [TestMethod]
        public void SetFavorite_true_uses_POST_and_false_uses_DELETE()
        {
            var fake = new FakeTransport();
            var c = MakeClient(fake);
            c.SetFavoriteAsync("u1", "i1", true).Wait();
            Assert.AreEqual("POST", fake.LastMethod);
            c.SetFavoriteAsync("u1", "i1", false).Wait();
            Assert.AreEqual("DELETE", fake.LastMethod);
        }

        [TestMethod]
        public void SetPlayed_uses_POST_and_DELETE()
        {
            var fake = new FakeTransport();
            var c = MakeClient(fake);
            c.SetPlayedAsync("u1", "i1", true).Wait();
            Assert.AreEqual("POST", fake.LastMethod);
            c.SetPlayedAsync("u1", "i1", false).Wait();
            Assert.AreEqual("DELETE", fake.LastMethod);
        }

        [TestMethod]
        public void ReportProgress_sends_position_ticks()
        {
            var fake = new FakeTransport();
            MakeClient(fake).ReportPlaybackProgressAsync(new PlaybackProgressDto { ItemId = "i1", PositionTicks = 30000000L }).Wait();
            Assert.IsTrue(fake.LastJson.Contains("\"PositionTicks\":30000000"), fake.LastJson);
        }

        [TestMethod]
        public void GetPlaybackInfo_carries_the_requested_bitrate()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"MediaSources\":[],\"PlaySessionId\":\"ps1\"}";
            MakeClient(fake).GetPlaybackInfoAsync("u1", "i1", 0L, 2000000).Wait();
            Assert.IsTrue(fake.LastJson.Contains("\"MaxStreamingBitrate\":2000000"), fake.LastJson);
            Assert.IsFalse(fake.LastJson.Contains("\"MaxStreamingBitrate\":8000000"), fake.LastJson);
        }
    }
}

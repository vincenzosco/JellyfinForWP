using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class PlaybackUrlResolverTests
    {
        private MediaSourceInfoDto Source()
        {
            return new MediaSourceInfoDto { Id = "m1", Container = "mp4" };
        }

        [TestMethod]
        public void Uses_direct_stream_url_when_present()
        {
            var s = Source();
            s.DirectStreamUrl = "/Videos/i1/stream?static=true";
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsFalse(r.IsTranscode);
            Assert.IsTrue(r.Url.Contains("static=true"), r.Url);
            Assert.IsTrue(r.Url.Contains("ApiKey=TOK"), r.Url);
        }

        [TestMethod]
        public void Uses_transcoding_url_when_present()
        {
            var s = Source();
            s.TranscodingUrl = "/Videos/i1/stream.mp4?x=1";
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsTrue(r.IsTranscode);
            Assert.IsTrue(r.Url.Contains("ApiKey=TOK"), r.Url);
        }

        [TestMethod]
        public void Builds_static_stream_when_only_direct_flags_are_returned()
        {
            var s = Source();
            s.SupportsDirectPlay = true;
            s.SupportsDirectStream = true;
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsFalse(r.IsTranscode);
            Assert.AreEqual("http://h:8096/Videos/i1/stream?static=true&mediaSourceId=m1&ApiKey=TOK", r.Url);
        }

        [TestMethod]
        public void Builds_progressive_mp4_when_only_transcoding_is_supported()
        {
            var s = Source();
            s.SupportsTranscoding = true;
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsTrue(r.IsTranscode);
            Assert.IsTrue(r.Url.Contains("/Videos/i1/stream.mp4"), r.Url);
            Assert.IsTrue(r.Url.Contains("MaxStreamingBitrate=8000000"), r.Url);
            Assert.IsTrue(r.Url.Contains("ApiKey=TOK"), r.Url);
        }

        [TestMethod]
        public void A_subtitle_forces_a_transcode_even_when_direct_play_is_available()
        {
            var s = Source();
            s.SupportsDirectPlay = true;
            s.SupportsDirectStream = true;
            s.SupportsTranscoding = true;
            s.DirectStreamUrl = "/Videos/i1/stream?static=true";
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000, null, 3);
            Assert.IsTrue(r.IsTranscode, "a burned-in subtitle needs the transcode");
            Assert.IsTrue(r.Url.Contains("SubtitleStreamIndex=3"), r.Url);
            Assert.IsTrue(r.Url.Contains("SubtitleMethod=Encode"), r.Url);
            Assert.IsFalse(r.Url.Contains("static=true"), r.Url);
        }

        [TestMethod]
        public void A_chosen_audio_track_forces_a_transcode()
        {
            var s = Source();
            s.SupportsDirectPlay = true;
            s.SupportsTranscoding = true;
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000, 2);
            Assert.IsTrue(r.IsTranscode);
            Assert.IsTrue(r.Url.Contains("AudioStreamIndex=2"), r.Url);
            Assert.IsFalse(r.Url.Contains("SubtitleStreamIndex"), r.Url);
        }

        [TestMethod]
        public void A_source_that_cannot_transcode_cannot_carry_a_track()
        {
            var s = Source();
            s.SupportsDirectStream = true;
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000, null, 3);
            Assert.IsNull(r, "without transcoding there is nowhere to burn the subtitle");
        }

        [TestMethod]
        public void Returns_nothing_when_no_source_is_playable()
        {
            var s = Source();
            Assert.IsNull(PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000));
        }

        // The two below build the URL a failed stream falls back to, so both are tried
        // only after the player has already failed to play the other one.

        [TestMethod]
        public void Transcode_url_prefers_the_one_the_server_gave()
        {
            var s = Source();
            s.TranscodingUrl = "/Videos/i1/stream.mp4?DeviceProfileId=abc";
            string u = PlaybackUrlResolver.TranscodeUrl("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsTrue(u.Contains("DeviceProfileId=abc"), u);
            Assert.IsTrue(u.Contains("ApiKey=TOK"), u);
        }

        [TestMethod]
        public void Transcode_url_is_built_when_the_server_gave_none()
        {
            string u = PlaybackUrlResolver.TranscodeUrl("http://h:8096", "TOK", "i1", Source(), 2000000);
            Assert.IsTrue(u.Contains("/Videos/i1/stream.mp4"), u);
            Assert.IsTrue(u.Contains("MaxStreamingBitrate=2000000"), u);
            Assert.IsTrue(u.Contains("ApiKey=TOK"), u);
        }

        [TestMethod]
        public void Direct_url_is_nothing_when_the_source_cannot_be_played_directly()
        {
            Assert.IsNull(PlaybackUrlResolver.DirectUrl("http://h:8096", "TOK", "i1", Source()));
        }

        [TestMethod]
        public void Direct_url_builds_the_static_stream_when_the_flags_allow_it()
        {
            var s = Source();
            s.SupportsDirectStream = true;
            Assert.AreEqual("http://h:8096/Videos/i1/stream?static=true&mediaSourceId=m1&ApiKey=TOK",
                            PlaybackUrlResolver.DirectUrl("http://h:8096", "TOK", "i1", s));
        }

        [TestMethod]
        public void Does_not_duplicate_the_key_parameter()
        {
            var s = Source();
            s.DirectStreamUrl = "/Videos/i1/stream?static=true&ApiKey=TOK";
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.AreEqual(1, r.Url.Split(new[] { "ApiKey=" }, System.StringSplitOptions.None).Length - 1, r.Url);
        }

        [TestMethod]
        public void Leaves_a_url_the_server_signed_with_the_old_parameter_alone()
        {
            // A URL that already carries a token must not get a second one, whichever
            // spelling the server used.
            var s = Source();
            s.DirectStreamUrl = "/Videos/i1/stream?static=true&api_key=TOK";
            var r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000);
            Assert.IsFalse(r.Url.Contains("ApiKey="), r.Url);
            Assert.AreEqual(1, r.Url.Split(new[] { "api_key=" }, System.StringSplitOptions.None).Length - 1, r.Url);
        }
    }
}

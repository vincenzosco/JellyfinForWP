using System;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class StreamFallbackTests
    {
        private static Uri U(string url)
        {
            return new Uri(url);
        }

        [TestMethod]
        public void Returns_the_fallback_when_it_is_a_different_url()
        {
            var alternative = StreamFallback.Alternative(U("http://h/Videos/i1/stream?static=true"), U("http://h/Videos/i1/stream.mp4"), false);
            Assert.IsNotNull(alternative);
            Assert.AreEqual("http://h/Videos/i1/stream.mp4", alternative.ToString());
        }

        [TestMethod]
        public void Returns_nothing_when_the_fallback_was_already_used()
        {
            // A second failure has to reach the user: two broken URLs must not ping-pong.
            Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), U("http://h/other.mp4"), true));
        }

        [TestMethod]
        public void Returns_nothing_when_there_is_no_fallback()
        {
            Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), null, false));
        }

        [TestMethod]
        public void Returns_nothing_when_the_fallback_is_the_url_that_just_failed()
        {
            Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), U("http://h/direct"), false));
        }

        [TestMethod]
        public void Returns_nothing_when_there_is_no_current_url()
        {
            Assert.IsNull(StreamFallback.Alternative(null, U("http://h/other.mp4"), false));
        }
    }
}

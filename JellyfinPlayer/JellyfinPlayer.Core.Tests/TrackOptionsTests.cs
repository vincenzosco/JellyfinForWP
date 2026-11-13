using System.Collections.Generic;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class TrackOptionsTests
    {
        private List<MediaStreamDto> Streams()
        {
            return new List<MediaStreamDto>
            {
                new MediaStreamDto { Index = 0, Type = "Video", Codec = "h264" },
                new MediaStreamDto { Index = 1, Type = "Audio", Language = "eng", Codec = "aac", IsDefault = false },
                new MediaStreamDto { Index = 2, Type = "Audio", DisplayTitle = "Italiano - AAC stereo", Codec = "aac", IsDefault = true },
                new MediaStreamDto { Index = 3, Type = "Subtitle", DisplayTitle = "English", Codec = "subrip", IsExternal = true }
            };
        }

        [TestMethod]
        public void Audio_lists_only_audio_streams_in_server_order()
        {
            var options = TrackOptions.Audio(Streams());
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual(1, options[0].Index.Value);
            Assert.AreEqual(2, options[1].Index.Value);
        }

        [TestMethod]
        public void Subtitles_put_off_first_then_every_subtitle()
        {
            var options = TrackOptions.Subtitles(Streams(), "Off");
            Assert.AreEqual(2, options.Count);
            Assert.IsFalse(options[0].Index.HasValue, "the first row is the explicit off");
            Assert.AreEqual("Off", options[0].Label);
            Assert.AreEqual(3, options[1].Index.Value);
            Assert.AreEqual("English", options[1].Label);
        }

        [TestMethod]
        public void A_stream_without_subtitles_still_offers_off()
        {
            Assert.AreEqual(1, TrackOptions.Subtitles(null, "Off").Count);
            Assert.IsFalse(TrackOptions.Subtitles(null, "Off")[0].Index.HasValue);
        }

        [TestMethod]
        public void The_default_audio_index_is_the_flagged_one_or_the_first()
        {
            Assert.AreEqual(2, TrackOptions.DefaultAudioIndex(Streams()).Value);
            var noDefault = new List<MediaStreamDto>
            {
                new MediaStreamDto { Index = 4, Type = "Audio" }
            };
            Assert.AreEqual(4, TrackOptions.DefaultAudioIndex(noDefault).Value);
            Assert.IsFalse(TrackOptions.DefaultAudioIndex(null).HasValue);
        }

        [TestMethod]
        public void The_default_audio_is_sent_as_nothing_so_it_can_direct_play()
        {
            Assert.IsFalse(TrackOptions.AudioSelection(Streams(), 2).HasValue);
            Assert.AreEqual(1, TrackOptions.AudioSelection(Streams(), 1).Value);
            Assert.IsFalse(TrackOptions.AudioSelection(Streams(), null).HasValue);
        }

        [TestMethod]
        public void A_label_falls_back_from_display_title_to_language_and_codec()
        {
            Assert.AreEqual("Italiano - AAC stereo", TrackOptions.Describe(Streams()[2]));
            Assert.AreEqual("eng - AAC", TrackOptions.Describe(Streams()[1]));
            Assert.AreEqual("Track 7", TrackOptions.Describe(new MediaStreamDto { Index = 7 }));
            Assert.AreEqual("", TrackOptions.Describe(null));
        }
    }
}

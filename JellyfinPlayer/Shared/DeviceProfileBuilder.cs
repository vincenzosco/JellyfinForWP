using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    public static class DeviceProfileBuilder
    {
        public const int DefaultBitrate = 8000000;

        // Direct play for MP4 H.264+AAC/MP3, with a progressive HTTP MP4
        // h264+aac transcode fallback. The Windows Phone Silverlight MediaElement
        // has no AdaptiveMediaSource (no HLS) and no confirmed MKV support, so the
        // baseline claims only the container the platform is documented to play.
        public static DeviceProfileDto DefaultProfile(int maxBitrate = DefaultBitrate)
        {
            var dp = new DeviceProfileDto();
            dp.MaxStreamingBitrate = maxBitrate;
            dp.TranscodingProfiles = new List<TranscodingProfileDto>
            {
                new TranscodingProfileDto { Container = "mp4", Type = "Video", VideoCodec = "h264",
                                            AudioCodec = "aac", Protocol = "http", Context = "Streaming",
                                            MaxAudioChannels = "2" }
            };
            dp.DirectPlayProfiles = new List<DirectPlayProfileDto>
            {
                new DirectPlayProfileDto { Container = "mp4", Type = "Video", VideoCodec = "h264", AudioCodec = "aac,mp3" }
            };
            // Encode burns the subtitle into the transcode. Text and image formats
            // are both declared, so a PGS or VobSub track can be chosen and still
            // reach the phone as a picture it already knows how to draw.
            dp.SubtitleProfiles = new List<SubtitleProfileDto>
            {
                new SubtitleProfileDto { Format = "srt", Method = "Encode" },
                new SubtitleProfileDto { Format = "subrip", Method = "Encode" },
                new SubtitleProfileDto { Format = "ass", Method = "Encode" },
                new SubtitleProfileDto { Format = "ssa", Method = "Encode" },
                new SubtitleProfileDto { Format = "vtt", Method = "Encode" },
                new SubtitleProfileDto { Format = "webvtt", Method = "Encode" },
                new SubtitleProfileDto { Format = "mov_text", Method = "Encode" },
                new SubtitleProfileDto { Format = "pgssub", Method = "Encode" },
                new SubtitleProfileDto { Format = "dvdsub", Method = "Encode" },
                new SubtitleProfileDto { Format = "dvbsub", Method = "Encode" }
            };
            return dp;
        }

        public static PlaybackInfoRequestDto ToRequest(string userId, long startTicks, int maxBitrate = DefaultBitrate,
                                                       int? audioStreamIndex = null,
                                                       int? subtitleStreamIndex = null)
        {
            return new PlaybackInfoRequestDto
            {
                UserId = userId,
                MaxStreamingBitrate = maxBitrate,
                StartTimeTicks = startTicks,
                AudioStreamIndex = audioStreamIndex,
                SubtitleStreamIndex = subtitleStreamIndex,
                AutoOpenLiveStream = true,
                DeviceProfile = DefaultProfile(maxBitrate)
            };
        }
    }
}

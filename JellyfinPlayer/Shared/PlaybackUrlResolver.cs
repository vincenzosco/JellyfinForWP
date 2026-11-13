using System;

namespace JellyfinPlayer.Shared
{
    public class ResolvedPlayback
    {
        public string Url { get; set; }
        public bool IsTranscode { get; set; }
        public string MediaSourceId { get; set; }
    }

    // Jellyfin does NOT always return DirectStreamUrl/TranscodingUrl: for a
    // directly-playable file it returns only flags (SupportsDirectPlay/
    // SupportsDirectStream/SupportsTranscoding) and expects the client to build
    // the stream URL. This resolves whatever the server gave us into a playable URL.
    public static class PlaybackUrlResolver
    {
        public static ResolvedPlayback Resolve(string baseUrl, string token, string itemId, MediaSourceInfoDto source, int maxBitrate,
                                               int? audioStreamIndex = null,
                                               int? subtitleStreamIndex = null)
        {
            if (source == null) return null;
            var urls = new JellyfinUrls(baseUrl, token);
            var plan = new ResolvedPlayback();
            plan.MediaSourceId = source.Id;
            // A chosen audio track or subtitle can only be honored while the server
            // remuxes or burns in, so those requests skip every direct-play branch.
            bool tracksRequested = audioStreamIndex.HasValue || subtitleStreamIndex.HasValue;

            if (!tracksRequested)
            {
                if (!string.IsNullOrEmpty(source.DirectStreamUrl))
                {
                    plan.Url = EnsureKey(source.DirectStreamUrl, token);
                    plan.IsTranscode = false;
                    return plan;
                }

                if (!string.IsNullOrEmpty(source.TranscodingUrl))
                {
                    plan.Url = EnsureKey(source.TranscodingUrl, token);
                    plan.IsTranscode = true;
                    return plan;
                }

                if (source.SupportsDirectStream || source.SupportsDirectPlay)
                {
                    plan.Url = urls.VideoStream(itemId, source.Id);
                    plan.IsTranscode = false;
                    return plan;
                }
            }

            if (source.SupportsTranscoding)
            {
                plan.Url = WithTracks(urls.VideoTranscode(itemId, source.Id, maxBitrate, audioStreamIndex, subtitleStreamIndex),
                                      audioStreamIndex, subtitleStreamIndex);
                plan.IsTranscode = true;
                return plan;
            }

            return null;
        }

        // The two builders below produce the URL a stream falls back to after the
        // one being played has failed - the other way round. They are only ever
        // used as a retry, so building a URL the server may refuse is deliberate:
        // the alternative is showing the failure sentence without trying.

        // The transcode of a source: what the server offered, else the
        // progressive MP4 this client asks for.
        public static string TranscodeUrl(string baseUrl, string token, string itemId,
                                          MediaSourceInfoDto source, int maxBitrate,
                                          int? audioStreamIndex = null,
                                          int? subtitleStreamIndex = null)
        {
            if (source == null) return null;
            if (!string.IsNullOrEmpty(source.TranscodingUrl))
            {
                return WithTracks(EnsureKey(source.TranscodingUrl, token), audioStreamIndex, subtitleStreamIndex);
            }
            return new JellyfinUrls(baseUrl, token).VideoTranscode(itemId, source.Id, maxBitrate, audioStreamIndex, subtitleStreamIndex);
        }

        // The original file, when the server is willing to send it as it is.
        // Nothing when the source cannot be played directly at all.
        public static string DirectUrl(string baseUrl, string token, string itemId,
                                       MediaSourceInfoDto source)
        {
            if (source == null) return null;
            if (!string.IsNullOrEmpty(source.DirectStreamUrl)) return EnsureKey(source.DirectStreamUrl, token);
            if (source.SupportsDirectStream || source.SupportsDirectPlay)
            {
                return new JellyfinUrls(baseUrl, token).VideoStream(itemId, source.Id);
            }
            return null;
        }

        // Adds the chosen tracks to a URL the server itself produced, without
        // duplicating a parameter it already carries.
        private static string WithTracks(string url, int? audioStreamIndex, int? subtitleStreamIndex)
        {
            if (string.IsNullOrEmpty(url)) return url;
            string result = url;
            if (audioStreamIndex.HasValue && result.IndexOf("AudioStreamIndex=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                result += (result.Contains("?") ? "&" : "?") + "AudioStreamIndex=" + audioStreamIndex.Value.ToString();
            }
            if (subtitleStreamIndex.HasValue && result.IndexOf("SubtitleStreamIndex=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                result += (result.Contains("?") ? "&" : "?") + "SubtitleStreamIndex=" + subtitleStreamIndex.Value.ToString() + "&SubtitleMethod=Encode";
            }
            return result;
        }

        public static string EnsureKey(string url, string token)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token)) return url;
            // ApiKey is the parameter Jellyfin 12 still honors; a URL the server
            // already signed with either spelling is left alone so the key is not
            // sent twice.
            if (url.IndexOf("ApiKey=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.Contains("api_key=")) return url;
            return url + (url.Contains("?") ? "&" : "?") + "ApiKey=" + token;
        }
    }
}

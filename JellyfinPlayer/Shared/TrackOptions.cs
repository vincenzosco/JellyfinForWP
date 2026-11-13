using System;
using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    // One row of the player's track picker. Index is null only for the explicit
    // "off" subtitle entry.
    public class TrackOption
    {
        public int? Index { get; set; }
        public string Label { get; set; }
    }

    // The audio and subtitle streams a Jellyfin media source carries. Pure, so the
    // contents of the picker and the "is this the default track" rule are covered by
    // the desktop tests instead of by a device.
    public static class TrackOptions
    {
        public static List<TrackOption> Audio(List<MediaStreamDto> streams)
        {
            var options = new List<TrackOption>();
            if (streams == null) return options;
            foreach (var stream in streams)
            {
                if (stream != null && IsType(stream, "Audio"))
                {
                    options.Add(new TrackOption { Index = stream.Index, Label = Describe(stream) });
                }
            }
            return options;
        }

        // "Off" comes first: choosing it is how the user gets back to the
        // untouched stream after watching with burned-in subtitles.
        public static List<TrackOption> Subtitles(List<MediaStreamDto> streams, string offLabel)
        {
            var options = new List<TrackOption>();
            options.Add(new TrackOption { Index = null, Label = offLabel });
            if (streams == null) return options;
            foreach (var stream in streams)
            {
                if (stream != null && IsType(stream, "Subtitle"))
                {
                    options.Add(new TrackOption { Index = stream.Index, Label = Describe(stream) });
                }
            }
            return options;
        }

        // The track the server would pick: the one marked default, else the first.
        public static int? DefaultAudioIndex(List<MediaStreamDto> streams)
        {
            int? first = null;
            if (streams == null) return null;
            foreach (var stream in streams)
            {
                if (stream != null && IsType(stream, "Audio"))
                {
                    if (stream.IsDefault) return stream.Index;
                    if (!first.HasValue) first = stream.Index;
                }
            }
            return first;
        }

        // Nothing goes to the server while the default audio is playing: a normal
        // playback must still direct-play instead of being transcoded for nothing.
        public static int? AudioSelection(List<MediaStreamDto> streams, int? index)
        {
            if (!index.HasValue) return null;
            if (DefaultAudioIndex(streams) == index) return null;
            return index;
        }

        public static string Describe(MediaStreamDto stream)
        {
            if (stream == null) return "";
            if (!string.IsNullOrEmpty(stream.DisplayTitle)) return stream.DisplayTitle;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(stream.Language)) parts.Add(stream.Language);
            if (!string.IsNullOrEmpty(stream.Codec)) parts.Add(stream.Codec.ToUpperInvariant());
            if (parts.Count == 0) parts.Add("Track " + stream.Index.ToString());
            return string.Join(" - ", parts.ToArray());
        }

        private static bool IsType(MediaStreamDto stream, string type)
        {
            return string.Equals(stream.Type, type, StringComparison.OrdinalIgnoreCase);
        }
    }
}

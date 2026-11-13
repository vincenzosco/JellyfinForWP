using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace JellyfinPlayer.Shared
{
    // One stored video. It travels as JSON next to the files, so the app can list
    // and resume downloads with no server round trip.
    [DataContract]
    public class DownloadRecordDto
    {
        [DataMember] public string ItemId { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Subtitle { get; set; }
        [DataMember] public string FileName { get; set; }
        [DataMember] public long? RunTimeTicks { get; set; }
        [DataMember] public long? PositionTicks { get; set; }
        [DataMember] public long SizeBytes { get; set; }
        [DataMember] public long DownloadedAtTicks { get; set; }
    }

    // The rules the download screens depend on: where a file goes, how much of it
    // has arrived, and how big it is in words. Pure, so they are unit-tested.
    public static class Downloads
    {
        private const long Megabyte = 1048576;

        // Server ids are GUIDs, but a file name is built from whatever arrives, so
        // everything that is not a letter, a digit, a dash or an underscore goes.
        public static string FileNameFor(string itemId)
        {
            var safe = new StringBuilder();
            foreach (char c in (itemId ?? string.Empty))
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') safe.Append(c);
            }
            if (safe.Length == 0) safe.Append("download");
            return safe.ToString() + ".mp4";
        }

        public static int Percent(long received, long total)
        {
            if (total <= 0) return 0;
            int value = (int)(Math.Min(received, total) * 100L / total);
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        public static string SizeLabel(long bytes)
        {
            if (bytes >= 1024L * Megabyte)
            {
                return (bytes / (1024.0 * Megabyte)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
            }
            int megabytes = (int)Math.Round(bytes / (double)Megabyte);
            if (megabytes < 1) megabytes = 1;
            return megabytes.ToString() + " MB";
        }
    }
}

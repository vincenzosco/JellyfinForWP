using System;
using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    public class QualityOption
    {
        public int Bitrate { get; set; }
        public string Label { get; set; }
    }

    // The bitrates the player offers. They are the same values the settings screen
    // stores; the player writes the same preference, so the two screens agree.
    public static class QualityOptions
    {
        public static readonly int[] Bitrates = { 2000000, 4000000, 8000000, 20000000 };

        public static List<QualityOption> Choices(string mbpsSuffix)
        {
            var result = new List<QualityOption>();
            foreach (int bitrate in Bitrates)
            {
                result.Add(new QualityOption
                {
                    Bitrate = bitrate,
                    Label = (bitrate / 1000000).ToString() + " " + mbpsSuffix
                });
            }
            return result;
        }

        public static int IndexOf(int bitrate)
        {
            return Array.IndexOf(Bitrates, bitrate);
        }
    }
}

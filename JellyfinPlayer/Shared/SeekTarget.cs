using System;

namespace JellyfinPlayer.Shared
{
    // Keeps every seek inside the video: never before the start, never past the
    // end, and nothing at all while the duration is still unknown (a transcode
    // that has not reported its length yet).
    public static class SeekTarget
    {
        public static TimeSpan Relative(TimeSpan position, TimeSpan delta, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) return TimeSpan.Zero;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            return Clamp(position + delta, duration);
        }

        public static TimeSpan Absolute(double seconds, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) return TimeSpan.Zero;
            if (double.IsNaN(seconds) || seconds <= 0) return TimeSpan.Zero;
            return Clamp(TimeSpan.FromSeconds(seconds), duration);
        }

        private static TimeSpan Clamp(TimeSpan value, TimeSpan duration)
        {
            if (value < TimeSpan.Zero) return TimeSpan.Zero;
            if (value > duration) return duration;
            return value;
        }
    }
}

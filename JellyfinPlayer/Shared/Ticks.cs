using System;

namespace JellyfinPlayer.Shared
{
    public static class Ticks
    {
        public const long TicksPerSecond = 10000000;

        public static TimeSpan ToTimeSpan(long ticks)
        {
            return TimeSpan.FromTicks(ticks);
        }

        public static long FromTimeSpan(TimeSpan value)
        {
            return value.Ticks;
        }

        // Returns "" for null, "h:mm:ss" when at least one hour, else "m:ss".
        public static string Format(long? ticks)
        {
            if (!ticks.HasValue) return "";
            return FormatSpan(TimeSpan.FromTicks(ticks.Value));
        }

        public static string FormatDuration(long? ticks)
        {
            return Format(ticks);
        }

        // Same rules for a TimeSpan, which is what the player measures with.
        public static string FormatSpan(TimeSpan value)
        {
            if (value.Ticks < 0) value = TimeSpan.Zero;
            if (value.TotalHours >= 1)
            {
                return string.Format("{0}:{1:00}:{2:00}", (int)Math.Floor(value.TotalHours), value.Minutes, value.Seconds);
            }
            return string.Format("{0}:{1:00}", (int)Math.Floor(value.TotalMinutes), value.Seconds);
        }
    }
}

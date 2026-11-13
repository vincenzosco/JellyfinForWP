using System;

namespace JellyfinPlayer.Shared
{
    // After a stream fails the player may try the other way - but only once, and
    // only when that URL is a different one. Without this rule two broken URLs
    // bounce the user between them and the failure sentence never arrives.
    public static class StreamFallback
    {
        public static Uri Alternative(Uri current, Uri fallback, bool alreadyRetried)
        {
            if (alreadyRetried) return null;
            if (current == null || fallback == null) return null;
            if (string.Equals(current.ToString(), fallback.ToString(), StringComparison.OrdinalIgnoreCase)) return null;
            return fallback;
        }
    }
}

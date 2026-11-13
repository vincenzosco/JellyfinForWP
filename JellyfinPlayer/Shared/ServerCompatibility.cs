namespace JellyfinPlayer.Shared
{
    // Jellyfin 12 stopped reading two things older servers still expect: the
    // legacy `api_key` query parameter and the X-Emby-Authorization header. The
    // login records the server version, and every later request is built for the
    // generation the server belongs to.
    public static class ServerCompatibility
    {
        // The first generation that reads only ApiKey and Authorization.
        public const int ModernMajorVersion = 12;

        // Legacy only when the version is known and older than 12. A server that
        // did not report a version is treated as modern: 12 refuses the legacy
        // spellings outright, while 10.x also understands the modern ones.
        public static bool IsLegacy(string serverVersion)
        {
            int major;
            return TryMajor(serverVersion, out major) && major < ModernMajorVersion;
        }

        // "10.9.11" -> 10, "12" -> 12, "10" -> 10, "dev" -> false.
        public static bool TryMajor(string serverVersion, out int major)
        {
            major = 0;
            if (string.IsNullOrEmpty(serverVersion)) return false;
            string text = serverVersion.Trim();
            int dot = text.IndexOf('.');
            string head = dot < 0 ? text : text.Substring(0, dot);
            return int.TryParse(head, out major);
        }
    }
}

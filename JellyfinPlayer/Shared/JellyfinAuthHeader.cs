namespace JellyfinPlayer.Shared
{
    // The one header name that both generations of server read.
    //
    // Jellyfin 12 disables the legacy X-Emby-Authorization / X-Emby-Token headers by
    // default, so a request that identifies itself only with them no longer carries
    // a client name: login answers 400 ("request.App" is null in the server log) and
    // every later call answers 401. "Authorization: MediaBrowser ..." is never gated
    // by that setting, on 10.x or 12.x, so it is the single path this client uses.
    public static class JellyfinAuthHeader
    {
        // Older servers (10.x) read this name; 12 reads the Authorization header
        // below. The value is the same string either way.
        public const string LegacyHeaderName = "X-Emby-Authorization";

        public const string ClientName = "JellyfinPlayer";
        public const string DeviceName = "WindowsPhone";
        public const string ClientVersion = "1.0.0";

        // Identifies the client before a token exists, i.e. for login.
        public static string Identify(string deviceId)
        {
            return "MediaBrowser Client=\"" + ClientName + "\", Device=\"" + DeviceName + "\", DeviceId=\"" + deviceId + "\", Version=\"" + ClientVersion + "\"";
        }

        // Identifies the client and carries the access token, for every later call.
        public static string WithToken(string token, string deviceId)
        {
            return "MediaBrowser Token=\"" + token + "\", Client=\"" + ClientName + "\", Device=\"" + DeviceName + "\", DeviceId=\"" + deviceId + "\", Version=\"" + ClientVersion + "\"";
        }
    }
}

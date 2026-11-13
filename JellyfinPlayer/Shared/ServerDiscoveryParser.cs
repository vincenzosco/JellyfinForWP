using System;

namespace JellyfinPlayer.Shared
{
    // Jellyfin servers answer a UDP probe on port 7359 with
    // {"Address":"http://host:8096","Id":"...","Name":"..."}.
    public static class ServerDiscoveryParser
    {
        public const int DiscoveryPort = 7359;
        public const string ProbePayload = "who is JellyfinServer?";

        // Never throws: a malformed datagram simply returns false.
        public static bool TryParse(string payload, out DiscoveredServerDto server)
        {
            server = null;
            if (string.IsNullOrEmpty(payload)) return false;
            try
            {
                var parsed = Json.Deserialize<DiscoveredServerDto>(payload);
                if (parsed == null) return false;
                if (string.IsNullOrEmpty(parsed.Address)) return false;
                server = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

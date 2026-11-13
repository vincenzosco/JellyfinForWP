using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JellyfinPlayer.Shared
{
    // UDP discovery on port 7359 answers with anything that cares to reply, and
    // that reply is only a shape ({Address, Id, Name}) - not proof of a Jellyfin
    // server. Probing the address before filling it in keeps manual control when
    // the reply came from something else.
    public static class ServerProbe
    {
        private const int MaxProbes = 3;

        public static async Task<DiscoveredServerDto> FindFirstJellyfinAsync(
            IEnumerable<DiscoveredServerDto> candidates,
            Func<string, JellyfinApiClient> createClient)
        {
            if (candidates == null || createClient == null) return null;

            foreach (var candidate in candidates
                .Where(c => c != null && !string.IsNullOrEmpty(c.Address))
                .Take(MaxProbes))
            {
                try
                {
                    if (await createClient(candidate.Address).ValidateConnectionAsync().ConfigureAwait(false))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // A candidate that does not answer, or answers with something
                    // that is not Jellyfin JSON, is simply not the server.
                }
            }

            return null;
        }
    }
}

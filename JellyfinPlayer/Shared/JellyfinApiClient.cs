using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JellyfinPlayer.Shared
{
    public class JellyfinApiClient
    {
        private readonly IHttpTransport _transport;
        private readonly JellyfinUrls _urls;
        private readonly string _deviceId;

        public string AccessToken { get; set; }
        public string UserId { get; set; }
        public string ServerId { get; set; }

        public JellyfinApiClient(IHttpTransport transport, JellyfinUrls urls, string deviceId)
        {
            _transport = transport;
            _urls = urls;
            _deviceId = deviceId;
        }

        // Both go out as "Authorization: MediaBrowser ...", the one header name Jellyfin
        // 12 still reads (see JellyfinAuthHeader). The legacy X-Emby-* pair is gone:
        // sending it is what made login fail with "request.App is null" on 12.x.
        private IDictionary<string, string> TokenHeaders()
        {
            var h = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(AccessToken)) h["Authorization"] = JellyfinAuthHeader.WithToken(AccessToken, _deviceId);
            return h;
        }

        private IDictionary<string, string> AuthHeaders()
        {
            var h = new Dictionary<string, string>();
            h["Authorization"] = JellyfinAuthHeader.Identify(_deviceId);
            return h;
        }

        // ---- system / auth -------------------------------------------------

        public async Task<ServerInfoDto> GetServerInfoAsync()
        {
            string body = await _transport.GetStringAsync(_urls.SystemInfoPublic(), null);
            return ResponseFailures.Parse<ServerInfoDto>(body);
        }

        public async Task<List<PublicUserDto>> GetPublicUsersAsync()
        {
            string body = await _transport.GetStringAsync(_urls.PublicUsers(), null);
            return ResponseFailures.Parse<List<PublicUserDto>>(body);
        }

        // Fills the name of an account the migration could not name.
        public async Task<UserDto> GetUserAsync(string userId)
        {
            string body = await _transport.GetStringAsync(_urls.User(userId), TokenHeaders());
            return ResponseFailures.Parse<UserDto>(body);
        }

        public async Task<bool> ValidateConnectionAsync()
        {
            ServerInfoDto info = null;
            try
            {
                info = await GetServerInfoAsync();
            }
            catch
            {
                return false;
            }
            return info != null && !string.IsNullOrEmpty(info.Id);
        }

        public async Task<AuthenticateResultDto> AuthenticateAsync(string username, string password)
        {
            var req = new AuthenticateRequestDto { Username = username, Pw = password, Password = password };
            string body = await _transport.PostJsonAsync(_urls.AuthenticateByName(), Json.Serialize(req), AuthHeaders());
            var result = ResponseFailures.Parse<AuthenticateResultDto>(body);
            if (result != null)
            {
                AccessToken = result.AccessToken;
                if (result.User != null) UserId = result.User.Id;
                ServerId = result.ServerId;
                _urls.Token = AccessToken;
            }
            return result;
        }

        // ---- browsing ------------------------------------------------------

        private static List<BaseItemDto> AsList(QueryResultDto<BaseItemDto> q)
        {
            if (q == null || q.Items == null) return new List<BaseItemDto>();
            return q.Items;
        }

        public async Task<List<BaseItemDto>> GetViewsAsync(string userId)
        {
            string body = await _transport.GetStringAsync(_urls.Views(userId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<QueryResultDto<BaseItemDto>> GetItemsAsync(string userId, string parentId, string includeItemTypes, int startIndex, int limit,
                                                                      string sortBy = "SortName", string sortOrder = "Ascending",
                                                                      string genreIds = null)
        {
            string body = await _transport.GetStringAsync(_urls.Items(userId, parentId, includeItemTypes, startIndex, limit, sortBy, sortOrder, genreIds), TokenHeaders());
            var q = ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body);
            if (q == null) q = new QueryResultDto<BaseItemDto>();
            if (q.Items == null) q.Items = new List<BaseItemDto>();
            return q;
        }

        public async Task<BaseItemDto> GetItemAsync(string userId, string itemId)
        {
            string body = await _transport.GetStringAsync(_urls.Item(userId, itemId), TokenHeaders());
            return ResponseFailures.Parse<BaseItemDto>(body);
        }

        public async Task<List<BaseItemDto>> GetSeasonsAsync(string seriesId, string userId)
        {
            string body = await _transport.GetStringAsync(_urls.Seasons(seriesId, userId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<List<BaseItemDto>> GetEpisodesAsync(string seriesId, string userId, string seasonId)
        {
            string body = await _transport.GetStringAsync(_urls.Episodes(seriesId, userId, seasonId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<QueryResultDto<BaseItemDto>> SearchAsync(string userId, string term)
        {
            string body = await _transport.GetStringAsync(_urls.Search(userId, term), TokenHeaders());
            var q = ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body);
            if (q == null) q = new QueryResultDto<BaseItemDto>();
            if (q.Items == null) q.Items = new List<BaseItemDto>();
            return q;
        }

        public async Task<List<BaseItemDto>> GetGenresAsync(string userId, string parentId)
        {
            string body = await _transport.GetStringAsync(_urls.Genres(userId, parentId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<List<BaseItemDto>> GetPlaylistsAsync(string userId)
        {
            string body = await _transport.GetStringAsync(_urls.Playlists(userId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        // Playlist entries are ordinary items; their order is the playlist's order.
        public async Task<List<BaseItemDto>> GetPlaylistItemsAsync(string playlistId, string userId)
        {
            string body = await _transport.GetStringAsync(_urls.PlaylistItems(playlistId, userId), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<List<BaseItemDto>> GetResumeAsync(string userId, int limit)
        {
            string body = await _transport.GetStringAsync(_urls.ResumeItems(userId, limit), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        public async Task<List<BaseItemDto>> GetNextUpAsync(string userId, int limit)
        {
            string body = await _transport.GetStringAsync(_urls.NextUp(userId, limit), TokenHeaders());
            return AsList(ResponseFailures.Parse<QueryResultDto<BaseItemDto>>(body));
        }

        // ---- playback / user data ------------------------------------------

        public async Task<PlaybackInfoResponseDto> GetPlaybackInfoAsync(string userId, string itemId, long startTicks,
                                                                       int maxBitrate = DeviceProfileBuilder.DefaultBitrate,
                                                                       int? audioStreamIndex = null,
                                                                       int? subtitleStreamIndex = null)
        {
            var req = DeviceProfileBuilder.ToRequest(userId, startTicks, maxBitrate, audioStreamIndex, subtitleStreamIndex);
            string body = await _transport.PostJsonAsync(_urls.PlaybackInfo(itemId, userId), Json.Serialize(req), TokenHeaders());
            return ResponseFailures.Parse<PlaybackInfoResponseDto>(body);
        }

        public async Task ReportPlaybackStartAsync(PlaybackProgressDto progress)
        {
            await _transport.PostJsonAsync(_urls.SessionsPlaying(), Json.Serialize(progress), TokenHeaders());
        }

        public async Task ReportPlaybackProgressAsync(PlaybackProgressDto progress)
        {
            await _transport.PostJsonAsync(_urls.SessionsProgress(), Json.Serialize(progress), TokenHeaders());
        }

        public async Task ReportPlaybackStoppedAsync(PlaybackProgressDto progress)
        {
            await _transport.PostJsonAsync(_urls.SessionsStopped(), Json.Serialize(progress), TokenHeaders());
        }

        public async Task SetPlayedAsync(string userId, string itemId, bool played)
        {
            string method = played ? "POST" : "DELETE";
            await _transport.SendAsync(method, _urls.PlayedItems(userId, itemId), null, TokenHeaders());
        }

        public async Task SetFavoriteAsync(string userId, string itemId, bool favorite)
        {
            string method = favorite ? "POST" : "DELETE";
            await _transport.SendAsync(method, _urls.FavoriteItems(userId, itemId), null, TokenHeaders());
        }
    }
}

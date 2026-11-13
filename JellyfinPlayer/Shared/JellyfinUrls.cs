using System;

namespace JellyfinPlayer.Shared
{
    // Builds every Jellyfin REST URL this client uses. The token can be set after
    // construction because the client authenticates after the URLs object exists.
    public class JellyfinUrls
    {
        public string BaseUrl { get; set; }
        public string Token { get; set; }

        public JellyfinUrls(string baseUrl, string token = null)
        {
            BaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            Token = token;
        }

        private string U(string path)
        {
            return BaseUrl + path;
        }

        // Jellyfin 12 disabled the legacy api_key query parameter together with the
        // X-Emby-* headers; ApiKey is the modern spelling and both 10.x and 12.x read
        // it. Image and media URLs need it because BitmapImage and MediaElement cannot
        // attach an Authorization header.
        private string WithKey(string url)
        {
            if (string.IsNullOrEmpty(Token)) return url;
            return url + (url.Contains("?") ? "&" : "?") + "ApiKey=" + Uri.EscapeDataString(Token);
        }

        public string SystemInfoPublic()
        {
            return U("/System/Info/Public");
        }

        public string PublicUsers()
        {
            return U("/Users/Public");
        }

        // The signed-in user's own record; the picker needs User.Name from it.
        public string User(string userId)
        {
            return WithKey(U("/Users/" + userId));
        }

        public string AuthenticateByName()
        {
            return U("/Users/AuthenticateByName");
        }

        public string Views(string userId)
        {
            return WithKey(U("/Users/" + userId + "/Views"));
        }

        public string Items(string userId, string parentId, string includeItemTypes, int startIndex, int limit, string sortBy, string sortOrder,
                            string genreIds = null)
        {
            string q = "/Users/" + userId + "/Items?Recursive=true";
            if (!string.IsNullOrEmpty(parentId)) q += "&ParentId=" + Uri.EscapeDataString(parentId);
            if (!string.IsNullOrEmpty(includeItemTypes)) q += "&IncludeItemTypes=" + Uri.EscapeDataString(includeItemTypes);
            if (!string.IsNullOrEmpty(sortBy)) q += "&SortBy=" + Uri.EscapeDataString(sortBy);
            if (!string.IsNullOrEmpty(sortOrder)) q += "&SortOrder=" + Uri.EscapeDataString(sortOrder);
            if (!string.IsNullOrEmpty(genreIds)) q += "&GenreIds=" + Uri.EscapeDataString(genreIds);
            q += "&StartIndex=" + startIndex.ToString() + "&Limit=" + limit.ToString();
            q += "&Fields=Overview,PrimaryImageAspectRatio";
            return WithKey(U(q));
        }

        // The genres a library actually contains; the id is what the item query
        // filters on.
        public string Genres(string userId, string parentId)
        {
            string q = "/Genres?UserId=" + Uri.EscapeDataString(userId);
            if (!string.IsNullOrEmpty(parentId)) q += "&ParentId=" + Uri.EscapeDataString(parentId);
            return WithKey(U(q));
        }

        public string Playlists(string userId)
        {
            return WithKey(U("/Users/" + Uri.EscapeDataString(userId) + "/Items?Recursive=true&IncludeItemTypes=Playlist&SortBy=SortName&Fields=ChildCount,PrimaryImageAspectRatio"));
        }

        public string PlaylistItems(string playlistId, string userId)
        {
            return WithKey(U("/Playlists/" + Uri.EscapeDataString(playlistId) + "/Items?UserId=" + Uri.EscapeDataString(userId) + "&Fields=PrimaryImageAspectRatio,Overview,UserData"));
        }

        public string Item(string userId, string itemId)
        {
            return WithKey(U("/Users/" + userId + "/Items/" + itemId));
        }

        public string Seasons(string seriesId, string userId)
        {
            return WithKey(U("/Shows/" + seriesId + "/Seasons?UserId=" + userId));
        }

        public string Episodes(string seriesId, string userId, string seasonId)
        {
            return WithKey(U("/Shows/" + seriesId + "/Episodes?UserId=" + userId + "&SeasonId=" + seasonId + "&Fields=Overview"));
        }

        public string Search(string userId, string term)
        {
            return WithKey(U("/Users/" + userId + "/Items?Recursive=true&IncludeItemTypes=Movie,Series,Episode&Limit=50&SearchTerm=" + Uri.EscapeDataString(term)));
        }

        public string ResumeItems(string userId, int limit)
        {
            return WithKey(U("/Users/" + userId + "/Items/Resume?MediaTypes=Video&Limit=" + limit.ToString()));
        }

        public string NextUp(string userId, int limit)
        {
            return WithKey(U("/Shows/NextUp?UserId=" + userId + "&Limit=" + limit.ToString()));
        }

        public string PlaybackInfo(string itemId, string userId)
        {
            return WithKey(U("/Items/" + itemId + "/PlaybackInfo?UserId=" + userId));
        }

        public string PlayedItems(string userId, string itemId)
        {
            return WithKey(U("/Users/" + userId + "/PlayedItems/" + itemId));
        }

        public string FavoriteItems(string userId, string itemId)
        {
            return WithKey(U("/Users/" + userId + "/FavoriteItems/" + itemId));
        }

        public string SessionsPlaying()
        {
            return WithKey(U("/Sessions/Playing"));
        }

        public string SessionsProgress()
        {
            return WithKey(U("/Sessions/Playing/Progress"));
        }

        public string SessionsStopped()
        {
            return WithKey(U("/Sessions/Playing/Stopped"));
        }

        // Streams the original file (used when the server reports SupportsDirectStream
        // but returns no DirectStreamUrl).
        public string VideoStream(string itemId, string mediaSourceId)
        {
            return WithKey(U("/Videos/" + itemId + "/stream?static=true&mediaSourceId=" + Uri.EscapeDataString(mediaSourceId ?? "")));
        }

        // Progressive MP4 transcode (WP8.1 has no AdaptiveMediaSource, so no HLS).
        // A chosen audio track other than the default, or any subtitle, is honored
        // here: the server can only switch audio or burn in a subtitle while it
        // transcodes, which is why the player uses this URL for both.
        public string VideoTranscode(string itemId, string mediaSourceId, int maxBitrate,
                                     int? audioStreamIndex = null,
                                     int? subtitleStreamIndex = null)
        {
            string q = "/Videos/" + itemId + "/stream.mp4?VideoCodec=h264&AudioCodec=aac&MaxStreamingBitrate=" + maxBitrate.ToString() + "&mediaSourceId=" + Uri.EscapeDataString(mediaSourceId ?? "");
            if (audioStreamIndex.HasValue) q += "&AudioStreamIndex=" + audioStreamIndex.Value.ToString();
            if (subtitleStreamIndex.HasValue)
            {
                q += "&SubtitleStreamIndex=" + subtitleStreamIndex.Value.ToString() + "&SubtitleMethod=Encode";
            }
            return WithKey(U(q));
        }

        public string PrimaryImage(string itemId, int maxHeight)
        {
            return WithKey(U("/Items/" + itemId + "/Images/Primary?maxHeight=" + maxHeight.ToString() + "&quality=90"));
        }

        public string BackdropImage(string itemId, int maxWidth)
        {
            return WithKey(U("/Items/" + itemId + "/Images/Backdrop/0?maxWidth=" + maxWidth.ToString() + "&quality=90"));
        }
    }
}

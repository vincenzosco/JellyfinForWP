using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    public class PlaybackPlan
    {
        public Uri Uri { get; set; }
        public bool IsTranscode { get; set; }
        public string MediaSourceId { get; set; }
        public string PlaySessionId { get; set; }
        public TimeSpan StartPosition { get; set; }
        // The streams the chosen source carries, so the player can offer them.
        public List<MediaStreamDto> Tracks { get; set; }
        // What was actually asked for; null means the default audio / no subtitle.
        public int? AudioStreamIndex { get; set; }
        public int? SubtitleStreamIndex { get; set; }

        // The other way round, tried when what we are playing fails: the original
        // file (offered by the "Play direct" button) or the transcode of the same
        // source (retried automatically, once).
        public Uri DirectFallbackUri { get; set; }
        public Uri TranscodeFallbackUri { get; set; }
    }

    public static class PlaybackService
    {
        public static async Task<PlaybackPlan> PrepareAsync(BaseItemDto item, long startTicks,
                                                           int? audioStreamIndex = null,
                                                           int? subtitleStreamIndex = null)
        {
            var session = AppSession.Current;
            var info = await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, startTicks,
                                                                        session.PreferredBitrate, audioStreamIndex, subtitleStreamIndex);
            if (info == null || info.MediaSources == null || info.MediaSources.Count == 0)
            {
                throw new JellyfinException(Localized.Text("PlaybackUnavailable"), 0);
            }

            var plan = new PlaybackPlan();
            plan.PlaySessionId = info.PlaySessionId;
            plan.StartPosition = Ticks.ToTimeSpan(startTicks);
            plan.AudioStreamIndex = audioStreamIndex;
            plan.SubtitleStreamIndex = subtitleStreamIndex;
            plan.Tracks = FirstStreams(info);

            bool tracksRequested = audioStreamIndex.HasValue || subtitleStreamIndex.HasValue;

            // A directly playable source is preferred - but only when nothing was
            // chosen: a non-default audio track or a subtitle needs the server to
            // remux or burn in, and the direct URL cannot do either.
            if (!tracksRequested)
            {
                foreach (var s in info.MediaSources)
                {
                    if (s.SupportsDirectStream || s.SupportsDirectPlay)
                    {
                        var direct = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate);
                        if (direct != null)
                        {
                            plan.Uri = new Uri(direct.Url);
                            plan.IsTranscode = false;
                            plan.MediaSourceId = direct.MediaSourceId;
                            // The picker describes the source that plays, never another one.
                            plan.Tracks = s.MediaStreams ?? new List<MediaStreamDto>();
                            FillFallback(plan, session, item.Id, s);
                            return plan;
                        }
                    }
                }
            }

            // Otherwise any source the server is willing to transcode.
            foreach (var s in info.MediaSources)
            {
                var resolved = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate,
                                                           audioStreamIndex, subtitleStreamIndex);
                if (resolved != null)
                {
                    plan.Uri = new Uri(resolved.Url);
                    plan.IsTranscode = resolved.IsTranscode;
                    plan.MediaSourceId = resolved.MediaSourceId;
                    plan.Tracks = s.MediaStreams ?? new List<MediaStreamDto>();
                    if (!tracksRequested) FillFallback(plan, session, item.Id, s);
                    return plan;
                }
            }

            throw new JellyfinException(Localized.Text("PlaybackNoSource"), 0);
        }

        // The picker is built from one source's streams; the first one the server
        // listed is the one it will use unless it has a better idea.
        private static List<MediaStreamDto> FirstStreams(PlaybackInfoResponseDto info)
        {
            foreach (var source in info.MediaSources)
            {
                if (source != null && source.MediaStreams != null) return source.MediaStreams;
            }
            return new List<MediaStreamDto>();
        }

        // The episode that follows an episode that just ended. Failures are
        // deliberately swallowed: the end of a video must never become an error
        // screen because the "next" lookup did not answer.
        public static async Task<BaseItemDto> NextEpisodeAsync(JellyfinApiClient client, string userId, BaseItemDto current)
        {
            if (client == null || current == null || string.IsNullOrEmpty(current.SeriesId)) return null;
            if (!current.ParentIndexNumber.HasValue || !current.IndexNumber.HasValue) return null;

            try
            {
                // The current season first: most next episodes live here.
                string seasonId = current.SeasonId;
                if (string.IsNullOrEmpty(seasonId))
                {
                    var allSeasons = await client.GetSeasonsAsync(current.SeriesId, userId);
                    if (allSeasons != null)
                    {
                        foreach (var season in allSeasons)
                        {
                            if (season != null && season.IndexNumber == current.ParentIndexNumber)
                            {
                                seasonId = season.Id;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(seasonId))
                {
                    var episodes = await client.GetEpisodesAsync(current.SeriesId, userId, seasonId);
                    var nextInSeason = NextEpisode.Find(current, episodes);
                    if (nextInSeason != null) return nextInSeason;
                }

                // Then the first later season that has an episode at all.
                var seasons = await client.GetSeasonsAsync(current.SeriesId, userId);
                if (seasons == null) return null;
                foreach (var season in seasons.OrderBy(s => s != null && s.IndexNumber.HasValue ? s.IndexNumber.Value : int.MaxValue))
                {
                    if (season == null || !season.IndexNumber.HasValue) continue;
                    if (season.IndexNumber.Value <= current.ParentIndexNumber.Value) continue;
                    var episodes = await client.GetEpisodesAsync(current.SeriesId, userId, season.Id);
                    var nextSeason = NextEpisode.Find(current, episodes);
                    if (nextSeason != null) return nextSeason;
                }
            }
            catch
            {
                // Offline server, a refused token, a series with nothing after this:
                // all of them mean "no next episode", not "playback broke".
            }
            return null;
        }

        // A plan carries the URL of the other way round, so a failure has somewhere
        // to go: the transcode of the same source when the original file is playing,
        // the original file itself when a transcode is playing. What the player does
        // with it - retry once on its own, or offer it behind a button - is the
        // player's decision.
        private static void FillFallback(PlaybackPlan plan, AppSession session, string itemId, MediaSourceInfoDto source)
        {
            if (plan.IsTranscode)
            {
                string direct = PlaybackUrlResolver.DirectUrl(session.BaseUrl, session.AccessToken, itemId, source);
                if (!string.IsNullOrEmpty(direct)) plan.DirectFallbackUri = new Uri(direct);
            }
            else
            {
                string transcode = PlaybackUrlResolver.TranscodeUrl(session.BaseUrl, session.AccessToken, itemId, source, session.PreferredBitrate);
                if (!string.IsNullOrEmpty(transcode)) plan.TranscodeFallbackUri = new Uri(transcode);
            }
        }
    }
}

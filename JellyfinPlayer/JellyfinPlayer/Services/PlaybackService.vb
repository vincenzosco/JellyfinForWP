Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared

Namespace Services
    Public Class PlaybackPlan
        Public Property Uri As Uri
        Public Property IsTranscode As Boolean
        Public Property MediaSourceId As String
        Public Property PlaySessionId As String
        Public Property StartPosition As TimeSpan
        ' The streams the chosen source carries, so the player can offer them.
        Public Property Tracks As List(Of MediaStreamDto)
        ' What was actually asked for; Nothing means the default audio / no subtitle.
        Public Property AudioStreamIndex As Integer?
        Public Property SubtitleStreamIndex As Integer?

        ' The other way round, tried when what we are playing fails: the original
        ' file (offered by the "Play direct" button) or the transcode of the same
        ' source (retried automatically, once).
        Public Property DirectFallbackUri As Uri
        Public Property TranscodeFallbackUri As Uri
    End Class

    Public Module PlaybackService
        Public Async Function PrepareAsync(item As BaseItemDto, startTicks As Long,
                                           Optional audioStreamIndex As Integer? = Nothing,
                                           Optional subtitleStreamIndex As Integer? = Nothing) As Task(Of PlaybackPlan)
            Dim session = AppSession.Current
            Dim info = Await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, startTicks,
                                                                         session.PreferredBitrate, audioStreamIndex, subtitleStreamIndex)
            If info Is Nothing OrElse info.MediaSources Is Nothing OrElse info.MediaSources.Count = 0 Then
                Throw New JellyfinException(Localized.Text("PlaybackUnavailable"), 0)
            End If

            Dim plan As New PlaybackPlan()
            plan.PlaySessionId = info.PlaySessionId
            plan.StartPosition = Ticks.ToTimeSpan(startTicks)
            plan.AudioStreamIndex = audioStreamIndex
            plan.SubtitleStreamIndex = subtitleStreamIndex
            plan.Tracks = FirstStreams(info)

            Dim tracksRequested = audioStreamIndex.HasValue OrElse subtitleStreamIndex.HasValue

            ' A directly playable source is preferred - but only when nothing was
            ' chosen: a non-default audio track or a subtitle needs the server to
            ' remux or burn in, and the direct URL cannot do either.
            If Not tracksRequested Then
                For Each s In info.MediaSources
                    If s.SupportsDirectStream OrElse s.SupportsDirectPlay Then
                        Dim direct = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate)
                        If direct IsNot Nothing Then
                            plan.Uri = New Uri(direct.Url)
                            plan.IsTranscode = False
                            plan.MediaSourceId = direct.MediaSourceId
                            ' The picker describes the source that plays, never another one.
                            plan.Tracks = If(s.MediaStreams, New List(Of MediaStreamDto)())
                            FillFallback(plan, session, item.Id, s)
                            Return plan
                        End If
                    End If
                Next
            End If

            ' Otherwise any source the server is willing to transcode.
            For Each s In info.MediaSources
                Dim resolved = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate,
                                                           audioStreamIndex, subtitleStreamIndex)
                If resolved IsNot Nothing Then
                    plan.Uri = New Uri(resolved.Url)
                    plan.IsTranscode = resolved.IsTranscode
                    plan.MediaSourceId = resolved.MediaSourceId
                    plan.Tracks = If(s.MediaStreams, New List(Of MediaStreamDto)())
                    If Not tracksRequested Then FillFallback(plan, session, item.Id, s)
                    Return plan
                End If
            Next

            Throw New JellyfinException(Localized.Text("PlaybackNoSource"), 0)
        End Function

        ' The picker is built from one source's streams; the first one the server
        ' listed is the one it will use unless it has a better idea.
        Private Function FirstStreams(info As PlaybackInfoResponseDto) As List(Of MediaStreamDto)
            For Each source In info.MediaSources
                If source IsNot Nothing AndAlso source.MediaStreams IsNot Nothing Then Return source.MediaStreams
            Next
            Return New List(Of MediaStreamDto)()
        End Function

        ' The episode that follows an episode that just ended. Failures are
        ' deliberately swallowed: the end of a video must never become an error
        ' screen because the "next" lookup did not answer.
        Public Async Function NextEpisodeAsync(client As JellyfinApiClient, userId As String, current As BaseItemDto) As Task(Of BaseItemDto)
            If client Is Nothing OrElse current Is Nothing OrElse String.IsNullOrEmpty(current.SeriesId) Then Return Nothing
            If Not current.ParentIndexNumber.HasValue OrElse Not current.IndexNumber.HasValue Then Return Nothing

            Try
                ' The current season first: most next episodes live here.
                Dim seasonId = current.SeasonId
                If String.IsNullOrEmpty(seasonId) Then
                    Dim allSeasons = Await client.GetSeasonsAsync(current.SeriesId, userId)
                    If allSeasons IsNot Nothing Then
                        For Each season In allSeasons
                            If season IsNot Nothing AndAlso season.IndexNumber = current.ParentIndexNumber Then
                                seasonId = season.Id
                                Exit For
                            End If
                        Next
                    End If
                End If

                If Not String.IsNullOrEmpty(seasonId) Then
                    Dim episodes = Await client.GetEpisodesAsync(current.SeriesId, userId, seasonId)
                    Dim nextInSeason = NextEpisode.Find(current, episodes)
                    If nextInSeason IsNot Nothing Then Return nextInSeason
                End If

                ' Then the first later season that has an episode at all.
                Dim seasons = Await client.GetSeasonsAsync(current.SeriesId, userId)
                If seasons Is Nothing Then Return Nothing
                For Each season In seasons.OrderBy(Function(s) If(s IsNot Nothing AndAlso s.IndexNumber.HasValue, s.IndexNumber.Value, Integer.MaxValue))
                    If season Is Nothing OrElse Not season.IndexNumber.HasValue Then Continue For
                    If season.IndexNumber.Value <= current.ParentIndexNumber.Value Then Continue For
                    Dim episodes = Await client.GetEpisodesAsync(current.SeriesId, userId, season.Id)
                    Dim nextSeason = NextEpisode.Find(current, episodes)
                    If nextSeason IsNot Nothing Then Return nextSeason
                Next
            Catch
                ' Offline server, a refused token, a series with nothing after this:
                ' all of them mean "no next episode", not "playback broke".
            End Try
            Return Nothing
        End Function

        ' A plan carries the URL of the other way round, so a failure has somewhere
        ' to go: the transcode of the same source when the original file is playing,
        ' the original file itself when a transcode is playing. What the player does
        ' with it - retry once on its own, or offer it behind a button - is the
        ' player's decision.
        Private Sub FillFallback(plan As PlaybackPlan, session As AppSession, itemId As String, source As MediaSourceInfoDto)
            If plan.IsTranscode Then
                Dim direct = PlaybackUrlResolver.DirectUrl(session.BaseUrl, session.AccessToken, itemId, source)
                If Not String.IsNullOrEmpty(direct) Then plan.DirectFallbackUri = New Uri(direct)
            Else
                Dim transcode = PlaybackUrlResolver.TranscodeUrl(session.BaseUrl, session.AccessToken, itemId, source, session.PreferredBitrate)
                If Not String.IsNullOrEmpty(transcode) Then plan.TranscodeFallbackUri = New Uri(transcode)
            End If
        End Sub
    End Module
End Namespace

Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared

Namespace Services
    Public Class PlaybackPlan
        Public Property Uri As Uri
        Public Property IsTranscode As Boolean
        Public Property MediaSourceId As String
        Public Property PlaySessionId As String
        Public Property StartPosition As TimeSpan

        ' The other way round, tried when what we are playing fails: the original
        ' file (offered by the "Play direct" button) or the transcode of the same
        ' source (retried automatically, once).
        Public Property DirectFallbackUri As Uri
        Public Property TranscodeFallbackUri As Uri
    End Class

    Public Module PlaybackService
        Public Async Function PrepareAsync(item As BaseItemDto, startTicks As Long) As Task(Of PlaybackPlan)
            Dim session = AppSession.Current
            Dim info = Await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, startTicks, session.PreferredBitrate)
            If info Is Nothing OrElse info.MediaSources Is Nothing OrElse info.MediaSources.Count = 0 Then
                Throw New JellyfinException(Localized.Text("PlaybackUnavailable"), 0)
            End If

            Dim plan As New PlaybackPlan()
            plan.PlaySessionId = info.PlaySessionId
            plan.StartPosition = Ticks.ToTimeSpan(startTicks)

            ' A directly playable source is preferred. The server often returns only
            ' flags and no URL, so PlaybackUrlResolver builds one when needed.
            For Each s In info.MediaSources
                If s.SupportsDirectStream OrElse s.SupportsDirectPlay Then
                    Dim direct = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate)
                    If direct IsNot Nothing Then
                        plan.Uri = New Uri(direct.Url)
                        plan.IsTranscode = False
                        plan.MediaSourceId = direct.MediaSourceId
                        FillFallback(plan, session, item.Id, s)
                        Return plan
                    End If
                End If
            Next

            ' Otherwise any source the server is willing to transcode.
            For Each s In info.MediaSources
                Dim resolved = PlaybackUrlResolver.Resolve(session.BaseUrl, session.AccessToken, item.Id, s, session.PreferredBitrate)
                If resolved IsNot Nothing Then
                    plan.Uri = New Uri(resolved.Url)
                    plan.IsTranscode = resolved.IsTranscode
                    plan.MediaSourceId = resolved.MediaSourceId
                    FillFallback(plan, session, item.Id, s)
                    Return plan
                End If
            Next

            Throw New JellyfinException(Localized.Text("PlaybackNoSource"), 0)
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

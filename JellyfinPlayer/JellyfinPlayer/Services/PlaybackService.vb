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
        Public Property DirectFallbackUri As Uri
    End Class

    Public Module PlaybackService
        Public Async Function PrepareAsync(item As BaseItemDto, startTicks As Long) As Task(Of PlaybackPlan)
            Dim session = AppSession.Current
            Dim info = Await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, startTicks)
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
                        plan.DirectFallbackUri = New Uri(direct.Url)
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
                    Return plan
                End If
            Next

            Throw New JellyfinException(Localized.Text("PlaybackNoSource"), 0)
        End Function
    End Module
End Namespace

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
                Throw New JellyfinException("This video cannot be played on this device.", 0)
            End If

            Dim plan As New PlaybackPlan()
            plan.PlaySessionId = info.PlaySessionId
            plan.StartPosition = Ticks.ToTimeSpan(startTicks)

            For Each s In info.MediaSources
                If s.SupportsDirectPlay AndAlso Not String.IsNullOrEmpty(s.DirectStreamUrl) Then
                    plan.DirectFallbackUri = New Uri(WithKey(s.DirectStreamUrl, session.AccessToken))
                    Exit For
                End If
            Next

            For Each s In info.MediaSources
                If s.SupportsDirectPlay AndAlso Not String.IsNullOrEmpty(s.DirectStreamUrl) Then
                    plan.Uri = New Uri(WithKey(s.DirectStreamUrl, session.AccessToken))
                    plan.IsTranscode = False
                    plan.MediaSourceId = s.Id
                    Return plan
                End If
            Next

            For Each s In info.MediaSources
                If s.SupportsTranscoding AndAlso Not String.IsNullOrEmpty(s.TranscodingUrl) Then
                    plan.Uri = New Uri(WithKey(s.TranscodingUrl, session.AccessToken))
                    plan.IsTranscode = True
                    plan.MediaSourceId = s.Id
                    Return plan
                End If
            Next

            Throw New JellyfinException("This video cannot be played on this device (transcoding is unavailable).", 0)
        End Function

        Private Function WithKey(url As String, token As String) As String
            If String.IsNullOrEmpty(url) Then Return url
            If url.Contains("api_key=") Then Return url
            Return url & If(url.Contains("?"), "&", "?") & "api_key=" & token
        End Function
    End Module
End Namespace

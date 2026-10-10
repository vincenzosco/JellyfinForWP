Imports System

Namespace Global.JellyfinPlayer.Shared
    Public Class ResolvedPlayback
        Public Property Url As String
        Public Property IsTranscode As Boolean
        Public Property MediaSourceId As String
    End Class

    ' Jellyfin does NOT always return DirectStreamUrl/TranscodingUrl: for a
    ' directly-playable file it returns only flags (SupportsDirectPlay/
    ' SupportsDirectStream/SupportsTranscoding) and expects the client to build
    ' the stream URL. This resolves whatever the server gave us into a playable URL.
    Public Module PlaybackUrlResolver
        Public Function Resolve(baseUrl As String, token As String, itemId As String, source As MediaSourceInfoDto, maxBitrate As Integer) As ResolvedPlayback
            If source Is Nothing Then Return Nothing
            Dim urls As New JellyfinUrls(baseUrl, token)
            Dim plan As New ResolvedPlayback()
            plan.MediaSourceId = source.Id

            If Not String.IsNullOrEmpty(source.DirectStreamUrl) Then
                plan.Url = EnsureKey(source.DirectStreamUrl, token)
                plan.IsTranscode = False
                Return plan
            End If

            If Not String.IsNullOrEmpty(source.TranscodingUrl) Then
                plan.Url = EnsureKey(source.TranscodingUrl, token)
                plan.IsTranscode = True
                Return plan
            End If

            If source.SupportsDirectStream OrElse source.SupportsDirectPlay Then
                plan.Url = urls.VideoStream(itemId, source.Id)
                plan.IsTranscode = False
                Return plan
            End If

            If source.SupportsTranscoding Then
                plan.Url = urls.VideoTranscode(itemId, source.Id, maxBitrate)
                plan.IsTranscode = True
                Return plan
            End If

            Return Nothing
        End Function

        Public Function EnsureKey(url As String, token As String) As String
            If String.IsNullOrEmpty(url) OrElse String.IsNullOrEmpty(token) Then Return url
            If url.Contains("api_key=") Then Return url
            Return url & If(url.Contains("?"), "&", "?") & "api_key=" & token
        End Function
    End Module
End Namespace

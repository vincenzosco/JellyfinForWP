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

        ' The two builders below produce the URL a stream falls back to after the
        ' one being played has failed - the other way round. They are only ever
        ' used as a retry, so building a URL the server may refuse is deliberate:
        ' the alternative is showing the failure sentence without trying.

        ' The transcode of a source: what the server offered, else the
        ' progressive MP4 this client asks for.
        Public Function TranscodeUrl(baseUrl As String, token As String, itemId As String,
                                     source As MediaSourceInfoDto, maxBitrate As Integer) As String
            If source Is Nothing Then Return Nothing
            If Not String.IsNullOrEmpty(source.TranscodingUrl) Then Return EnsureKey(source.TranscodingUrl, token)
            Return New JellyfinUrls(baseUrl, token).VideoTranscode(itemId, source.Id, maxBitrate)
        End Function

        ' The original file, when the server is willing to send it as it is.
        ' Nothing when the source cannot be played directly at all.
        Public Function DirectUrl(baseUrl As String, token As String, itemId As String,
                                  source As MediaSourceInfoDto) As String
            If source Is Nothing Then Return Nothing
            If Not String.IsNullOrEmpty(source.DirectStreamUrl) Then Return EnsureKey(source.DirectStreamUrl, token)
            If source.SupportsDirectStream OrElse source.SupportsDirectPlay Then
                Return New JellyfinUrls(baseUrl, token).VideoStream(itemId, source.Id)
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

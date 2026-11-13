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
        Public Function Resolve(baseUrl As String, token As String, itemId As String, source As MediaSourceInfoDto, maxBitrate As Integer,
                                Optional audioStreamIndex As Integer? = Nothing,
                                Optional subtitleStreamIndex As Integer? = Nothing) As ResolvedPlayback
            If source Is Nothing Then Return Nothing
            Dim urls As New JellyfinUrls(baseUrl, token)
            Dim plan As New ResolvedPlayback()
            plan.MediaSourceId = source.Id
            ' A chosen audio track or subtitle can only be honored while the server
            ' remuxes or burns in, so those requests skip every direct-play branch.
            Dim tracksRequested = audioStreamIndex.HasValue OrElse subtitleStreamIndex.HasValue

            If Not tracksRequested Then
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
            End If

            If source.SupportsTranscoding Then
                plan.Url = WithTracks(urls.VideoTranscode(itemId, source.Id, maxBitrate, audioStreamIndex, subtitleStreamIndex),
                                      audioStreamIndex, subtitleStreamIndex)
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
                                     source As MediaSourceInfoDto, maxBitrate As Integer,
                                     Optional audioStreamIndex As Integer? = Nothing,
                                     Optional subtitleStreamIndex As Integer? = Nothing) As String
            If source Is Nothing Then Return Nothing
            If Not String.IsNullOrEmpty(source.TranscodingUrl) Then
                Return WithTracks(EnsureKey(source.TranscodingUrl, token), audioStreamIndex, subtitleStreamIndex)
            End If
            Return New JellyfinUrls(baseUrl, token).VideoTranscode(itemId, source.Id, maxBitrate, audioStreamIndex, subtitleStreamIndex)
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

        ' Adds the chosen tracks to a URL the server itself produced, without
        ' duplicating a parameter it already carries.
        Private Function WithTracks(url As String, audioStreamIndex As Integer?, subtitleStreamIndex As Integer?) As String
            If String.IsNullOrEmpty(url) Then Return url
            Dim result = url
            If audioStreamIndex.HasValue AndAlso result.IndexOf("AudioStreamIndex=", StringComparison.OrdinalIgnoreCase) < 0 Then
                result &= If(result.Contains("?"), "&", "?") & "AudioStreamIndex=" & audioStreamIndex.Value.ToString()
            End If
            If subtitleStreamIndex.HasValue AndAlso result.IndexOf("SubtitleStreamIndex=", StringComparison.OrdinalIgnoreCase) < 0 Then
                result &= If(result.Contains("?"), "&", "?") & "SubtitleStreamIndex=" & subtitleStreamIndex.Value.ToString() & "&SubtitleMethod=Encode"
            End If
            Return result
        End Function

        Public Function EnsureKey(url As String, token As String) As String
            If String.IsNullOrEmpty(url) OrElse String.IsNullOrEmpty(token) Then Return url
            ' ApiKey is the parameter Jellyfin 12 still honors; a URL the server
            ' already signed with either spelling is left alone so the key is not
            ' sent twice.
            If url.IndexOf("ApiKey=", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               url.Contains("api_key=") Then Return url
            Return url & If(url.Contains("?"), "&", "?") & "ApiKey=" & token
        End Function
    End Module
End Namespace

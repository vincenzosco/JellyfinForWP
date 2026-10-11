Imports System

Namespace Global.JellyfinPlayer.Shared
    ' Builds every Jellyfin REST URL this client uses. The token can be set after
    ' construction because the client authenticates after the URLs object exists.
    Public Class JellyfinUrls
        Public Property BaseUrl As String
        Public Property Token As String

        Public Sub New(baseUrl As String, Optional token As String = Nothing)
            Me.BaseUrl = If(baseUrl, String.Empty).TrimEnd("/"c)
            Me.Token = token
        End Sub

        Private Function U(path As String) As String
            Return BaseUrl & path
        End Function

        Private Function WithKey(url As String) As String
            If String.IsNullOrEmpty(Token) Then Return url
            Return url & If(url.Contains("?"), "&", "?") & "api_key=" & Uri.EscapeDataString(Token)
        End Function

        Public Function SystemInfoPublic() As String
            Return U("/System/Info/Public")
        End Function

        Public Function PublicUsers() As String
            Return U("/Users/Public")
        End Function

        ' The signed-in user's own record; the picker needs User.Name from it.
        Public Function User(userId As String) As String
            Return WithKey(U("/Users/" & userId))
        End Function

        Public Function AuthenticateByName() As String
            Return U("/Users/AuthenticateByName")
        End Function

        Public Function Views(userId As String) As String
            Return WithKey(U("/Users/" & userId & "/Views"))
        End Function

        Public Function Items(userId As String, parentId As String, includeItemTypes As String, startIndex As Integer, limit As Integer, sortBy As String, sortOrder As String) As String
            Dim q As String = "/Users/" & userId & "/Items?Recursive=true"
            If Not String.IsNullOrEmpty(parentId) Then q &= "&ParentId=" & Uri.EscapeDataString(parentId)
            If Not String.IsNullOrEmpty(includeItemTypes) Then q &= "&IncludeItemTypes=" & Uri.EscapeDataString(includeItemTypes)
            If Not String.IsNullOrEmpty(sortBy) Then q &= "&SortBy=" & Uri.EscapeDataString(sortBy)
            If Not String.IsNullOrEmpty(sortOrder) Then q &= "&SortOrder=" & Uri.EscapeDataString(sortOrder)
            q &= "&StartIndex=" & startIndex.ToString() & "&Limit=" & limit.ToString()
            q &= "&Fields=Overview,PrimaryImageAspectRatio"
            Return WithKey(U(q))
        End Function

        Public Function Item(userId As String, itemId As String) As String
            Return WithKey(U("/Users/" & userId & "/Items/" & itemId))
        End Function

        Public Function Seasons(seriesId As String, userId As String) As String
            Return WithKey(U("/Shows/" & seriesId & "/Seasons?UserId=" & userId))
        End Function

        Public Function Episodes(seriesId As String, userId As String, seasonId As String) As String
            Return WithKey(U("/Shows/" & seriesId & "/Episodes?UserId=" & userId & "&SeasonId=" & seasonId & "&Fields=Overview"))
        End Function

        Public Function Search(userId As String, term As String) As String
            Return WithKey(U("/Users/" & userId & "/Items?Recursive=true&IncludeItemTypes=Movie,Series,Episode&Limit=50&SearchTerm=" & Uri.EscapeDataString(term)))
        End Function

        Public Function ResumeItems(userId As String, limit As Integer) As String
            Return WithKey(U("/Users/" & userId & "/Items/Resume?MediaTypes=Video&Limit=" & limit.ToString()))
        End Function

        Public Function NextUp(userId As String, limit As Integer) As String
            Return WithKey(U("/Shows/NextUp?UserId=" & userId & "&Limit=" & limit.ToString()))
        End Function

        Public Function PlaybackInfo(itemId As String, userId As String) As String
            Return WithKey(U("/Items/" & itemId & "/PlaybackInfo?UserId=" & userId))
        End Function

        Public Function PlayedItems(userId As String, itemId As String) As String
            Return WithKey(U("/Users/" & userId & "/PlayedItems/" & itemId))
        End Function

        Public Function FavoriteItems(userId As String, itemId As String) As String
            Return WithKey(U("/Users/" & userId & "/FavoriteItems/" & itemId))
        End Function

        Public Function SessionsPlaying() As String
            Return WithKey(U("/Sessions/Playing"))
        End Function

        Public Function SessionsProgress() As String
            Return WithKey(U("/Sessions/Playing/Progress"))
        End Function

        Public Function SessionsStopped() As String
            Return WithKey(U("/Sessions/Playing/Stopped"))
        End Function

        ' Streams the original file (used when the server reports SupportsDirectStream
        ' but returns no DirectStreamUrl).
        Public Function VideoStream(itemId As String, mediaSourceId As String) As String
            Return WithKey(U("/Videos/" & itemId & "/stream?static=true&mediaSourceId=" & Uri.EscapeDataString(If(mediaSourceId, ""))))
        End Function

        ' Progressive MP4 transcode (WP8.1 has no AdaptiveMediaSource, so no HLS).
        Public Function VideoTranscode(itemId As String, mediaSourceId As String, maxBitrate As Integer) As String
            Return WithKey(U("/Videos/" & itemId & "/stream.mp4?VideoCodec=h264&AudioCodec=aac&MaxStreamingBitrate=" & maxBitrate.ToString() & "&mediaSourceId=" & Uri.EscapeDataString(If(mediaSourceId, ""))))
        End Function

        Public Function PrimaryImage(itemId As String, maxHeight As Integer) As String
            Return WithKey(U("/Items/" & itemId & "/Images/Primary?maxHeight=" & maxHeight.ToString() & "&quality=90"))
        End Function

        Public Function BackdropImage(itemId As String, maxWidth As Integer) As String
            Return WithKey(U("/Items/" & itemId & "/Images/Backdrop/0?maxWidth=" & maxWidth.ToString() & "&quality=90"))
        End Function
    End Class
End Namespace

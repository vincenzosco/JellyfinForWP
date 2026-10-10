Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

Namespace Global.JellyfinPlayer.Shared
    Public Class JellyfinApiClient
        Private ReadOnly _transport As IHttpTransport
        Private ReadOnly _urls As JellyfinUrls
        Private ReadOnly _deviceId As String

        Public Property AccessToken As String
        Public Property UserId As String
        Public Property ServerId As String

        Public Sub New(transport As IHttpTransport, urls As JellyfinUrls, deviceId As String)
            _transport = transport
            _urls = urls
            _deviceId = deviceId
        End Sub

        Private Function AuthHeader() As String
            Return "MediaBrowser Client=""JellyfinPlayer"", Device=""WindowsPhone"", DeviceId=""" & _deviceId & """, Version=""1.0.0"""
        End Function

        Private Function TokenHeaders() As IDictionary(Of String, String)
            Dim h As New Dictionary(Of String, String)()
            If Not String.IsNullOrEmpty(AccessToken) Then h("X-Emby-Token") = AccessToken
            Return h
        End Function

        Private Function AuthHeaders() As IDictionary(Of String, String)
            Dim h As New Dictionary(Of String, String)()
            h("X-Emby-Authorization") = AuthHeader()
            Return h
        End Function

        ' ---- system / auth -------------------------------------------------

        Public Async Function GetServerInfoAsync() As Task(Of ServerInfoDto)
            Dim body As String = Await _transport.GetStringAsync(_urls.SystemInfoPublic(), Nothing)
            Return Json.Deserialize(Of ServerInfoDto)(body)
        End Function

        Public Async Function GetPublicUsersAsync() As Task(Of List(Of PublicUserDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.PublicUsers(), Nothing)
            Return Json.Deserialize(Of List(Of PublicUserDto))(body)
        End Function

        Public Async Function ValidateConnectionAsync() As Task(Of Boolean)
            Dim info As ServerInfoDto = Nothing
            Try
                info = Await GetServerInfoAsync()
            Catch
                Return False
            End Try
            Return info IsNot Nothing AndAlso Not String.IsNullOrEmpty(info.Id)
        End Function

        Public Async Function AuthenticateAsync(username As String, password As String) As Task(Of AuthenticateResultDto)
            Dim req As New AuthenticateRequestDto With {.Username = username, .Pw = password}
            Dim body As String = Await _transport.PostJsonAsync(_urls.AuthenticateByName(), Json.Serialize(req), AuthHeaders())
            Dim result = Json.Deserialize(Of AuthenticateResultDto)(body)
            If result IsNot Nothing Then
                AccessToken = result.AccessToken
                If result.User IsNot Nothing Then UserId = result.User.Id
                ServerId = result.ServerId
                _urls.Token = AccessToken
            End If
            Return result
        End Function

        ' ---- browsing ------------------------------------------------------

        Private Shared Function AsList(q As QueryResultDto(Of BaseItemDto)) As List(Of BaseItemDto)
            If q Is Nothing OrElse q.Items Is Nothing Then Return New List(Of BaseItemDto)()
            Return q.Items
        End Function

        Public Async Function GetViewsAsync(userId As String) As Task(Of List(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.Views(userId), TokenHeaders())
            Return AsList(Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body))
        End Function

        Public Async Function GetItemsAsync(userId As String, parentId As String, includeItemTypes As String, startIndex As Integer, limit As Integer) As Task(Of QueryResultDto(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.Items(userId, parentId, includeItemTypes, startIndex, limit, "SortName", "Ascending"), TokenHeaders())
            Dim q = Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body)
            If q Is Nothing Then q = New QueryResultDto(Of BaseItemDto)()
            If q.Items Is Nothing Then q.Items = New List(Of BaseItemDto)()
            Return q
        End Function

        Public Async Function GetItemAsync(userId As String, itemId As String) As Task(Of BaseItemDto)
            Dim body As String = Await _transport.GetStringAsync(_urls.Item(userId, itemId), TokenHeaders())
            Return Json.Deserialize(Of BaseItemDto)(body)
        End Function

        Public Async Function GetSeasonsAsync(seriesId As String, userId As String) As Task(Of List(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.Seasons(seriesId, userId), TokenHeaders())
            Return AsList(Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body))
        End Function

        Public Async Function GetEpisodesAsync(seriesId As String, userId As String, seasonId As String) As Task(Of List(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.Episodes(seriesId, userId, seasonId), TokenHeaders())
            Return AsList(Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body))
        End Function

        Public Async Function SearchAsync(userId As String, term As String) As Task(Of QueryResultDto(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.Search(userId, term), TokenHeaders())
            Dim q = Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body)
            If q Is Nothing Then q = New QueryResultDto(Of BaseItemDto)()
            If q.Items Is Nothing Then q.Items = New List(Of BaseItemDto)()
            Return q
        End Function

        Public Async Function GetResumeAsync(userId As String, limit As Integer) As Task(Of List(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.ResumeItems(userId, limit), TokenHeaders())
            Return AsList(Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body))
        End Function

        Public Async Function GetNextUpAsync(userId As String, limit As Integer) As Task(Of List(Of BaseItemDto))
            Dim body As String = Await _transport.GetStringAsync(_urls.NextUp(userId, limit), TokenHeaders())
            Return AsList(Json.Deserialize(Of QueryResultDto(Of BaseItemDto))(body))
        End Function

        ' ---- playback / user data ------------------------------------------

        Public Async Function GetPlaybackInfoAsync(userId As String, itemId As String, startTicks As Long) As Task(Of PlaybackInfoResponseDto)
            Dim req = DeviceProfileBuilder.ToRequest(userId, startTicks)
            Dim body As String = Await _transport.PostJsonAsync(_urls.PlaybackInfo(itemId, userId), Json.Serialize(req), TokenHeaders())
            Return Json.Deserialize(Of PlaybackInfoResponseDto)(body)
        End Function

        Public Async Function ReportPlaybackStartAsync(progress As PlaybackProgressDto) As Task
            Await _transport.PostJsonAsync(_urls.SessionsPlaying(), Json.Serialize(progress), TokenHeaders())
        End Function

        Public Async Function ReportPlaybackProgressAsync(progress As PlaybackProgressDto) As Task
            Await _transport.PostJsonAsync(_urls.SessionsProgress(), Json.Serialize(progress), TokenHeaders())
        End Function

        Public Async Function ReportPlaybackStoppedAsync(progress As PlaybackProgressDto) As Task
            Await _transport.PostJsonAsync(_urls.SessionsStopped(), Json.Serialize(progress), TokenHeaders())
        End Function

        Public Async Function SetPlayedAsync(userId As String, itemId As String, played As Boolean) As Task
            Dim method As String = If(played, "POST", "DELETE")
            Await _transport.SendAsync(method, _urls.PlayedItems(userId, itemId), Nothing, TokenHeaders())
        End Function

        Public Async Function SetFavoriteAsync(userId As String, itemId As String, favorite As Boolean) As Task
            Dim method As String = If(favorite, "POST", "DELETE")
            Await _transport.SendAsync(method, _urls.FavoriteItems(userId, itemId), Nothing, TokenHeaders())
        End Function
    End Class
End Namespace

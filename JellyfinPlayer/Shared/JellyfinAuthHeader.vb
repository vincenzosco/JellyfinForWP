Namespace Global.JellyfinPlayer.Shared
    ' The one header name that both generations of server read.
    '
    ' Jellyfin 12 disables the legacy X-Emby-Authorization / X-Emby-Token headers by
    ' default (ServerConfiguration.EnableLegacyAuthorization, off in 12.0 and force
    ' disabled on upgrade), so a request that identifies itself only with them no
    ' longer carries a client name: login answers 400 ("request.App" is null in the
    ' server log) and every later call answers 401. "Authorization: MediaBrowser ..."
    ' is never gated by that setting, on 10.x or 12.x, so it is the single path this
    ' client uses instead of detecting the server version.
    Public Module JellyfinAuthHeader
        Public Const ClientName As String = "JellyfinPlayer"
        Public Const DeviceName As String = "WindowsPhone"
        Public Const ClientVersion As String = "1.0.0"

        ' Identifies the client before a token exists, i.e. for login.
        Public Function Identify(deviceId As String) As String
            Return "MediaBrowser Client=""" & ClientName & """, Device=""" & DeviceName & """, DeviceId=""" & deviceId & """, Version=""" & ClientVersion & """"
        End Function

        ' Identifies the client and carries the access token, for every later call.
        Public Function WithToken(token As String, deviceId As String) As String
            Return "MediaBrowser Token=""" & token & """, Client=""" & ClientName & """, Device=""" & DeviceName & """, DeviceId=""" & deviceId & """, Version=""" & ClientVersion & """"
        End Function
    End Module
End Namespace

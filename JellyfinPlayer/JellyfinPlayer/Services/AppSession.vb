Imports System
Imports System.Collections.Generic
Imports Windows.Storage
Imports JellyfinPlayer.Shared

Namespace Services
    ' Persists the server/token/user and the generated device id in LocalSettings.
    Public Class AppSession
        Private Shared _current As AppSession

        Public Shared ReadOnly Property Current As AppSession
            Get
                If _current Is Nothing Then _current = New AppSession()
                Return _current
            End Get
        End Property

        Private Const KBaseUrl As String = "JellyfinBaseUrl"
        Private Const KAccessToken As String = "JellyfinAccessToken"
        Private Const KUserId As String = "JellyfinUserId"
        Private Const KServerId As String = "JellyfinServerId"
        Private Const KDeviceId As String = "JellyfinDeviceId"
        Private Const KBitrate As String = "JellyfinBitrate"

        Public Property BaseUrl As String
        Public Property AccessToken As String
        Public Property UserId As String
        Public Property ServerId As String
        Public Property DeviceId As String
        Public Property PreferredBitrate As Integer = DeviceProfileBuilder.DefaultBitrate

        Public ReadOnly Property IsLoggedIn As Boolean
            Get
                Return Not String.IsNullOrEmpty(AccessToken) AndAlso Not String.IsNullOrEmpty(BaseUrl)
            End Get
        End Property

        Public Sub Load()
            Dim s = ApplicationData.Current.LocalSettings.Values
            BaseUrl = ReadString(s, KBaseUrl)
            AccessToken = ReadString(s, KAccessToken)
            UserId = ReadString(s, KUserId)
            ServerId = ReadString(s, KServerId)
            DeviceId = ReadString(s, KDeviceId)
            If String.IsNullOrEmpty(DeviceId) Then
                DeviceId = Guid.NewGuid().ToString("N")
                s(KDeviceId) = DeviceId
            End If

            Dim raw As Object = Nothing
            If s.TryGetValue(KBitrate, raw) AndAlso raw IsNot Nothing Then
                PreferredBitrate = CInt(raw)
            Else
                PreferredBitrate = DeviceProfileBuilder.DefaultBitrate
            End If
        End Sub

        Public Sub Save()
            Dim s = ApplicationData.Current.LocalSettings.Values
            s(KBaseUrl) = BaseUrl
            s(KAccessToken) = AccessToken
            s(KUserId) = UserId
            s(KServerId) = ServerId
            s(KDeviceId) = DeviceId
            s(KBitrate) = PreferredBitrate
        End Sub

        ' Removes every credential but keeps the device id.
        Public Sub Clear()
            Dim s = ApplicationData.Current.LocalSettings.Values
            For Each key In New String() {KBaseUrl, KAccessToken, KUserId, KServerId, KBitrate}
                If s.ContainsKey(key) Then s.Remove(key)
            Next
            BaseUrl = Nothing
            AccessToken = Nothing
            UserId = Nothing
            ServerId = Nothing
        End Sub

        Public Function CreateClient() As JellyfinApiClient
            Dim urls As New JellyfinUrls(BaseUrl, AccessToken)
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, DeviceId)
            client.AccessToken = AccessToken
            client.UserId = UserId
            client.ServerId = ServerId
            Return client
        End Function

        Private Shared Function ReadString(s As IDictionary(Of String, Object), key As String) As String
            Dim v As Object = Nothing
            If s.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return v.ToString()
            Return Nothing
        End Function
    End Class
End Namespace

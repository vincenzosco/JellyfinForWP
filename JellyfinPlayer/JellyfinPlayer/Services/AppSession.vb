Imports System
Imports System.Collections.Generic
Imports Windows.Storage
Imports JellyfinPlayer.Shared

Namespace Services
    ' What the pages see: the active account plus the device-wide settings. The
    ' accounts themselves live in the AccountStore, so a page that only ever
    ' calls CreateClient() does not know how many logins are saved.
    Public Class AppSession
        Private Shared _current As AppSession

        Public Shared ReadOnly Property Current As AppSession
            Get
                If _current Is Nothing Then _current = New AppSession()
                Return _current
            End Get
        End Property

        Private Const KDeviceId As String = "JellyfinDeviceId"
        Private Const KBitrate As String = "JellyfinBitrate"
        ' The single-session keys of the previous build: read once by Load() to
        ' migrate them, then removed.
        Private Const KLegacyBaseUrl As String = "JellyfinBaseUrl"
        Private Const KLegacyAccessToken As String = "JellyfinAccessToken"
        Private Const KLegacyUserId As String = "JellyfinUserId"
        Private Const KLegacyServerId As String = "JellyfinServerId"

        Private ReadOnly _store As AccountStore

        Public Sub New()
            _store = New AccountStore(New LocalAccountStorage())
        End Sub

        Public ReadOnly Property Store As AccountStore
            Get
                Return _store
            End Get
        End Property

        Public ReadOnly Property Accounts As List(Of AccountDto)
            Get
                Return _store.Accounts
            End Get
        End Property

        Public ReadOnly Property ActiveAccount As AccountDto
            Get
                Return _store.Active
            End Get
        End Property

        Public Property DeviceId As String
        Public Property PreferredBitrate As Integer = DeviceProfileBuilder.DefaultBitrate

        Public ReadOnly Property BaseUrl As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.BaseUrl)
            End Get
        End Property

        Public ReadOnly Property AccessToken As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.AccessToken)
            End Get
        End Property

        Public ReadOnly Property UserId As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.UserId)
            End Get
        End Property

        Public ReadOnly Property ServerId As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.ServerId)
            End Get
        End Property

        Public ReadOnly Property IsLoggedIn As Boolean
            Get
                Return Not String.IsNullOrEmpty(AccessToken) AndAlso Not String.IsNullOrEmpty(BaseUrl)
            End Get
        End Property

        Public Sub Load()
            Dim s = ApplicationData.Current.LocalSettings.Values
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

            ' An upgrade arrives with four loose keys and no document.
            _store.MigrateFromLegacy(ReadString(s, KLegacyBaseUrl), ReadString(s, KLegacyAccessToken),
                                     ReadString(s, KLegacyUserId), ReadString(s, KLegacyServerId))
            For Each key In New String() {KLegacyBaseUrl, KLegacyAccessToken, KLegacyUserId, KLegacyServerId}
                If s.ContainsKey(key) Then s.Remove(key)
            Next
        End Sub

        Public Sub Save()
            Dim s = ApplicationData.Current.LocalSettings.Values
            s(KDeviceId) = DeviceId
            s(KBitrate) = PreferredBitrate
            _store.Save()
        End Sub

        ' A successful login: the same server and user refreshes that entry.
        Public Function SignIn(baseUrl As String, userId As String, userName As String, token As String, serverId As String, serverName As String) As AccountDto
            Return _store.AddOrUpdate(baseUrl, userId, userName, token, serverId, serverName)
        End Function

        Public Sub SwitchTo(id As String)
            _store.SetActive(id)
        End Sub

        ' Leaving an account keeps it (and its token) saved.
        Public Sub SignOut()
            _store.ClearActive()
        End Sub

        Public Sub RemoveAccount(id As String)
            _store.Remove(id)
        End Sub

        Public Sub SetUserName(id As String, userName As String)
            _store.SetUserName(id, userName)
        End Sub

        Public Function CreateClient() As JellyfinApiClient
            Dim account = ActiveAccount
            Dim urls As New JellyfinUrls(If(account Is Nothing, Nothing, account.BaseUrl),
                                         If(account Is Nothing, Nothing, account.AccessToken))
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, DeviceId)
            If account IsNot Nothing Then
                client.AccessToken = account.AccessToken
                client.UserId = account.UserId
                client.ServerId = account.ServerId
            End If
            Return client
        End Function

        Private Shared Function ReadString(s As IDictionary(Of String, Object), key As String) As String
            Dim v As Object = Nothing
            If s.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return v.ToString()
            Return Nothing
        End Function
    End Class
End Namespace

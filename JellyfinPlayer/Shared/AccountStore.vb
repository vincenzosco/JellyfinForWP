Imports System
Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' The saved logins and which one is in use. Pure: no WinRT, no network.
    Public Class AccountStore
        Private ReadOnly _storage As IAccountStorage
        Private ReadOnly _accounts As New List(Of AccountDto)()
        Private _activeId As String

        Public Sub New(storage As IAccountStorage)
            _storage = storage
            Load()
        End Sub

        Public ReadOnly Property Accounts As List(Of AccountDto)
            Get
                Return _accounts
            End Get
        End Property

        Public ReadOnly Property ActiveId As String
            Get
                Return _activeId
            End Get
        End Property

        Public ReadOnly Property Active As AccountDto
            Get
                Return ById(_activeId)
            End Get
        End Property

        ' Logging in as the same user on the same server refreshes that entry
        ' instead of adding a second one.
        Public Function AddOrUpdate(baseUrl As String, userId As String, userName As String, token As String, serverId As String, serverName As String) As AccountDto
            Dim normalized As String = Normalize(baseUrl)
            Dim account = Find(normalized, userId)
            If account Is Nothing Then
                account = New AccountDto With {.Id = Guid.NewGuid().ToString("N")}
                _accounts.Add(account)
            End If
            account.BaseUrl = normalized
            account.UserId = userId
            account.UserName = userName
            account.AccessToken = token
            account.ServerId = serverId
            If Not String.IsNullOrEmpty(serverName) Then account.ServerName = serverName
            _activeId = account.Id
            Save()
            Return account
        End Function

        Public Sub SetActive(id As String)
            _activeId = id
            Save()
        End Sub

        ' Leaving an account does not forget it: the token stays, so coming back
        ' asks for no password.
        Public Sub ClearActive()
            _activeId = Nothing
            Save()
        End Sub

        Public Sub Remove(id As String)
            For i = _accounts.Count - 1 To 0 Step -1
                If _accounts(i).Id = id Then _accounts.RemoveAt(i)
            Next
            If _activeId = id Then _activeId = Nothing
            Save()
        End Sub

        ' For a name the migration could not know - the token is left alone.
        Public Sub SetUserName(id As String, userName As String)
            Dim account = ById(id)
            If account Is Nothing OrElse String.IsNullOrEmpty(userName) Then Return
            account.UserName = userName
            Save()
        End Sub

        Public Function Find(baseUrl As String, userId As String) As AccountDto
            Dim normalized As String = Normalize(baseUrl)
            For Each account In _accounts
                If String.Equals(account.BaseUrl, normalized, StringComparison.OrdinalIgnoreCase) AndAlso account.UserId = userId Then Return account
            Next
            Return Nothing
        End Function

        Public Function ById(id As String) As AccountDto
            If String.IsNullOrEmpty(id) Then Return Nothing
            For Each account In _accounts
                If account.Id = id Then Return account
            Next
            Return Nothing
        End Function

        ' An app upgraded from the single-session build still has its four loose
        ' keys: they become the first account, active, so the upgrade keeps the
        ' session. Only ever runs when there are no accounts yet.
        Public Function MigrateFromLegacy(baseUrl As String, token As String, userId As String, serverId As String) As AccountDto
            If String.IsNullOrEmpty(baseUrl) OrElse String.IsNullOrEmpty(token) Then Return Nothing
            If _accounts.Count > 0 Then Return Nothing
            Return AddOrUpdate(baseUrl, userId, Nothing, token, serverId, Nothing)
        End Function

        ' ---- persistence ----

        Public Sub Load()
            _accounts.Clear()
            _activeId = Nothing
            Dim raw As String = Nothing
            Try
                raw = _storage.Load()
            Catch
                ' Unreadable storage: start empty and let the login screen show.
                Return
            End Try
            If String.IsNullOrEmpty(raw) Then Return
            Dim document As AccountDocumentDto = Nothing
            Try
                document = Json.Deserialize(Of AccountDocumentDto)(raw)
            Catch
                ' A corrupt document counts as no accounts; the next save
                ' overwrites it.
                Return
            End Try
            If document Is Nothing OrElse document.Accounts Is Nothing Then Return
            For Each account In document.Accounts
                If account IsNot Nothing AndAlso Not String.IsNullOrEmpty(account.Id) Then _accounts.Add(account)
            Next
            ' A pointer to an account that is not there means nothing is active.
            If ById(document.ActiveId) IsNot Nothing Then _activeId = document.ActiveId
        End Sub

        Public Sub Save()
            Dim document As New AccountDocumentDto With {.Accounts = _accounts, .ActiveId = _activeId}
            _storage.Save(Json.Serialize(document))
        End Sub

        ' Same normalization JellyfinUrls applies to the base address.
        Public Shared Function Normalize(baseUrl As String) As String
            Return If(baseUrl, String.Empty).Trim().TrimEnd("/"c)
        End Function
    End Class
End Namespace

Imports JellyfinPlayer.Shared
Imports System.IO.IsolatedStorage

Namespace Services
    ' The one and only place the account document meets app storage. One value
    ' holds the whole document, so a save is a single atomic write. Silverlight
    ' stores it in IsolatedStorageSettings, and every access is serialised because
    ' the settings collection is not thread-safe.
    Public Class LocalAccountStorage
        Implements IAccountStorage

        Public Const Key As String = "JellyfinAccounts"
        Private Shared ReadOnly Sync As New Object()

        Public Function Load() As String Implements IAccountStorage.Load
            SyncLock Sync
                Dim values = IsolatedStorageSettings.ApplicationSettings
                Dim raw As Object = Nothing
                If values.TryGetValue(Key, raw) AndAlso raw IsNot Nothing Then Return raw.ToString()
                Return Nothing
            End SyncLock
        End Function

        Public Sub Save(json As String) Implements IAccountStorage.Save
            SyncLock Sync
                Dim values = IsolatedStorageSettings.ApplicationSettings
                values(Key) = json
                values.Save()
            End SyncLock
        End Sub
    End Class
End Namespace

Imports JellyfinPlayer.Shared
Imports Windows.Storage

Namespace Services
    ' The one and only place the account document meets LocalSettings. One value
    ' holds the whole document, so a save is a single atomic write.
    Public Class LocalAccountStorage
        Implements IAccountStorage

        Public Const Key As String = "JellyfinAccounts"

        Public Function Load() As String Implements IAccountStorage.Load
            Dim values = ApplicationData.Current.LocalSettings.Values
            Dim raw As Object = Nothing
            If values.TryGetValue(Key, raw) AndAlso raw IsNot Nothing Then Return raw.ToString()
            Return Nothing
        End Function

        Public Sub Save(json As String) Implements IAccountStorage.Save
            ApplicationData.Current.LocalSettings.Values(Key) = json
        End Sub
    End Class
End Namespace

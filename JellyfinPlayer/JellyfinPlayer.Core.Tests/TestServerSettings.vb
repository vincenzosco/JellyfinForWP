Imports System
Imports System.Collections.Generic
Imports System.IO

' Connection settings for the integration tests.
' The password is never committed: it comes from the environment or from the
' git-ignored test.settings.local (copied next to the test assembly).
Public Class TestServerSettings
    Public Property Url As String
    Public Property User As String
    Public Property Password As String

    Public ReadOnly Property IsConfigured As Boolean
        Get
            Return Not String.IsNullOrEmpty(Url) AndAlso Not String.IsNullOrEmpty(User) AndAlso Not String.IsNullOrEmpty(Password)
        End Get
    End Property

    Public Shared Function Load() As TestServerSettings
        Dim s As New TestServerSettings()
        s.Url = Environment.GetEnvironmentVariable("JELLYFIN_TEST_URL")
        s.User = Environment.GetEnvironmentVariable("JELLYFIN_TEST_USER")
        s.Password = Environment.GetEnvironmentVariable("JELLYFIN_TEST_PASSWORD")

        Dim settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.settings.local")
        If File.Exists(settingsPath) Then
            Dim values = ParseSettings(File.ReadAllText(settingsPath))
            If String.IsNullOrEmpty(s.Url) Then s.Url = GetValue(values, "url")
            If String.IsNullOrEmpty(s.User) Then s.User = GetValue(values, "user")
            If String.IsNullOrEmpty(s.Password) Then s.Password = GetValue(values, "password")
        End If

        If String.IsNullOrEmpty(s.Url) Then s.Url = "http://192.168.0.108:8096"
        If String.IsNullOrEmpty(s.User) Then s.User = "vincenzo"
        If s.Password Is Nothing Then s.Password = ""
        Return s
    End Function

    ' key=value per line; '#' starts a comment; blank and malformed lines are ignored.
    Public Shared Function ParseSettings(text As String) As Dictionary(Of String, String)
        Dim result As New Dictionary(Of String, String)()
        If String.IsNullOrEmpty(text) Then Return result
        For Each rawLine In text.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
            Dim line = rawLine.Trim()
            If line.Length = 0 OrElse line.StartsWith("#") Then Continue For
            Dim idx = line.IndexOf("="c)
            If idx <= 0 Then Continue For
            Dim key = line.Substring(0, idx).Trim().ToLowerInvariant()
            Dim value = line.Substring(idx + 1).Trim()
            If key.Length > 0 Then result(key) = value
        Next
        Return result
    End Function

    Private Shared Function GetValue(values As Dictionary(Of String, String), key As String) As String
        Dim v As String = Nothing
        If values.TryGetValue(key, v) Then Return v
        Return Nothing
    End Function
End Class

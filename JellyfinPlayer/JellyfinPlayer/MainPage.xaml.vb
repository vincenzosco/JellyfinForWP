Imports System
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

''' <summary>
''' Server connect / login page.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Dim session = AppSession.Current
        ServerBox.Text = If(String.IsNullOrEmpty(session.BaseUrl), "http://", session.BaseUrl)
    End Sub

    Private Async Sub ConnectButton_Click(sender As Object, e As RoutedEventArgs)
        Dim baseUrl = ServerBox.Text.Trim()
        If String.IsNullOrEmpty(baseUrl) Then
            StatusText.Text = "Enter the server address."
            Return
        End If
        If Not baseUrl.StartsWith("http") Then baseUrl = "http://" & baseUrl

        Busy.IsActive = True
        ConnectButton.IsEnabled = False
        StatusText.Text = ""

        Try
            Dim session = AppSession.Current
            Dim urls As New JellyfinUrls(baseUrl)
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, session.DeviceId)

            Dim info = Await client.GetServerInfoAsync()
            If info Is Nothing Then Throw New JellyfinException("That address is not a Jellyfin server.", 0)

            Dim result = Await client.AuthenticateAsync(UserBox.Text.Trim(), PassBox.Password)
            If result Is Nothing OrElse String.IsNullOrEmpty(result.AccessToken) Then
                StatusText.Text = "Wrong username or password."
                Return
            End If

            session.BaseUrl = urls.BaseUrl
            session.AccessToken = client.AccessToken
            session.UserId = client.UserId
            session.ServerId = client.ServerId
            session.Save()

            NavigationService.Navigate(GetType(HomePage), Nothing)
        Catch ex As JellyfinException
            StatusText.Text = If(ex.StatusCode = 401, "Wrong username or password.", ex.Message)
        Catch ex As Exception
            StatusText.Text = ex.Message
        Finally
            Busy.IsActive = False
            ConnectButton.IsEnabled = True
        End Try
    End Sub
End Class

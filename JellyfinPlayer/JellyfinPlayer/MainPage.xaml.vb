Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

''' <summary>
''' Server connect / login page. Auto-detects the server when no address is saved.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)

        Dim session = AppSession.Current
        If Not String.IsNullOrEmpty(session.BaseUrl) Then
            ServerBox.Text = session.BaseUrl
            DiscoveryStatus.Text = Localized.Text("StatusUsingSavedServer")
            Return
        End If

        ServerBox.Text = "http://"
        Await DiscoverAsync()
    End Sub

    ' Fills the address field from the first UDP reply. When nothing answers, or
    ' several servers do, the user keeps (or still has) manual control.
    Private Async Function DiscoverAsync() As Task
        DiscoveryStatus.Text = Localized.Text("DiscoverySearching")
        Try
            Dim found = Await UdpServerDiscovery.DiscoverAsync(2500)

            If found Is Nothing OrElse found.Count = 0 Then
                DiscoveryStatus.Text = Localized.Text("DiscoveryNone")
                Return
            End If

            ' 7359 answers with anything that cares to reply, so the address is only
            ' filled in once it has answered as a Jellyfin server.
            Dim session = AppSession.Current
            Dim server = Await ServerProbe.FindFirstJellyfinAsync(
                found, Function(baseUrl) New JellyfinApiClient(New HttpTransport(), New JellyfinUrls(baseUrl), session.DeviceId))

            If server Is Nothing Then
                DiscoveryStatus.Text = Localized.Text("DiscoveryRejected")
                Return
            End If

            ServerBox.Text = server.Address
            If found.Count = 1 Then
                DiscoveryStatus.Text = Localized.Format("DiscoveryFound", server.Name)
            Else
                DiscoveryStatus.Text = Localized.Format("DiscoveryFoundMany", found.Count, server.Name)
            End If
        Catch ex As Exception
            DiscoveryStatus.Text = Localized.Format("DiscoveryFailed", ex.Message)
        End Try
    End Function

    Private Async Sub ConnectButton_Click(sender As Object, e As RoutedEventArgs)
        Dim baseUrl = ServerBox.Text.Trim()
        If String.IsNullOrEmpty(baseUrl) Then
            StatusText.Text = Localized.Text("StatusEnterAddress")
            Return
        End If
        If Not baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then baseUrl = "http://" & baseUrl

        Busy.IsActive = True
        ConnectButton.IsEnabled = False
        StatusText.Text = ""

        Try
            Dim session = AppSession.Current
            Dim urls As New JellyfinUrls(baseUrl)
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, session.DeviceId)

            Dim info = Await client.GetServerInfoAsync()
            If info Is Nothing Then Throw New JellyfinException(Localized.Text("StatusNotJellyfin"), 0)

            Dim result = Await client.AuthenticateAsync(UserBox.Text.Trim(), PassBox.Password)
            If result Is Nothing OrElse String.IsNullOrEmpty(result.AccessToken) Then
                StatusText.Text = Localized.Text("StatusWrongCredentials")
                Return
            End If

            session.BaseUrl = urls.BaseUrl
            session.AccessToken = client.AccessToken
            session.UserId = client.UserId
            session.ServerId = client.ServerId
            session.Save()

            NavigationService.Navigate(GetType(HomePage), Nothing)
        Catch ex As JellyfinException
            StatusText.Text = If(ex.StatusCode = 401, Localized.Text("StatusWrongCredentials"), Localized.DescribeError(ex))
        Catch ex As Exception
            StatusText.Text = Localized.DescribeError(ex)
        Finally
            Busy.IsActive = False
            ConnectButton.IsEnabled = True
        End Try
    End Sub
End Class

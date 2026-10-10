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
            DiscoveryStatus.Text = "Using saved server."
            Return
        End If

        ServerBox.Text = "http://"
        Await DiscoverAsync()
    End Sub

    ' Fills the address field from the first UDP reply. When nothing answers, or
    ' several servers do, the user keeps (or still has) manual control.
    Private Async Function DiscoverAsync() As Task
        DiscoveryStatus.Text = "Looking for a Jellyfin server..."
        Try
            Dim found = Await UdpServerDiscovery.DiscoverAsync(2500)

            If found Is Nothing OrElse found.Count = 0 Then
                DiscoveryStatus.Text = "No server found automatically. Enter the address."
                Return
            End If

            ServerBox.Text = found(0).Address
            If found.Count = 1 Then
                DiscoveryStatus.Text = "Found " & found(0).Name & "."
            Else
                Dim names As New List(Of String)()
                For Each server In found
                    names.Add(server.Name & " (" & server.Address & ")")
                Next
                DiscoveryStatus.Text = found.Count.ToString() & " servers found, using " & found(0).Name & ". Also: " & String.Join(", ", names.ToArray())
            End If
        Catch ex As Exception
            DiscoveryStatus.Text = "Automatic detection failed: " & ex.Message
        End Try
    End Function

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

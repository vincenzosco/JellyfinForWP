Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class HomePage
    Inherits PhoneApplicationPage

    Public Sub New()
        InitializeComponent()
        SwitchAccountButton.Content = Localized.Text("SwitchAccountButton.Content")
        SearchButton.Content = Localized.Text("SearchButton.Content")
        SettingsButton.Content = Localized.Text("SettingsButton.Content")
        ResumeHeader.Text = Localized.Text("ResumeHeader.Text")
        NextUpHeader.Text = Localized.Text("NextUpHeader.Text")
        PlaylistsHeader.Text = Localized.Text("PlaylistsHeader")
        DownloadsHeader.Text = Localized.Text("DownloadsHeader")
        ManageDownloadsButton.Content = Localized.Text("ManageDownloads")
        LibrariesHeader.Text = Localized.Text("LibrariesHeader.Text")
        ' Both sections appear only when they have something to show.
        PlaylistsHeader.Visibility = Visibility.Collapsed
        DownloadsHeader.Visibility = Visibility.Collapsed
        ManageDownloadsButton.Visibility = Visibility.Collapsed
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        If Not AppSession.Current.IsLoggedIn Then
            Services.NavigationService.Navigate(GetType(MainPage), Nothing)
            Return
        End If
        Await LoadAsync()
    End Sub

    Private Async Function LoadAsync() As Task
        Dim session = AppSession.Current
        Dim client = session.CreateClient()

        Try
            Dim views = Await client.GetViewsAsync(session.UserId)
            ViewsList.ItemsSource = PosterItem.Build(views, session.BaseUrl, session.AccessToken)
        Catch ex As Exception
            If FailureText.IsUnauthorized(ex) Then
                Services.NavigationService.Navigate(GetType(MainPage), session.ActiveAccount)
                Return
            End If
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        Try
            Dim resumeItems = Await client.GetResumeAsync(session.UserId, 12)
            ResumeList.ItemsSource = PosterItem.Build(resumeItems, session.BaseUrl, session.AccessToken)
            ResumeHeader.Visibility = If(resumeItems.Count > 0, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            If FailureText.IsUnauthorized(ex) Then
                Services.NavigationService.Navigate(GetType(MainPage), session.ActiveAccount)
                Return
            End If
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        Try
            Dim nextUp = Await client.GetNextUpAsync(session.UserId, 12)
            NextUpList.ItemsSource = PosterItem.Build(nextUp, session.BaseUrl, session.AccessToken)
            NextUpHeader.Visibility = If(nextUp.Count > 0, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            If FailureText.IsUnauthorized(ex) Then
                Services.NavigationService.Navigate(GetType(MainPage), session.ActiveAccount)
                Return
            End If
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        Try
            Dim playlists = Await client.GetPlaylistsAsync(session.UserId)
            PlaylistsList.ItemsSource = PosterItem.Build(playlists, session.BaseUrl, session.AccessToken)
            PlaylistsHeader.Visibility = If(playlists.Count > 0, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            If FailureText.IsUnauthorized(ex) Then
                Services.NavigationService.Navigate(GetType(MainPage), session.ActiveAccount)
                Return
            End If
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        ShowDownloads()

        ' A migrated account has no name in the store; fetch it once so the picker
        ' shows a person instead of an id.
        If String.IsNullOrEmpty(session.ActiveAccount.UserName) Then
            Try
                Dim user = Await client.GetUserAsync(session.UserId)
                If user IsNot Nothing Then session.SetUserName(session.ActiveAccount.Id, user.Name)
            Catch
                ' The name is cosmetic: a failure here must not disturb Home.
            End Try
        End If
    End Function

    Private Sub View_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then Services.NavigationService.Navigate(GetType(LibraryPage), item.Raw)
    End Sub

    Private Sub Item_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub

    ' Downloads are local: they show even with no server in reach.
    Private Sub ShowDownloads()
        Dim rows = DownloadRow.Build(DownloadStore.Current.Records())
        DownloadsList.ItemsSource = rows
        Dim any = rows.Count > 0
        DownloadsHeader.Visibility = If(any, Visibility.Visible, Visibility.Collapsed)
        ManageDownloadsButton.Visibility = If(any, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub Playlist_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then Services.NavigationService.Navigate(GetType(PlaylistPage), item.Raw)
    End Sub

    Private Sub Download_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, DownloadRow)
        If item IsNot Nothing Then Services.NavigationService.Navigate(GetType(PlayerPage), item.Record)
    End Sub

    Private Sub ManageDownloads_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(DownloadsPage), Nothing)
    End Sub

    Private Sub Search_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(SearchPage), Nothing)
    End Sub

    Private Sub Settings_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(SettingsPage), Nothing)
    End Sub

    Private Sub SwitchAccount_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub
End Class

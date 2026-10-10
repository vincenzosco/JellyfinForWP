Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class HomePage
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        If Not AppSession.Current.IsLoggedIn Then
            NavigationService.Navigate(GetType(MainPage), Nothing)
            Return
        End If
        Await LoadAsync()
    End Sub

    Private Async Function LoadAsync() As Task
        Dim session = AppSession.Current
        Dim client = session.CreateClient()

        Try
            Dim views = Await client.GetViewsAsync(session.UserId)
            ViewsGrid.ItemsSource = PosterItem.Build(views, session.BaseUrl, session.AccessToken)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        Try
            Dim resumeItems = Await client.GetResumeAsync(session.UserId, 12)
            ResumeList.ItemsSource = PosterItem.Build(resumeItems, session.BaseUrl, session.AccessToken)
            ResumeHeader.Visibility = If(resumeItems.Count > 0, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        Try
            Dim nextUp = Await client.GetNextUpAsync(session.UserId, 12)
            NextUpList.ItemsSource = PosterItem.Build(nextUp, session.BaseUrl, session.AccessToken)
            NextUpHeader.Visibility = If(nextUp.Count > 0, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Function

    Private Sub View_Click(sender As Object, e As ItemClickEventArgs)
        Dim item = TryCast(e.ClickedItem, PosterItem)
        If item IsNot Nothing Then NavigationService.Navigate(GetType(LibraryPage), item.Raw)
    End Sub

    Private Sub Item_Click(sender As Object, e As ItemClickEventArgs)
        Dim item = TryCast(e.ClickedItem, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub

    Private Sub Search_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(SearchPage), Nothing)
    End Sub

    Private Sub Settings_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(SettingsPage), Nothing)
    End Sub
End Class

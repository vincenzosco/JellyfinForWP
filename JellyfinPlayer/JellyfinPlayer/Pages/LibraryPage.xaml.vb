Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class LibraryPage
    Inherits PhoneApplicationPage

    Private Const PageSize As Integer = 50
    Private Const IncludeTypes As String = "Movie,Series,Episode,Video,BoxSet"
    Private Const SortByName As String = "SortName"
    Private Const SortByDateAdded As String = "DateCreated"

    Private _view As BaseItemDto
    Private _startIndex As Integer = 0
    Private _items As New List(Of PosterItem)()
    Private _sortBy As String = SortByName
    Private _genre As GenreChoice
    Private _genreChoices As List(Of GenreChoice)
    Private _suppressGenre As Boolean

    Public Sub New()
        InitializeComponent()
        LoadMoreButton.Content = Localized.Text("LoadMoreButton.Content")
        GenrePanelTitle.Text = Localized.Text("GenrePanelTitle")
        CloseGenreButton.Content = Localized.Text("ActionClose")
        UpdateSortLabel()
        UpdateGenreLabel()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _view = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        ' The frame caches pages, so this instance can be reused for another
        ' library, or navigated back into. Start from the first page again.
        _startIndex = 0
        _items.Clear()
        _sortBy = SortByName
        _genre = Nothing
        _genreChoices = Nothing
        GenrePanel.Visibility = Visibility.Collapsed
        UpdateSortLabel()
        UpdateGenreLabel()
        ErrorText.Text = ""
        TitleText.Text = If(_view IsNot Nothing, _view.Name, Localized.Text("LibraryTitle"))
        If _view Is Nothing Then Return
        Await LoadGenresAsync()
        Await LoadNextPageAsync()
    End Sub

    ' A genre filter is a convenience: a failure leaves the list with "all" and
    ' the library still opens.
    Private Async Function LoadGenresAsync() As Task
        Try
            Dim session = AppSession.Current
            Dim genres = Await session.CreateClient().GetGenresAsync(session.UserId, _view.Id)
            _genreChoices = LibraryFilters.GenreChoices(genres, Localized.Text("AllGenres"))
        Catch
            _genreChoices = LibraryFilters.GenreChoices(Nothing, Localized.Text("AllGenres"))
        End Try
        _suppressGenre = True
        GenreList.ItemsSource = _genreChoices
        GenreList.SelectedIndex = 0
        _suppressGenre = False
    End Function

    Private Async Function LoadNextPageAsync() As Task
        Dim session = AppSession.Current
        Busy.Visibility = Visibility.Visible
        Try
            Dim genreIds = If(_genre IsNot Nothing AndAlso Not _genre.IsAll, _genre.Id, Nothing)
            Dim q = Await session.CreateClient().GetItemsAsync(session.UserId, _view.Id, IncludeTypes, _startIndex, PageSize, _sortBy, "Ascending", genreIds)
            For Each poster In PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken)
                _items.Add(poster)
            Next
            _startIndex += PageSize
            ' Reset the source so the list re-renders the accumulated items.
            ItemsList.ItemsSource = Nothing
            ItemsList.ItemsSource = _items
            LoadMoreButton.Visibility = If(_items.Count < q.TotalRecordCount, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        Finally
            Busy.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Async Sub LoadMore_Click(sender As Object, e As RoutedEventArgs)
        Await LoadNextPageAsync()
    End Sub

    Private Sub UpdateSortLabel()
        SortButton.Content = Localized.Text(If(_sortBy = SortByName, "SortByName", "SortByDateAdded"))
    End Sub

    Private Sub UpdateGenreLabel()
        ' The button states the filter it is applying: the genre's own name, or
        ' "Genre" while every genre is shown.
        Dim name = If(_genre IsNot Nothing AndAlso Not _genre.IsAll, _genre.Name, Localized.Text("GenreFilter"))
        GenreButton.Content = name
    End Sub

    Private Sub Genre_Click(sender As Object, e As RoutedEventArgs)
        GenrePanel.Visibility = If(GenrePanel.Visibility = Visibility.Visible, Visibility.Collapsed, Visibility.Visible)
    End Sub

    Private Sub CloseGenre_Click(sender As Object, e As RoutedEventArgs)
        GenrePanel.Visibility = Visibility.Collapsed
    End Sub

    Private Async Sub Genre_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _suppressGenre Then Return
        Dim chosen = TryCast(GenreList.SelectedItem, GenreChoice)
        If chosen Is Nothing Then Return
        GenrePanel.Visibility = Visibility.Collapsed
        If _genre IsNot Nothing AndAlso _genre.Id = chosen.Id Then Return
        _genre = chosen
        UpdateGenreLabel()
        ' A different filter means a different first page.
        _startIndex = 0
        _items.Clear()
        ItemsList.ItemsSource = Nothing
        LoadMoreButton.Visibility = Visibility.Collapsed
        If _view IsNot Nothing Then Await LoadNextPageAsync()
    End Sub

    Private Async Sub Sort_Click(sender As Object, e As RoutedEventArgs)
        _sortBy = If(_sortBy = SortByName, SortByDateAdded, SortByName)
        UpdateSortLabel()
        ' A different order means a different first page, not a second one.
        _startIndex = 0
        _items.Clear()
        ItemsList.ItemsSource = Nothing
        LoadMoreButton.Visibility = Visibility.Collapsed
        If _view IsNot Nothing Then Await LoadNextPageAsync()
    End Sub

    Private Sub Item_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub
End Class

Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class LibraryPage
    Inherits Page

    Private Const PageSize As Integer = 50
    Private Const IncludeTypes As String = "Movie,Series,Episode,Video,BoxSet"

    Private _view As BaseItemDto
    Private _startIndex As Integer = 0
    Private _items As New List(Of PosterItem)()

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _view = TryCast(e.Parameter, BaseItemDto)
        TitleText.Text = If(_view IsNot Nothing, _view.Name, "Library")
        Await LoadNextPageAsync()
    End Sub

    Private Async Function LoadNextPageAsync() As Task
        Dim session = AppSession.Current
        Busy.IsActive = True
        Try
            Dim q = Await session.CreateClient().GetItemsAsync(session.UserId, _view.Id, IncludeTypes, _startIndex, PageSize)
            For Each poster In PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken)
                _items.Add(poster)
            Next
            _startIndex += PageSize
            ' Reset the source so the grid re-renders the accumulated items.
            ItemsGrid.ItemsSource = Nothing
            ItemsGrid.ItemsSource = _items
            LoadMoreButton.Visibility = If(_items.Count < q.TotalRecordCount, Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            ErrorText.Text = ex.Message
        Finally
            Busy.IsActive = False
        End Try
    End Function

    Private Async Sub LoadMore_Click(sender As Object, e As RoutedEventArgs)
        Await LoadNextPageAsync()
    End Sub

    Private Sub Item_Click(sender As Object, e As ItemClickEventArgs)
        Dim item = TryCast(e.ClickedItem, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub
End Class

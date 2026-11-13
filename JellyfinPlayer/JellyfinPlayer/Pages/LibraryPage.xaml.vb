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

    Private _view As BaseItemDto
    Private _startIndex As Integer = 0
    Private _items As New List(Of PosterItem)()

    Public Sub New()
        InitializeComponent()
        LoadMoreButton.Content = Localized.Text("LoadMoreButton.Content")
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _view = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        ' The frame caches pages, so this instance can be reused for another
        ' library, or navigated back into. Start from the first page again.
        _startIndex = 0
        _items.Clear()
        ErrorText.Text = ""
        TitleText.Text = If(_view IsNot Nothing, _view.Name, Localized.Text("LibraryTitle"))
        If _view Is Nothing Then Return
        Await LoadNextPageAsync()
    End Sub

    Private Async Function LoadNextPageAsync() As Task
        Dim session = AppSession.Current
        Busy.Visibility = Visibility.Visible
        Try
            Dim q = Await session.CreateClient().GetItemsAsync(session.UserId, _view.Id, IncludeTypes, _startIndex, PageSize)
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

    Private Sub Item_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub
End Class

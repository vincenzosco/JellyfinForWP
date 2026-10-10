Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class SearchPage
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub QueryBox_KeyDown(sender As Object, e As Windows.UI.Xaml.Input.KeyRoutedEventArgs)
        If e.Key <> Windows.System.VirtualKey.Enter Then Return
        Await RunSearchAsync()
    End Sub

    Private Async Function RunSearchAsync() As Task
        Dim term = QueryBox.Text.Trim()
        If String.IsNullOrEmpty(term) Then Return

        Dim session = AppSession.Current
        ErrorText.Text = ""
        EmptyText.Text = "Searching..."
        EmptyText.Visibility = Visibility.Visible

        Try
            Dim q = Await session.CreateClient().SearchAsync(session.UserId, term)
            ResultsList.ItemsSource = PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken)
            If q.Items.Count = 0 Then
                EmptyText.Text = "No results."
            Else
                EmptyText.Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            ErrorText.Text = ex.Message
            EmptyText.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Sub Result_Click(sender As Object, e As ItemClickEventArgs)
        Dim item = TryCast(e.ClickedItem, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub
End Class

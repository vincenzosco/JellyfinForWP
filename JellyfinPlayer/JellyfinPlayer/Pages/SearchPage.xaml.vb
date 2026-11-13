Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class SearchPage
    Inherits PhoneApplicationPage

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub QueryBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key <> Key.Enter Then Return
        Await RunSearchAsync()
    End Sub

    Private Async Function RunSearchAsync() As Task
        Dim term = QueryBox.Text.Trim()
        If String.IsNullOrEmpty(term) Then Return

        Dim session = AppSession.Current
        ErrorText.Text = ""
        EmptyText.Text = Localized.Text("Searching")
        EmptyText.Visibility = Visibility.Visible

        Try
            Dim q = Await session.CreateClient().SearchAsync(session.UserId, term)
            ResultsList.ItemsSource = PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken)
            If q.Items.Count = 0 Then
                EmptyText.Text = Localized.Text("NoResults")
            Else
                EmptyText.Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
            EmptyText.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Sub Result_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then ItemNavigator.Open(item.Raw)
    End Sub
End Class

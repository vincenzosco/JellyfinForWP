Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class PlaylistPage
    Inherits PhoneApplicationPage

    Private _playlist As BaseItemDto
    Private _entries As List(Of BaseItemDto)

    Public Sub New()
        InitializeComponent()
        PlayAllButton.Content = Localized.Text("PlayAll")
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _playlist = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        If _playlist Is Nothing Then Return
        TitleText.Text = _playlist.Name
        ErrorText.Text = ""
        ' This instance is reused for another playlist: no entries from the old
        ' one, and the button back on for a playlist that has them.
        _entries = Nothing
        EntryList.ItemsSource = Nothing
        PlayAllButton.Visibility = Visibility.Visible

        Dim session = AppSession.Current
        Try
            _entries = Await session.CreateClient().GetPlaylistItemsAsync(_playlist.Id, session.UserId)
            If _entries Is Nothing OrElse _entries.Count = 0 Then
                ErrorText.Text = Localized.Text("PlaylistEmpty")
                PlayAllButton.Visibility = Visibility.Collapsed
                Return
            End If
            EntryList.ItemsSource = PosterItem.Build(_entries, session.BaseUrl, session.AccessToken)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
            PlayAllButton.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Sub PlayAll_Click(sender As Object, e As RoutedEventArgs)
        PlayAt(0)
    End Sub

    Private Sub Entry_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item Is Nothing OrElse _entries Is Nothing Then Return
        For index = 0 To _entries.Count - 1
            If _entries(index).Id = item.Id Then
                PlayAt(index)
                Return
            End If
        Next
    End Sub

    ' The whole playlist travels with the player, so the entries after this one
    ' continue on their own instead of stopping at the end of the item.
    Private Sub PlayAt(index As Integer)
        Dim item = PlayQueue.At(_entries, index)
        If item Is Nothing Then Return
        Services.NavigationService.Navigate(GetType(PlayerPage), New PlayRequest With {
            .Item = item, .Queue = _entries, .Index = index
        })
    End Sub
End Class

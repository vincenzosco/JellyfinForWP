Imports System.Collections.Generic
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class DownloadsPage
    Inherits PhoneApplicationPage

    Public Sub New()
        InitializeComponent()
        TitleText.Text = Localized.Text("DownloadsTitle")
        EmptyText.Text = Localized.Text("DownloadEmpty")
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Refresh()
    End Sub

    Private Sub Refresh()
        Dim rows = DownloadRow.Build(DownloadStore.Current.Records())
        DownloadList.ItemsSource = rows
        EmptyText.Visibility = If(rows.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub Row_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, DownloadRow)
        If item IsNot Nothing Then Services.NavigationService.Navigate(GetType(PlayerPage), item.Record)
    End Sub

    Private Sub Delete_Click(sender As Object, e As RoutedEventArgs)
        Dim button = TryCast(sender, FrameworkElement)
        Dim item = TryCast(button.DataContext, DownloadRow)
        If item Is Nothing Then Return
        DownloadStore.Current.Remove(item.Record.ItemId)
        Refresh()
    End Sub
End Class

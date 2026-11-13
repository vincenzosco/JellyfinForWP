Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Media.Imaging

Partial Public Class ItemDetailPage
    Inherits PhoneApplicationPage

    Private _item As BaseItemDto
    Private _suppressToggle As Boolean
    Private _download As DownloadRecordDto
    Private _downloading As Boolean

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _item = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        If _item Is Nothing Then Return

        Dim session = AppSession.Current
        Try
            Dim full = Await session.CreateClient().GetItemAsync(session.UserId, _item.Id)
            If full IsNot Nothing Then _item = full
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        ApplyItem()
        _download = DownloadStore.Current.Find(_item.Id)
        UpdateDownloadButton()
    End Sub

    Private Sub UpdateDownloadButton()
        DownloadButton.IsEnabled = Not _downloading
        DownloadButton.Content = Localized.Text(If(_download IsNot Nothing, "RemoveDownload", "DownloadAction"))
    End Sub

    ' The button is a toggle: download, or remove the copy that is already there.
    ' The tap runs the copy; the download finishes even if the page scrolls, but
    ' leaving the app stops it (there is no background agent on this platform).
    Private Async Sub Download_Click(sender As Object, e As RoutedEventArgs)
        If _downloading OrElse _item Is Nothing Then Return
        If _download IsNot Nothing Then
            DownloadStore.Current.Remove(_item.Id)
            _download = Nothing
            DownloadProgress.Visibility = Visibility.Collapsed
            UpdateDownloadButton()
            Return
        End If

        _downloading = True
        ErrorText.Text = ""
        DownloadProgress.Value = 0
        DownloadProgress.Visibility = Visibility.Visible
        UpdateDownloadButton()
        ' The frame caches this page: a download finishes against the item it was
        ' started for, even if the user has opened another one meanwhile.
        Dim downloadingItemId = _item.Id
        Try
            Dim record = Await DownloadService.DownloadAsync(_item, Sub(percent)
                                                                       Deployment.Current.Dispatcher.BeginInvoke(Sub() ShowPercent(percent))
                                                                   End Sub)
            If _item IsNot Nothing AndAlso _item.Id = downloadingItemId Then _download = record
        Catch ex As Exception
            If _item IsNot Nothing AndAlso _item.Id = downloadingItemId Then ErrorText.Text = Localized.DescribeError(ex)
        Finally
            _downloading = False
            DownloadProgress.Visibility = Visibility.Collapsed
            UpdateDownloadButton()
        End Try
    End Sub

    Private Sub ShowPercent(percent As Integer)
        If Not _downloading Then Return
        DownloadProgress.Value = percent
        DownloadButton.Content = Localized.Format("Downloading", percent)
    End Sub

    Private Sub ApplyItem()
        TitleText.Text = _item.Name
        OverviewText.Text = _item.Overview

        Dim meta As String = ""
        If _item.SeriesName IsNot Nothing Then meta &= _item.SeriesName
        If _item.ParentIndexNumber.HasValue Then meta &= "  S" & _item.ParentIndexNumber.Value.ToString("00")
        If _item.IndexNumber.HasValue Then meta &= "E" & _item.IndexNumber.Value.ToString("00")
        If _item.RunTimeTicks.HasValue Then meta &= "  " & Ticks.FormatDuration(_item.RunTimeTicks)
        MetaText.Text = meta.Trim()

        If ImageHelper.HasBackdropImage(_item) Then
            BackdropImage.Source = New BitmapImage(ImageHelper.BackdropUrl(AppSession.Current.BaseUrl, AppSession.Current.AccessToken, _item.Id, 720))
        ElseIf ImageHelper.HasPrimaryImage(_item) Then
            ' No backdrop: fall back to the primary image so the banner is not empty.
            BackdropImage.Source = New BitmapImage(ImageHelper.ImageUrl(AppSession.Current.BaseUrl, AppSession.Current.AccessToken, _item.Id, 720))
        End If

        Dim resuming = _item.UserData IsNot Nothing AndAlso _item.UserData.PlaybackPositionTicks.HasValue AndAlso _item.UserData.PlaybackPositionTicks.Value > 0
        PlayButton.Content = If(resuming, Localized.Text("ResumeAction"), Localized.Text("PlayAction"))

        _suppressToggle = True
        FavoriteToggle.IsChecked = _item.UserData IsNot Nothing AndAlso _item.UserData.IsFavorite
        PlayedToggle.IsChecked = _item.UserData IsNot Nothing AndAlso _item.UserData.Played
        _suppressToggle = False

        FavoriteToggle.Content = Localized.Text("FavoriteToggle.Content")
        PlayedToggle.Content = Localized.Text("PlayedToggle.Content")
    End Sub

    Private Sub Play_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(PlayerPage), _item)
    End Sub

    Private Async Sub Favorite_Click(sender As Object, e As RoutedEventArgs)
        If _suppressToggle Then Return
        Dim session = AppSession.Current
        Dim value = FavoriteToggle.IsChecked.GetValueOrDefault(False)
        Try
            Await session.CreateClient().SetFavoriteAsync(session.UserId, _item.Id, value)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Sub

    Private Async Sub Played_Click(sender As Object, e As RoutedEventArgs)
        If _suppressToggle Then Return
        Dim session = AppSession.Current
        Dim value = PlayedToggle.IsChecked.GetValueOrDefault(False)
        Try
            Await session.CreateClient().SetPlayedAsync(session.UserId, _item.Id, value)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Sub
End Class

Imports System
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class ItemDetailPage
    Inherits Page

    Private _item As BaseItemDto
    Private _suppressToggle As Boolean

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _item = TryCast(e.Parameter, BaseItemDto)
        If _item Is Nothing Then Return

        Dim session = AppSession.Current
        Try
            Dim full = Await session.CreateClient().GetItemAsync(session.UserId, _item.Id)
            If full IsNot Nothing Then _item = full
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try

        ApplyItem()
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
            BackdropImage.Source = New Windows.UI.Xaml.Media.Imaging.BitmapImage(ImageHelper.BackdropUrl(AppSession.Current.BaseUrl, AppSession.Current.AccessToken, _item.Id, 720))
        ElseIf ImageHelper.HasPrimaryImage(_item) Then
            ' No backdrop: fall back to the primary image so the banner is not empty.
            BackdropImage.Source = New Windows.UI.Xaml.Media.Imaging.BitmapImage(ImageHelper.ImageUrl(AppSession.Current.BaseUrl, AppSession.Current.AccessToken, _item.Id, 720))
        End If

        Dim resuming = _item.UserData IsNot Nothing AndAlso _item.UserData.PlaybackPositionTicks.HasValue AndAlso _item.UserData.PlaybackPositionTicks.Value > 0
        PlayButton.Content = If(resuming, Localized.Text("ResumeAction"), Localized.Text("PlayAction"))

        _suppressToggle = True
        FavoriteToggle.IsChecked = _item.UserData IsNot Nothing AndAlso _item.UserData.IsFavorite
        PlayedToggle.IsChecked = _item.UserData IsNot Nothing AndAlso _item.UserData.Played
        _suppressToggle = False
    End Sub

    Private Sub Play_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(PlayerPage), _item)
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

Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

Partial Public Class SeriesPage
    Inherits PhoneApplicationPage

    Private _series As BaseItemDto
    Private _seasons As List(Of BaseItemDto)

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _series = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        If _series Is Nothing Then Return
        TitleText.Text = _series.Name

        Dim session = AppSession.Current
        Dim client = session.CreateClient()

        Try
            Dim full = Await client.GetItemAsync(session.UserId, _series.Id)
            If full IsNot Nothing Then Overview.Text = full.Overview
        Catch
            ' Overview is optional; ignore.
        End Try

        Try
            _seasons = Await client.GetSeasonsAsync(_series.Id, session.UserId)
            SeasonBox.ItemsSource = _seasons
            If _seasons IsNot Nothing AndAlso _seasons.Count > 0 Then SeasonBox.SelectedIndex = 0
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Sub

    Private Async Sub Season_Changed(sender As Object, e As SelectionChangedEventArgs)
        Dim season = TryCast(SeasonBox.SelectedItem, BaseItemDto)
        If season Is Nothing OrElse _series Is Nothing Then Return
        Dim session = AppSession.Current
        Try
            Dim eps = Await session.CreateClient().GetEpisodesAsync(_series.Id, session.UserId, season.Id)
            EpisodeList.ItemsSource = PosterItem.Build(eps, session.BaseUrl, session.AccessToken)
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Sub

    Private Sub Episode_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        Dim item = TryCast(row.DataContext, PosterItem)
        If item IsNot Nothing Then Services.NavigationService.Navigate(GetType(ItemDetailPage), item.Raw)
    End Sub
End Class

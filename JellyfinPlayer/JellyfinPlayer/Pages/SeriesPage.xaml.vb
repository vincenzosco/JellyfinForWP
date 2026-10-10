Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class SeriesPage
    Inherits Page

    Private _series As BaseItemDto
    Private _seasons As List(Of BaseItemDto)

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _series = TryCast(e.Parameter, BaseItemDto)
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
            ErrorText.Text = ex.Message
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
            ErrorText.Text = ex.Message
        End Try
    End Sub

    Private Sub Episode_Click(sender As Object, e As ItemClickEventArgs)
        Dim item = TryCast(e.ClickedItem, PosterItem)
        If item IsNot Nothing Then NavigationService.Navigate(GetType(ItemDetailPage), item.Raw)
    End Sub
End Class
